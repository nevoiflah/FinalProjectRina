using FinalProjectRina.Server.BL;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FinalProjectRina.Server.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILearningService _learningService;
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<ChatSession> _sessions;
    private readonly IMongoCollection<KnowledgeFact> _knowledge;
    private readonly IMongoDatabase _database;
    private readonly IConfiguration _configuration;
    private readonly KnowledgeCache _knowledgeCache;

    public AdminController(IUserService userService, ILearningService learningService, IMongoDatabase database, IConfiguration configuration, KnowledgeCache knowledgeCache)
    {
        _userService = userService;
        _learningService = learningService;
        _database = database;
        _configuration = configuration;
        _knowledgeCache = knowledgeCache;
        _users = database.GetCollection<User>("users");
        _sessions = database.GetCollection<ChatSession>("chatSessions");
        _knowledge = database.GetCollection<KnowledgeFact>("ruppinKnowledge");
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers([FromQuery] string userId)
    {
        if (!IsAdmin(userId)) return Unauthorized();

        var users = await _users.Find(Builders<User>.Filter.Empty)
                                .SortByDescending(u => u.CreatedAt)
                                .ToListAsync();

        var result = users.Select(u => new AdminUserDto(
            UserId: u.UserId,
            Name: u.Name,
            Email: u.Email,
            Organization: u.Organization ?? "N/A",
            IsAdmin: u.IsAdmin,
            JoinedAt: u.CreatedAt,
            LastLogin: u.LastLoginAt));

        return Ok(result);
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetAllSessions([FromQuery] string userId)
    {
        if (!IsAdmin(userId)) return Unauthorized();

        var sessions = await _sessions.Find(Builders<ChatSession>.Filter.Empty)
                                      .SortByDescending(s => s.StartedAt)
                                      .ToListAsync();

        var userIds = sessions.Select(s => s.UserId).Distinct().ToList();
        var usersMap = (await _users.Find(u => userIds.Contains(u.UserId)).ToListAsync())
                        .ToDictionary(u => u.UserId, u => u.Name);

        var result = sessions.Select(s => new AdminSessionDto(
            SessionId: s.Id,
            UserName: usersMap.GetValueOrDefault(s.UserId, "Unknown"),
            Question: s.InitialQuestion ?? "-",
            Result: s.FinalResult ?? "-",
            Date: s.StartedAt));

        return Ok(result);
    }

    [HttpPost("users/{id}/promote")]
    public IActionResult PromoteUser(string id, [FromQuery] string adminId)
    {
        if (!IsAdmin(adminId)) return Unauthorized();
        _userService.ToggleAdmin(id, true);
        return Ok(new { message = "User promoted" });
    }

    [HttpPost("users/{id}/demote")]
    public IActionResult DemoteUser(string id, [FromQuery] string adminId)
    {
        if (!IsAdmin(adminId)) return Unauthorized();
        _userService.ToggleAdmin(id, false);
        return Ok(new { message = "User demoted" });
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id, [FromQuery] string adminId)
    {
        if (!IsAdmin(adminId)) return Unauthorized();
        try
        {
            await _sessions.DeleteManyAsync(s => s.UserId == id);
            _userService.DeleteUser(id);
            return Ok(new { message = "User and history deleted" });
        }
        catch
        {
            return StatusCode(500, "Deletion failed");
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] string userId)
    {
        if (!IsAdmin(userId)) return Unauthorized();

        var cutoff = DateTime.UtcNow.AddDays(-30);
        var sessions = await _sessions.Find(s => s.StartedAt > cutoff).ToListAsync();

        var dailyTraffic = sessions
            .GroupBy(s => s.StartedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new { Date = g.Key.ToString("MM/dd"), Count = g.Count() })
            .ToList();

        var text = string.Join(" ", sessions.Select(s => (s.InitialQuestion ?? "").ToLower()));
        var degreeStats = new Dictionary<string, int>
        {
            { "Computer Science", CountKeywords(text, "computer", "code", "programming", "software", "מדעי המחשב") },
            { "Engineering", CountKeywords(text, "engineer", "electricity", "industry", "הנדסה", "חשמל") },
            { "Economics", CountKeywords(text, "economics", "business", "management", "account", "כלכלה", "מנהל עסקים") },
            { "Nursing", CountKeywords(text, "nursing", "nurse", "medical", "סיעוד") },
            { "Marine Sciences", CountKeywords(text, "marine", "sea", "ocean", "מדעי הים") },
            { "Mechina", CountKeywords(text, "mechina", "preparatory", "grade", "מכינה") }
        };

        var durations = sessions.Where(s => s.EndedAt.HasValue)
                                .Select(s => (s.EndedAt!.Value - s.StartedAt).TotalMinutes)
                                .ToList();
        var durationStats = new
        {
            Short = durations.Count(d => d < 2),
            Medium = durations.Count(d => d >= 2 && d < 10),
            Long = durations.Count(d => d >= 10)
        };

        return Ok(new { dailyTraffic, degreeStats, durationStats });
    }

    [HttpPost("knowledge/reseed")]
    public async Task<IActionResult> ReseedKnowledge([FromQuery] string adminId)
    {
        if (!IsAdmin(adminId)) return Unauthorized();
        var pythonUrl = _configuration["PythonService:Url"] ?? "http://localhost:5001";
        await KnowledgeSeeder.SeedAsync(_database, pythonUrl, force: true);
        _knowledgeCache.Invalidate();
        return Ok(new { message = "Knowledge base reseeded with embeddings." });
    }

    // ---- Self-improving RAG: review the facts the system wants to learn ----

    [HttpGet("learning")]
    public async Task<IActionResult> GetLearningQueue([FromQuery] string userId)
    {
        if (!IsAdmin(userId)) return Unauthorized();

        var pending = await _learningService.GetPendingCandidatesAsync();
        var result = pending.Select(c => new LearningCandidateDto(
            Id: c.Id,
            Fact: c.Fact,
            Category: c.Category,
            Confidence: Math.Round(c.Confidence, 2),
            Question: c.Question,
            Answer: c.Answer,
            CreatedAt: c.CreatedAt));

        return Ok(result);
    }

    [HttpPost("learning/{id}/approve")]
    public async Task<IActionResult> ApproveLearning(string id, [FromQuery] string adminId, [FromBody] ApproveLearningRequest? body)
    {
        if (!IsAdmin(adminId)) return Unauthorized();
        var ok = await _learningService.ApproveCandidateAsync(id, body?.Fact, body?.Category, adminId);
        if (!ok) return NotFound(new { error = "Candidate not found or already reviewed." });
        return Ok(new { message = "Fact added to the knowledge base." });
    }

    [HttpPost("learning/{id}/reject")]
    public async Task<IActionResult> RejectLearning(string id, [FromQuery] string adminId)
    {
        if (!IsAdmin(adminId)) return Unauthorized();
        var ok = await _learningService.RejectCandidateAsync(id, adminId);
        if (!ok) return NotFound(new { error = "Candidate not found or already reviewed." });
        return Ok(new { message = "Candidate rejected." });
    }

    [HttpGet("knowledge/learned")]
    public async Task<IActionResult> GetLearnedFacts([FromQuery] string userId)
    {
        if (!IsAdmin(userId)) return Unauthorized();

        var facts = await _knowledge.Find(f => f.Source == "learned")
                                    .SortByDescending(f => f.CreatedAt)
                                    .ToListAsync();

        var result = facts.Select(f => new LearnedFactDto(
            Id: f.Id.ToString(),
            Category: f.Category,
            FactText: f.FactText,
            CreatedAt: f.CreatedAt));

        return Ok(result);
    }

    [HttpDelete("knowledge/{id}")]
    public async Task<IActionResult> PruneLearnedFact(string id, [FromQuery] string adminId)
    {
        if (!IsAdmin(adminId)) return Unauthorized();
        if (!ObjectId.TryParse(id, out var objectId)) return BadRequest(new { error = "Invalid id." });

        // Only learned facts can be pruned here; seeded facts are managed via reseed.
        var result = await _knowledge.DeleteOneAsync(f => f.Id == objectId && f.Source == "learned");
        if (result.DeletedCount == 0) return NotFound(new { error = "Learned fact not found." });

        _knowledgeCache.Invalidate();
        return Ok(new { message = "Learned fact removed." });
    }

    private bool IsAdmin(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return false;
        var user = _userService.FindById(userId);
        return user != null && user.IsAdmin;
    }

    private static int CountKeywords(string text, params string[] keywords)
    {
        int count = 0;
        foreach (var k in keywords)
            count += System.Text.RegularExpressions.Regex.Matches(text, k).Count;
        return count;
    }
}

public record AdminUserDto(string UserId, string Name, string Email, string Organization, bool IsAdmin, DateTime JoinedAt, DateTime? LastLogin);
public record AdminSessionDto(string SessionId, string UserName, string Question, string Result, DateTime Date);
public record LearningCandidateDto(string Id, string Fact, string Category, double Confidence, string Question, string Answer, DateTime CreatedAt);
public record LearnedFactDto(string Id, string Category, string FactText, DateTime CreatedAt);
public record ApproveLearningRequest(string? Fact, string? Category);
