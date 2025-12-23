using FinalProjectRina.Server.BL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace FinalProjectRina.Server.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IConfiguration _configuration;

    public AdminController(IUserService userService, IConfiguration configuration)
    {
        _userService = userService;
        _configuration = configuration;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetDetailedStats([FromQuery] string userId)
    {
        // 1. Verify Requesting User is Admin
        var admin = _userService.FindById(userId);
        if (admin == null || !admin.IsAdmin)
        {
            return Unauthorized(new { error = "Access denied. Admin rights required." });
        }

        // 2. Fetch All Users + Their Latest Chat Session
        // We do a LEFT JOIN to get the user DETAILS and their LATEST session data
        // For simplicity in SQL, we can use OUTER APPLY or correlated subqueries, or just fetch all chats and map in memory.
        // Given small scale, fetching all is fine. Let's do a tailored SQL query.

        var stats = new List<UserStatDto>();
        const string sql = @"
            SELECT 
                u.UserId, u.Name, u.Email, u.CreatedAt,
                s.InitialQuestion, s.FinalResult, s.StartedAt as ChatStartedAt
            FROM NLA_Users u
            OUTER APPLY (
                SELECT TOP 1 InitialQuestion, FinalResult, StartedAt
                FROM NLA_ChatSessions
                WHERE UserId = u.UserId
                ORDER BY StartedAt DESC
            ) s
            ORDER BY u.CreatedAt DESC";

        var connStr = _configuration.GetConnectionString("myProjDB");
        if (!connStr.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase)) connStr += ";TrustServerCertificate=True;Encrypt=True";

        using var conn = new SqlConnection(connStr);
        using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            stats.Add(new UserStatDto(
                Name: reader.GetString(reader.GetOrdinal("Name")),
                Email: reader.GetString(reader.GetOrdinal("Email")),
                JoinedAt: reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                FirstQuestion: reader.IsDBNull(reader.GetOrdinal("InitialQuestion")) ? "No chat yet" : reader.GetString(reader.GetOrdinal("InitialQuestion")),
                LastResult: reader.IsDBNull(reader.GetOrdinal("FinalResult")) ? "-" : reader.GetString(reader.GetOrdinal("FinalResult"))
            ));
        }

        return Ok(stats);
    }
}

public record UserStatDto(string Name, string Email, DateTime JoinedAt, string FirstQuestion, string LastResult);
