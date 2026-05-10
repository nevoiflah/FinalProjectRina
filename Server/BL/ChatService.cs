using FinalProjectRina.Server.DAL;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace FinalProjectRina.Server.BL;

public class ChatSession
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("initialQuestion")]
    public string? InitialQuestion { get; set; }

    [BsonElement("finalResult")]
    public string? FinalResult { get; set; }

    [BsonElement("startedAt")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("endedAt")]
    public DateTime? EndedAt { get; set; }
}

public class KnowledgeFact
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    [BsonElement("factText")]
    public string FactText { get; set; } = string.Empty;
}

public interface IChatService
{
    Task<string> GenerateReplyAsync(string? message, string? userId);
    Task EndSessionAsync(string userId);
}

public class ChatService : IChatService
{
    private readonly IAiProvider _aiProvider;
    private readonly IMongoCollection<ChatSession> _sessions;
    private readonly IMongoCollection<KnowledgeFact> _knowledge;

    public ChatService(IAiProvider aiProvider, IMongoDatabase database)
    {
        _aiProvider = aiProvider;
        _sessions = database.GetCollection<ChatSession>("chatSessions");
        _knowledge = database.GetCollection<KnowledgeFact>("ruppinKnowledge");
    }

    public async Task<string> GenerateReplyAsync(string? message, string? userId)
    {
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Message is required", nameof(message));
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required", nameof(userId));

        var context = await RetrieveContextAsync(message.Trim());
        var reply = await _aiProvider.GenerateChatReplyAsync(message.Trim(), context);
        await LogChatInteraction(userId, message, reply);
        return reply;
    }

    private async Task<List<string>> RetrieveContextAsync(string query)
    {
        try
        {
            var keywords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                               .Where(w => w.Length > 3)
                               .Select(w => w.ToLower())
                               .ToList();

            if (!keywords.Any()) return new List<string>();

            var recentSessions = await _sessions
                .Find(s => s.FinalResult != null)
                .SortByDescending(s => s.StartedAt)
                .Limit(100)
                .ToListAsync();

            var bestHistoryMatches = recentSessions
                .Select(s => new { s.FinalResult, Score = keywords.Count(k => (s.InitialQuestion ?? "").ToLower().Contains(k)) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(2)
                .Select(x => x.FinalResult!)
                .ToList();

            var allFacts = await _knowledge.Find(Builders<KnowledgeFact>.Filter.Empty).ToListAsync();

            var bestFactMatches = allFacts
                .Select(f => new { f.FactText, Score = keywords.Count(k => f.Category.ToLower().Contains(k) || f.FactText.ToLower().Contains(k)) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(3)
                .Select(x => "Ruppin Fact: " + x.FactText)
                .ToList();

            bestHistoryMatches.AddRange(bestFactMatches);
            return bestHistoryMatches;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RAG Retrieval Failed: {ex.Message}");
            return new List<string>();
        }
    }

    private async Task LogChatInteraction(string userId, string userMessage, string botReply)
    {
        var activeSession = await _sessions
            .Find(s => s.UserId == userId && s.EndedAt == null)
            .SortByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();

        if (activeSession != null)
        {
            await _sessions.UpdateOneAsync(
                s => s.Id == activeSession.Id,
                Builders<ChatSession>.Update.Set(s => s.FinalResult, botReply));
        }
        else
        {
            await _sessions.InsertOneAsync(new ChatSession
            {
                UserId = userId,
                InitialQuestion = userMessage,
                FinalResult = botReply,
                StartedAt = DateTime.UtcNow
            });
        }
    }

    public async Task EndSessionAsync(string userId)
    {
        var now = DateTime.UtcNow;
        await _sessions.UpdateManyAsync(
            s => s.UserId == userId && s.EndedAt == null,
            Builders<ChatSession>.Update.Set(s => s.EndedAt, now));
    }
}
