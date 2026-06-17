using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace FinalProjectRina.Server.BL;

/// <summary>
/// A distilled fact awaiting a decision. Status:
/// "auto_approved" (added straight to the KB), "pending" (in the admin queue),
/// "approved" / "rejected" (after admin review).
/// </summary>
[BsonIgnoreExtraElements]
public class LearningCandidate
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("question")]
    public string Question { get; set; } = string.Empty;

    [BsonElement("answer")]
    public string Answer { get; set; } = string.Empty;

    [BsonElement("fact")]
    public string Fact { get; set; } = string.Empty;

    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    [BsonElement("embedding")]
    public float[]? Embedding { get; set; }

    [BsonElement("confidence")]
    public double Confidence { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = "pending";

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("reviewedBy")]
    [BsonIgnoreIfNull]
    public string? ReviewedBy { get; set; }

    [BsonElement("reviewedAt")]
    [BsonIgnoreIfNull]
    public DateTime? ReviewedAt { get; set; }
}

/// <summary>Raw 👍/👎 signal, kept for analytics. 👎 never feeds the knowledge base.</summary>
[BsonIgnoreExtraElements]
public class ChatFeedback
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("question")]
    public string Question { get; set; } = string.Empty;

    [BsonElement("answer")]
    public string Answer { get; set; } = string.Empty;

    [BsonElement("rating")]
    public string Rating { get; set; } = string.Empty; // "up" | "down"

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public interface ILearningService
{
    /// <summary>Record a 👍/👎. A 👍 kicks off the vetted learning loop.</summary>
    Task HandleFeedbackAsync(string userId, string question, string answer, string rating);

    Task<List<LearningCandidate>> GetPendingCandidatesAsync();
    Task<bool> ApproveCandidateAsync(string id, string? editedFact, string? editedCategory, string adminId);
    Task<bool> RejectCandidateAsync(string id, string adminId);
}

public class LearningService : ILearningService
{
    // Hybrid gate thresholds.
    private const double AutoApproveConfidence = 0.90; // >= → add to KB immediately
    private const double MinQueueConfidence = 0.60;    // [min, auto) → admin queue; below → discard
    private const float DuplicateSimilarity = 0.90f;   // cosine vs an existing fact → skip as duplicate

    private readonly IMongoCollection<KnowledgeFact> _knowledge;
    private readonly IMongoCollection<LearningCandidate> _candidates;
    private readonly IMongoCollection<ChatFeedback> _feedback;
    private readonly KnowledgeCache _knowledgeCache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _pythonServiceUrl;

    public LearningService(
        IMongoDatabase database,
        KnowledgeCache knowledgeCache,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _knowledge = database.GetCollection<KnowledgeFact>("ruppinKnowledge");
        _candidates = database.GetCollection<LearningCandidate>("learningCandidates");
        _feedback = database.GetCollection<ChatFeedback>("chatFeedback");
        _knowledgeCache = knowledgeCache;
        _httpClientFactory = httpClientFactory;
        _pythonServiceUrl = configuration["PythonService:Url"] ?? "http://localhost:5001";
    }

    public async Task HandleFeedbackAsync(string userId, string question, string answer, string rating)
    {
        var normalized = rating?.Trim().ToLowerInvariant() == "up" ? "up" : "down";

        // Always log the raw signal for analytics.
        await _feedback.InsertOneAsync(new ChatFeedback
        {
            UserId = userId,
            Question = question,
            Answer = answer,
            Rating = normalized
        });

        // Only a thumbs-up is a candidate for learning.
        if (normalized == "up")
            await LearnFromExchangeAsync(userId, question, answer);
    }

    private async Task LearnFromExchangeAsync(string userId, string question, string answer)
    {
        try
        {
            var distilled = await DistillAsync(question, answer);
            if (distilled is null || string.IsNullOrWhiteSpace(distilled.Fact))
                return;

            // Below the queue floor → not worth storing.
            if (distilled.Confidence < MinQueueConfidence)
                return;

            var embedding = await TryEmbedAsync(distilled.Fact);

            // De-dup against what we already know (only when we have vectors on both sides).
            if (embedding != null && await IsDuplicateAsync(embedding))
                return;

            var autoApprove = distilled.Confidence >= AutoApproveConfidence;

            var candidate = new LearningCandidate
            {
                UserId = userId,
                Question = question,
                Answer = answer,
                Fact = distilled.Fact.Trim(),
                Category = string.IsNullOrWhiteSpace(distilled.Category) ? "General" : distilled.Category.Trim(),
                Embedding = embedding,
                Confidence = distilled.Confidence,
                Status = autoApprove ? "auto_approved" : "pending"
            };
            await _candidates.InsertOneAsync(candidate);

            if (autoApprove)
                await AddFactToKnowledgeAsync(candidate.Fact, candidate.Category, candidate.Embedding, candidate.Id);
        }
        catch (Exception ex)
        {
            // Learning is best-effort; never surface its failures to the user.
            Console.WriteLine($"Learning loop failed: {ex.Message}");
        }
    }

    public async Task<List<LearningCandidate>> GetPendingCandidatesAsync() =>
        await _candidates.Find(c => c.Status == "pending")
                         .SortByDescending(c => c.CreatedAt)
                         .ToListAsync();

    public async Task<bool> ApproveCandidateAsync(string id, string? editedFact, string? editedCategory, string adminId)
    {
        var candidate = await _candidates.Find(c => c.Id == id).FirstOrDefaultAsync();
        if (candidate is null || candidate.Status != "pending") return false;

        var fact = string.IsNullOrWhiteSpace(editedFact) ? candidate.Fact : editedFact.Trim();
        var category = string.IsNullOrWhiteSpace(editedCategory) ? candidate.Category : editedCategory.Trim();

        // Re-embed if the admin edited the text, otherwise reuse the stored vector.
        var embedding = (!string.IsNullOrWhiteSpace(editedFact) && editedFact.Trim() != candidate.Fact)
            ? await TryEmbedAsync(fact)
            : candidate.Embedding;

        await AddFactToKnowledgeAsync(fact, category, embedding, candidate.Id);

        await _candidates.UpdateOneAsync(
            c => c.Id == id,
            Builders<LearningCandidate>.Update
                .Set(c => c.Status, "approved")
                .Set(c => c.Fact, fact)
                .Set(c => c.Category, category)
                .Set(c => c.ReviewedBy, adminId)
                .Set(c => c.ReviewedAt, DateTime.UtcNow));
        return true;
    }

    public async Task<bool> RejectCandidateAsync(string id, string adminId)
    {
        var result = await _candidates.UpdateOneAsync(
            c => c.Id == id && c.Status == "pending",
            Builders<LearningCandidate>.Update
                .Set(c => c.Status, "rejected")
                .Set(c => c.ReviewedBy, adminId)
                .Set(c => c.ReviewedAt, DateTime.UtcNow));
        return result.ModifiedCount > 0;
    }

    private async Task AddFactToKnowledgeAsync(string fact, string category, float[]? embedding, string provenance)
    {
        await _knowledge.InsertOneAsync(new KnowledgeFact
        {
            Category = category,
            FactText = fact,
            Embedding = embedding,
            Source = "learned",
            Provenance = provenance,
            CreatedAt = DateTime.UtcNow
        });
        _knowledgeCache.Invalidate(); // make the new fact retrievable immediately
    }

    private async Task<bool> IsDuplicateAsync(float[] embedding)
    {
        var facts = await _knowledgeCache.GetFactsAsync();
        return facts.Any(f => f.Embedding != null
                              && f.Embedding.Length == embedding.Length
                              && CosineSimilarity(embedding, f.Embedding) > DuplicateSimilarity);
    }

    private async Task<DistilledFact?> DistillAsync(string question, string answer)
    {
        var http = _httpClientFactory.CreateClient();
        var response = await http.PostAsJsonAsync(
            $"{_pythonServiceUrl}/distill",
            new { question, answer });

        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<DistilledFact>();
    }

    private async Task<float[]?> TryEmbedAsync(string text)
    {
        try
        {
            var http = _httpClientFactory.CreateClient();
            var response = await http.PostAsJsonAsync(
                $"{_pythonServiceUrl}/embed",
                new { texts = new[] { text } });

            if (!response.IsSuccessStatusCode) return null; // e.g. 503 when sentence-transformers is absent
            var result = await response.Content.ReadFromJsonAsync<EmbedResponse>();
            return result?.Embeddings is { Length: > 0 } ? result.Embeddings[0] : null;
        }
        catch
        {
            return null;
        }
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
        var denom = MathF.Sqrt(magA) * MathF.Sqrt(magB);
        return denom == 0 ? 0 : dot / denom;
    }

    private record DistilledFact(
        [property: JsonPropertyName("fact")] string Fact,
        [property: JsonPropertyName("category")] string Category,
        [property: JsonPropertyName("confidence")] double Confidence);

    private record EmbedResponse(
        [property: JsonPropertyName("embeddings")] float[][] Embeddings);
}
