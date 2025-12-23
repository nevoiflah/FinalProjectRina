using FinalProjectRina.Server.DAL;

namespace FinalProjectRina.Server.BL;

public interface IChatService
{
    Task<string> GenerateReplyAsync(string? message, string? userId);
}

public class ChatService : IChatService
{
    private readonly IAiProvider _aiProvider;
    private readonly IConfiguration _configuration;

    public ChatService(IAiProvider aiProvider, IConfiguration configuration)
    {
        _aiProvider = aiProvider;
        _configuration = configuration;
    }

    public async Task<string> GenerateReplyAsync(string? message, string? userId)
    {
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Message is required", nameof(message));
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required", nameof(userId));

        // 1. Retrieve Context (RAG)
        var context = await RetrieveContextAsync(message.Trim());

        // 2. Generate Reply
        var reply = await _aiProvider.GenerateChatReplyAsync(message.Trim(), context);

        // 2. Persist Session (Upsert logic: Create if new context, or just log simplified "First Question -> Final Result" logic)
        // Per user request: "what question initialized their chat and on what response they ended it"
        // We will treat every message as potentially updating the "Final Result" of the LATEST open session, 
        // OR create a new session if none exists recently. For simplicity, we'll store every interaction as an update to the user's "Current" session or create one.
        // Let's implement a simple logic: Check for usage in last 10 minutes?
        // Actually, user just wants "what question initialized their chat". 
        // We will try to find an open session (EndedAt IS NULL). If found, update FinalResult. If not, create new.
        
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

            const string sql = @"
                SELECT TOP 100 InitialQuestion, FinalResult 
                FROM NLA_ChatSessions 
                WHERE FinalResult IS NOT NULL 
                ORDER BY StartedAt DESC";

            var candidates = new List<(string Question, string Answer)>();

            var connStr = _configuration.GetConnectionString("myProjDB");
            if (!connStr.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase)) 
                connStr += ";TrustServerCertificate=True;Encrypt=True";

            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr))
            {
                await conn.OpenAsync();
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    candidates.Add((
                        reader.GetString(0),
                        reader.GetString(1)
                    ));
                }
            }

            // Simple In-Memory Similarity (Word Overlap)
            var bestMatches = candidates
                .Select(c => new 
                { 
                    c.Answer, 
                    Score = keywords.Count(k => c.Question.ToLower().Contains(k)) 
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(2) // Top 2 context items
                .Select(x => x.Answer)
                .ToList();

            return bestMatches;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RAG Retrieval Failed: {ex.Message}");
            return new List<string>();
        }
    }

    private async Task LogChatInteraction(string userId, string userMessage, string botReply)
    {
        const string connName = "myProjDB"; 
        // Note: For production, inject ConnectionString proper, reusing logic from UserService or a DbContext is better.
        // Duplicating connection string logic for speed as requested.
        var connStr = _configuration.GetConnectionString(connName);
        if (!connStr.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase)) connStr += ";TrustServerCertificate=True;Encrypt=True";

        using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
        await conn.OpenAsync();

        // Check for active session (e.g. created in last 30 mins)
        const string checkSql = @"
            SELECT TOP 1 SessionId FROM NLA_ChatSessions 
            WHERE UserId = @UserId AND EndedAt IS NULL AND StartedAt > DATEADD(minute, -30, GETUTCDATE())
            ORDER BY StartedAt DESC";
        
        using var checkCmd = new Microsoft.Data.SqlClient.SqlCommand(checkSql, conn);
        checkCmd.Parameters.AddWithValue("@UserId", userId);
        var sessionId = await checkCmd.ExecuteScalarAsync();

        if (sessionId != null)
        {
            // Update existing
            const string updateSql = @"
                UPDATE NLA_ChatSessions 
                SET FinalResult = @Reply, EndedAt = NULL -- Keep it open, or update timestamp
                WHERE SessionId = @SessionId";
            using var updateCmd = new Microsoft.Data.SqlClient.SqlCommand(updateSql, conn);
            updateCmd.Parameters.AddWithValue("@Reply", botReply);
            updateCmd.Parameters.AddWithValue("@SessionId", sessionId);
            await updateCmd.ExecuteNonQueryAsync();
        }
        else
        {
            // Create new
            const string insertSql = @"
                INSERT INTO NLA_ChatSessions (UserId, InitialQuestion, FinalResult, StartedAt)
                VALUES (@UserId, @Msg, @Reply, GETUTCDATE())";
            using var insertCmd = new Microsoft.Data.SqlClient.SqlCommand(insertSql, conn);
            insertCmd.Parameters.AddWithValue("@UserId", userId);
            insertCmd.Parameters.AddWithValue("@Msg", userMessage);
            insertCmd.Parameters.AddWithValue("@Reply", botReply);
            await insertCmd.ExecuteNonQueryAsync();
        }
    }
}
