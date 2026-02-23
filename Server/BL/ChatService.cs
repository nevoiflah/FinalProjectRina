using FinalProjectRina.Server.DAL;

namespace FinalProjectRina.Server.BL;

public interface IChatService
{
    Task<string> GenerateReplyAsync(string? message, string? userId);
    Task EndSessionAsync(string userId);
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

        // 3. Log Interaction
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

            var candidates = new List<(string Question, string Answer)>();
            var facts = new List<(string Category, string Fact)>();

            var connStr = _configuration.GetConnectionString("myProjDB");
            if (!connStr.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase)) 
                connStr += ";TrustServerCertificate=True;Encrypt=True";

            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr))
            {
                await conn.OpenAsync();

                // 1. Get History Context
                const string sqlHistory = @"
                    SELECT TOP 100 InitialQuestion, FinalResult 
                    FROM NLA_ChatSessions 
                    WHERE FinalResult IS NOT NULL 
                    ORDER BY StartedAt DESC";
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(sqlHistory, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        candidates.Add((reader.GetString(0), reader.GetString(1)));
                    }
                }

                // 2. Get Factual Knowledge Context
                // Wrapped in try/catch mapping so the app doesn't crash if the user hasn't created the table yet
                try 
                {
                    const string sqlFacts = @"SELECT Category, FactText FROM NLA_RuppinKnowledge";
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(sqlFacts, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            facts.Add((reader.GetString(0), reader.GetString(1)));
                        }
                    }
                } 
                catch (Microsoft.Data.SqlClient.SqlException) { /* Table doesn't exist yet, ignore */ }
            }

            // Simple In-Memory Similarity for History
            var bestHistoryMatches = candidates
                .Select(c => new 
                { 
                    c.Answer, 
                    Score = keywords.Count(k => c.Question.ToLower().Contains(k)) 
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(2) // Top 2 conversational history
                .Select(x => x.Answer)
                .ToList();

            // Simple In-Memory Similarity for Facts
            var bestFactMatches = facts
                .Select(f => new 
                { 
                    f.Fact, 
                    Score = keywords.Count(k => f.Category.ToLower().Contains(k) || f.Fact.ToLower().Contains(k)) 
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(3) // Top 3 concrete facts
                .Select(x => "Ruppin Fact: " + x.Fact)
                .ToList();

            // Blend Both
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
        const string connName = "myProjDB"; 
        var connStr = _configuration.GetConnectionString(connName);
        if (!connStr.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase)) connStr += ";TrustServerCertificate=True;Encrypt=True";

        using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
        await conn.OpenAsync();

        // Check for active session NOT ended
        const string checkSql = @"
            SELECT TOP 1 SessionId FROM NLA_ChatSessions 
            WHERE UserId = @UserId AND EndedAt IS NULL
            ORDER BY StartedAt DESC";
        
        using var checkCmd = new Microsoft.Data.SqlClient.SqlCommand(checkSql, conn);
        checkCmd.Parameters.AddWithValue("@UserId", userId);
        var sessionId = await checkCmd.ExecuteScalarAsync();

        if (sessionId != null)
        {
            // Update existing active session
            const string updateSql = @"
                UPDATE NLA_ChatSessions 
                SET FinalResult = @Reply
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

    public async Task EndSessionAsync(string userId)
    {
        const string connName = "myProjDB"; 
        var connStr = _configuration.GetConnectionString(connName);
        if (!connStr.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase)) connStr += ";TrustServerCertificate=True;Encrypt=True";

        using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
        await conn.OpenAsync();

        // Mark all open sessions for this user as ended
        const string sql = "UPDATE NLA_ChatSessions SET EndedAt = GETUTCDATE() WHERE UserId = @UserId AND EndedAt IS NULL";
        using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await cmd.ExecuteNonQueryAsync();
    }
}
