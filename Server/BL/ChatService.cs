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

    [BsonElement("embedding")]
    public float[]? Embedding { get; set; }
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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _openAiApiKey;

    public ChatService(IAiProvider aiProvider, IMongoDatabase database, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _aiProvider = aiProvider;
        _sessions = database.GetCollection<ChatSession>("chatSessions");
        _knowledge = database.GetCollection<KnowledgeFact>("ruppinKnowledge");
        _httpClientFactory = httpClientFactory;
        _openAiApiKey = configuration["OpenAI:ApiKey"] ?? "";
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
            var allFacts = await _knowledge.Find(Builders<KnowledgeFact>.Filter.Empty).ToListAsync();
            var factsWithEmbeddings = allFacts.Where(f => f.Embedding != null).ToList();

            List<string> bestFactMatches;

            if (factsWithEmbeddings.Count > 0)
            {
                // Semantic search via cosine similarity
                var queryEmbedding = await GetEmbeddingAsync(query);
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
                // Fallback: keyword matching
                var keywords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                   .Where(w => w.Length > 2)
                                   .Select(w => w.ToLower())
                                   .ToList();
                bestFactMatches = allFacts
                    .Select(f => new { f.FactText, Score = keywords.Count(k => f.Category.ToLower().Contains(k) || f.FactText.ToLower().Contains(k)) })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .Take(3)
                    .Select(x => "Ruppin Fact: " + x.FactText)
                    .ToList();
            }

            // Conversational history context (keyword-based — history is dynamic, no stored embeddings)
            var keywords2 = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                .Where(w => w.Length > 2)
                                .Select(w => w.ToLower())
                                .ToList();

            var recentSessions = await _sessions
                .Find(s => s.FinalResult != null)
                .SortByDescending(s => s.StartedAt)
                .Limit(100)
                .ToListAsync();

            var bestHistoryMatches = recentSessions
                .Select(s => new { s.FinalResult, Score = keywords2.Count(k => (s.InitialQuestion ?? "").ToLower().Contains(k)) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(2)
                .Select(x => x.FinalResult!)
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

    private async Task<float[]> GetEmbeddingAsync(string text)
    {
        var http = _httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _openAiApiKey);

        var response = await http.PostAsJsonAsync(
            "https://api.openai.com/v1/embeddings",
            new { model = "text-embedding-3-small", input = text });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
        return result!.Data[0].Embedding;
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

    private record EmbeddingResponse(
        [property: JsonPropertyName("data")] EmbeddingItem[] Data);

    private record EmbeddingItem(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("embedding")] float[] Embedding);

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
