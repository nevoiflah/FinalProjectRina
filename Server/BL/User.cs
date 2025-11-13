using System.Data;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace FinalProjectRina.Server.BL;

public class User
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Organization { get; set; }

    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public interface IUserService
{
    IEnumerable<User> GetUsers();
    User Register(string? name, string? email, string? organization, string? password);
    User? Authenticate(string? email, string? password);
    User? FindByEmail(string? email);
    User? FindById(string? userId);
}

public class UserService : IUserService
{
    private readonly string _connectionString;
    private static bool _schemaEnsured;
    private static readonly object SchemaLock = new();

    public UserService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("myProjDB")
            ?? throw new InvalidOperationException("Connection string 'myProjDB' was not found.");
        EnsureSchema();
    }

    public IEnumerable<User> GetUsers()
    {
        const string sql = """
            SELECT UserId, Name, Email, Organization, PasswordHash, IsAdmin, CreatedAt, LastLoginAt, IsActive
            FROM dbo.NLA_Users
            ORDER BY CreatedAt
            """;

        using var conn = CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        conn.Open();

        using var reader = cmd.ExecuteReader();
        var users = new List<User>();
        while (reader.Read())
        {
            users.Add(MapUser(reader));
        }

        return users;
    }

    public User? FindByEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return GetUserByEmail(NormalizeEmail(email));
    }

    public User? FindById(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return GetUserById(userId.Trim());
    }

    public User Register(string? name, string? email, string? organization, string? password)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            throw new ArgumentException("Password must be at least 6 characters", nameof(password));
        }

        var normalizedEmail = NormalizeEmail(email);
        var user = new User
        {
            UserId = Guid.NewGuid().ToString("N"),
            Name = name.Trim(),
            Email = normalizedEmail,
            Organization = string.IsNullOrWhiteSpace(organization) ? null : organization.Trim(),
            PasswordHash = HashPassword(password),
            IsAdmin = false,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = null,
            IsActive = true
        };

        PersistUser(user);
        return user;
    }

    public User? Authenticate(string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var stored = GetUserByEmail(NormalizeEmail(email));
        if (stored is null || !stored.IsActive || !VerifyPassword(password, stored.PasswordHash))
        {
            return null;
        }

        stored.LastLoginAt = UpdateLastLogin(stored.UserId);
        return stored;
    }

    private static string NormalizeEmail(string email)
    {
        try
        {
            var address = new MailAddress(email.Trim());
            return address.Address.ToLowerInvariant();
        }
        catch (FormatException)
        {
            throw new ArgumentException("Email format is invalid", nameof(email));
        }
    }

    private static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    private static bool VerifyPassword(string password, string storedHash) =>
        HashPassword(password) == storedHash;

    private SqlConnection CreateConnection() => new(_connectionString);

    private void EnsureSchema()
    {
        if (_schemaEnsured)
        {
            return;
        }

        lock (SchemaLock)
        {
            if (_schemaEnsured)
            {
                return;
            }

            const string sql = """
                IF OBJECT_ID('dbo.NLA_Users', 'U') IS NOT NULL
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM sys.columns
                        WHERE object_id = OBJECT_ID('dbo.NLA_Users')
                          AND name = 'Organization'
                          AND is_nullable = 0
                    )
                    BEGIN
                        ALTER TABLE dbo.NLA_Users ALTER COLUMN Organization NVARCHAR(200) NULL;
                    END
                END
                """;

            try
            {
                using var conn = CreateConnection();
                using var cmd = new SqlCommand(sql, conn);
                conn.Open();
                cmd.ExecuteNonQuery();
                _schemaEnsured = true;
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Failed to ensure the NLA_Users schema is up to date.", ex);
            }
        }
    }

    private void PersistUser(User user)
    {
        const string sql = """
            INSERT INTO dbo.NLA_Users
                (UserId, Name, Email, Organization, PasswordHash, IsAdmin, CreatedAt, LastLoginAt, IsActive)
            VALUES
                (@UserId, @Name, @Email, @Organization, @PasswordHash, @IsAdmin, @CreatedAt, @LastLoginAt, @IsActive)
            """;

        using var conn = CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", user.UserId);
        cmd.Parameters.AddWithValue("@Name", user.Name);
        cmd.Parameters.AddWithValue("@Email", user.Email);
        cmd.Parameters.Add("@Organization", SqlDbType.NVarChar, 200).Value = (object?)user.Organization ?? DBNull.Value;
        cmd.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
        cmd.Parameters.AddWithValue("@IsAdmin", user.IsAdmin);
        cmd.Parameters.AddWithValue("@CreatedAt", user.CreatedAt);
        cmd.Parameters.Add("@LastLoginAt", SqlDbType.DateTime2).Value = (object?)user.LastLoginAt ?? DBNull.Value;
        cmd.Parameters.AddWithValue("@IsActive", user.IsActive);

        conn.Open();
        try
        {
            cmd.ExecuteNonQuery();
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            throw new InvalidOperationException("User with this email already exists", ex);
        }
    }

    private User? GetUserByEmail(string normalizedEmail)
    {
        const string sql = """
            SELECT TOP 1 UserId, Name, Email, Organization, PasswordHash, IsAdmin, CreatedAt, LastLoginAt, IsActive
            FROM dbo.NLA_Users
            WHERE Email = @Email
            """;

        using var conn = CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Email", normalizedEmail);
        conn.Open();

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapUser(reader) : null;
    }

    private User? GetUserById(string userId)
    {
        const string sql = """
            SELECT TOP 1 UserId, Name, Email, Organization, PasswordHash, IsAdmin, CreatedAt, LastLoginAt, IsActive
            FROM dbo.NLA_Users
            WHERE UserId = @UserId
            """;

        using var conn = CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        conn.Open();

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapUser(reader) : null;
    }

    private static User MapUser(SqlDataReader reader)
    {
        return new User
        {
            UserId = reader.GetString(reader.GetOrdinal("UserId")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            Organization = reader.IsDBNull(reader.GetOrdinal("Organization"))
                ? null
                : reader.GetString(reader.GetOrdinal("Organization")),
            PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
            IsAdmin = reader.GetBoolean(reader.GetOrdinal("IsAdmin")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            LastLoginAt = reader.IsDBNull(reader.GetOrdinal("LastLoginAt"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("LastLoginAt")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
        };
    }

    private DateTime? UpdateLastLogin(string userId)
    {
        const string sql = """
            UPDATE dbo.NLA_Users
            SET LastLoginAt = SYSUTCDATETIME()
            OUTPUT inserted.LastLoginAt
            WHERE UserId = @UserId
            """;

        using var conn = CreateConnection();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        conn.Open();

        var result = cmd.ExecuteScalar();
        return result is DateTime dt ? dt : null;
    }
}
