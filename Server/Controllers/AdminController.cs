using FinalProjectRina.Server.BL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace FinalProjectRina.Server.Controllers;

[ApiController]
[Route("api/admin")]
[Microsoft.AspNetCore.Cors.EnableCors("AllowAll")]
public class AdminController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IConfiguration _configuration;

    public AdminController(IUserService userService, IConfiguration configuration)
    {
        _userService = userService;
        _configuration = configuration;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers([FromQuery] string userId)
    {
        if (!IsAdmin(userId)) return Unauthorized();

        var users = new List<AdminUserDto>();
        const string sql = "SELECT UserId, Name, Email, Organization, IsAdmin, CreatedAt, LastLoginAt FROM NLA_Users ORDER BY CreatedAt DESC";

        using var conn = GetConnection();
        using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(new AdminUserDto(
                UserId: reader.GetString(reader.GetOrdinal("UserId")),
                Name: reader.GetString(reader.GetOrdinal("Name")),
                Email: reader.GetString(reader.GetOrdinal("Email")),
                Organization: reader.IsDBNull(reader.GetOrdinal("Organization")) ? "N/A" : reader.GetString(reader.GetOrdinal("Organization")),
                IsAdmin: reader.GetBoolean(reader.GetOrdinal("IsAdmin")),
                JoinedAt: reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                LastLogin: reader.IsDBNull(reader.GetOrdinal("LastLoginAt")) ? null : reader.GetDateTime(reader.GetOrdinal("LastLoginAt"))
            ));
        }
        return Ok(users);
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetAllSessions([FromQuery] string userId)
    {
        if (!IsAdmin(userId)) return Unauthorized();

        var sessions = new List<AdminSessionDto>();
        const string sql = @"
            SELECT s.SessionId, s.UserId, u.Name, s.InitialQuestion, s.FinalResult, s.StartedAt 
            FROM NLA_ChatSessions s
            LEFT JOIN NLA_Users u ON s.UserId = u.UserId 
            ORDER BY s.StartedAt DESC";

        using var conn = GetConnection();
        using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sessions.Add(new AdminSessionDto(
                SessionId: reader.GetInt32(reader.GetOrdinal("SessionId")),
                UserName: reader.IsDBNull(reader.GetOrdinal("Name")) ? "Unknown" : reader.GetString(reader.GetOrdinal("Name")),
                Question: reader.IsDBNull(reader.GetOrdinal("InitialQuestion")) ? "-" : reader.GetString(reader.GetOrdinal("InitialQuestion")),
                Result: reader.IsDBNull(reader.GetOrdinal("FinalResult")) ? "-" : reader.GetString(reader.GetOrdinal("FinalResult")),
                Date: reader.GetDateTime(reader.GetOrdinal("StartedAt"))
            ));
        }
        return Ok(sessions);
    }

    [HttpPost("users/{id}/promote")]
    public async Task<IActionResult> PromoteUser(string id, [FromQuery] string adminId)
    {
        if (!IsAdmin(adminId)) return Unauthorized();

        using var conn = GetConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("UPDATE NLA_Users SET IsAdmin = 1 WHERE UserId = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
        
        return Ok(new { message = "User promoted" });
    }

    [HttpPost("users/{id}/demote")]
    public async Task<IActionResult> DemoteUser(string id, [FromQuery] string adminId)
    {
        if (!IsAdmin(adminId)) return Unauthorized();

        using var conn = GetConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("UPDATE NLA_Users SET IsAdmin = 0 WHERE UserId = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();

        return Ok(new { message = "User demoted" });
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id, [FromQuery] string adminId)
    {
        if (!IsAdmin(adminId)) return Unauthorized();

        using var conn = GetConnection();
        await conn.OpenAsync();
        using var trans = conn.BeginTransaction();
        try
        {
            using (var cmd1 = new SqlCommand("DELETE FROM NLA_ChatSessions WHERE UserId = @id", conn, trans))
            {
                cmd1.Parameters.AddWithValue("@id", id);
                await cmd1.ExecuteNonQueryAsync();
            }
            using (var cmd2 = new SqlCommand("DELETE FROM NLA_Users WHERE UserId = @id", conn, trans))
            {
                cmd2.Parameters.AddWithValue("@id", id);
                await cmd2.ExecuteNonQueryAsync();
            }
            await trans.CommitAsync();
            return Ok(new { message = "User and history deleted" });
        }
        catch
        {
            await trans.RollbackAsync();
            return StatusCode(500, "Deletion failed");
        }
    }

    private bool IsAdmin(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return false;
        var user = _userService.FindById(userId);
        return user != null && user.IsAdmin;
    }

    private SqlConnection GetConnection()
    {
        var connStr = _configuration.GetConnectionString("myProjDB");
        if (!connStr.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase)) connStr += ";TrustServerCertificate=True;Encrypt=True";
        return new SqlConnection(connStr);
    }
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] string userId)
    {
        if (!IsAdmin(userId)) return Unauthorized();

        var sessions = new List<SessionData>();
        const string sql = "SELECT StartedAt, EndedAt, InitialQuestion FROM NLA_ChatSessions WHERE StartedAt > DATEADD(day, -30, GETUTCDATE())";

        using var conn = GetConnection();
        using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sessions.Add(new SessionData
            {
                StartedAt = reader.GetDateTime(0),
                EndedAt = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1),
                InitialQuestion = reader.IsDBNull(2) ? "" : reader.GetString(2)
            });
        }

        // 1. Daily Traffic (Line Chart)
        var dailyTraffic = sessions
            .GroupBy(s => s.StartedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new { Date = g.Key.ToString("MM/dd"), Count = g.Count() })
            .ToList();

        // 2. Degree Interest (Pie Chart)
        var text = string.Join(" ", sessions.Select(s => s.InitialQuestion.ToLower()));
        var degreeStats = new Dictionary<string, int>
        {
            { "Computer Science", CountKeywords(text, "computer", "code", "programming", "software", "מדעי המחשב") },
            { "Engineering", CountKeywords(text, "engineer", "electricity", "industry", "הנדסה", "חשמל") },
            { "Economics", CountKeywords(text, "economics", "business", "management", "account", "כלכלה", "מנהל עסקים") },
            { "Nursing", CountKeywords(text, "nursing", "nurse", "medical", "סיעוד") },
            { "Marine Sciences", CountKeywords(text, "marine", "sea", "ocean", "מדעי הים") },
            { "Mechina", CountKeywords(text, "mechina", "preparatory", "grade", "מכינה") }
        };

        // 3. Duration Distribution (Bar Chart)
        var durations = sessions.Where(s => s.EndedAt.HasValue).Select(s => (s.EndedAt!.Value - s.StartedAt).TotalMinutes).ToList();
        var durationStats = new
        {
            Short = durations.Count(d => d < 2),
            Medium = durations.Count(d => d >= 2 && d < 10),
            Long = durations.Count(d => d >= 10)
        };

        return Ok(new { dailyTraffic, degreeStats, durationStats });
    }

    private int CountKeywords(string text, params string[] keywords)
    {
        int count = 0;
        foreach (var k in keywords)
        {
            count += System.Text.RegularExpressions.Regex.Matches(text, k).Count;
        }
        return count;
    }

    private class SessionData
    {
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public string InitialQuestion { get; set; } = "";
    }
}

public record AdminUserDto(string UserId, string Name, string Email, string Organization, bool IsAdmin, DateTime JoinedAt, DateTime? LastLogin);
public record AdminSessionDto(int SessionId, string UserName, string Question, string Result, DateTime Date);
