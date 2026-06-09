using System.Net.Http.Json;
using System.Text.Json.Serialization;
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

    [BsonElement("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    [BsonElement("startedAt")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("endedAt")]
    public DateTime? EndedAt { get; set; }
}

/// <summary>A persisted conversation turn. Role is "user" or "assistant".</summary>
public class ChatMessage
{
    [BsonElement("role")]
    public string Role { get; set; } = string.Empty;

    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;

    [BsonElement("at")]
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class KnowledgeFact
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    [BsonElement("factText")]
    public string FactText { get; set; } = string.Empty;

    [BsonElement("embedding")]
    public float[]? Embedding { get; set; }
}

public interface IChatService
{
    Task<string> GenerateReplyAsync(string? message, string? userId, List<ChatTurn>? history = null, string? language = null);
    Task EndSessionAsync(string userId);
}

public class ChatService : IChatService
{
    private readonly IAiProvider _aiProvider;
    private readonly IMongoCollection<ChatSession> _sessions;
    private readonly KnowledgeCache _knowledgeCache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _pythonServiceUrl;

    public ChatService(IAiProvider aiProvider, IMongoDatabase database, KnowledgeCache knowledgeCache, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _aiProvider = aiProvider;
        _sessions = database.GetCollection<ChatSession>("chatSessions");
        _knowledgeCache = knowledgeCache;
        _httpClientFactory = httpClientFactory;
        _pythonServiceUrl = configuration["PythonService:Url"] ?? "http://localhost:5001";
    }

    public async Task<string> GenerateReplyAsync(string? message, string? userId, List<ChatTurn>? history = null, string? language = null)
    {
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Message is required", nameof(message));
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required", nameof(userId));

        var context = await RetrieveContextAsync(message.Trim());
        var reply = await _aiProvider.GenerateChatReplyAsync(message.Trim(), context, history, language);
        await LogChatInteraction(userId, message.Trim(), reply);
        return reply;
    }

    private async Task<List<string>> RetrieveContextAsync(string query)
    {
        try
        {
            var keywords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                               .Where(w => w.Length > 2)
                               .Select(w => w.ToLower())
                               .ToList();

            // Run embedding call (facts come from the in-memory cache, no DB round-trip).
            var embeddingTask = GetEmbeddingAsync(query);
            await embeddingTask;

            // Facts come from in-memory cache — no DB round-trip
            var allFacts = await _knowledgeCache.GetFactsAsync();
            var factsWithEmbeddings = allFacts.Where(f => f.Embedding != null).ToList();

            List<string> bestFactMatches;
            if (factsWithEmbeddings.Count > 0)
            {
                var queryEmbedding = embeddingTask.Result;
                bestFactMatches = factsWithEmbeddings
                    .Select(f => new { f.FactText, Score = CosineSimilarity(queryEmbedding, f.Embedding!) })
                    .Where(x => x.Score > 0.3f)
                    .OrderByDescending(x => x.Score)
                    .Take(3)
                    .Select(x => "Ruppin Fact: " + x.FactText)
                    .ToList();
            }
            else
            {
                bestFactMatches = allFacts
                    .Select(f => new { f.FactText, Score = keywords.Count(k => f.Category.ToLower().Contains(k) || f.FactText.ToLower().Contains(k)) })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .Take(3)
                    .Select(x => "Ruppin Fact: " + x.FactText)
                    .ToList();
            }

            // NOTE: We intentionally do NOT inject other users' past final answers into
            // the prompt. The live conversation history (passed separately) is the source
            // of memory; mixing in unrelated past sessions hurt consistency and accuracy.
            return bestFactMatches;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RAG Retrieval Failed: {ex.Message}");
            return new List<string>();
        }
    }

    private async Task<float[]> GetEmbeddingAsync(string text)
    {
        var http = _httpClientFactory.CreateClient();
        var response = await http.PostAsJsonAsync(
            $"{_pythonServiceUrl}/embed",
            new { texts = new[] { text } });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbedResponse>();
        return result!.Embeddings[0];
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        float dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        return dot / (MathF.Sqrt(magA) * MathF.Sqrt(magB));
    }

    private record EmbedResponse(
        [property: JsonPropertyName("embeddings")] float[][] Embeddings);

    private async Task LogChatInteraction(string userId, string userMessage, string botReply)
    {
        var now = DateTime.UtcNow;
        var newTurns = new[]
        {
            new ChatMessage { Role = "user", Content = userMessage, At = now },
            new ChatMessage { Role = "assistant", Content = botReply, At = now }
        };

        var activeSession = await _sessions
            .Find(s => s.UserId == userId && s.EndedAt == null)
            .SortByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();

        if (activeSession != null)
        {
            // Keep legacy FinalResult for the Admin dashboard, and append the full turns.
            await _sessions.UpdateOneAsync(
                s => s.Id == activeSession.Id,
                Builders<ChatSession>.Update
                    .Set(s => s.FinalResult, botReply)
                    .PushEach(s => s.Messages, newTurns));
        }
        else
        {
            await _sessions.InsertOneAsync(new ChatSession
            {
                UserId = userId,
                InitialQuestion = userMessage,
                FinalResult = botReply,
                Messages = newTurns.ToList(),
                StartedAt = now
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
