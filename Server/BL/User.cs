using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace FinalProjectRina.Server.BL;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("organization")]
    public string? Organization { get; set; }

    [JsonIgnore]
    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("isAdmin")]
    public bool IsAdmin { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("lastLoginAt")]
    public DateTime? LastLoginAt { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;
}

public interface IUserService
{
    IEnumerable<User> GetUsers();
    User Register(string? name, string? email, string? organization, string? password);
    User? Authenticate(string? email, string? password);
    User? FindByEmail(string? email);
    User? FindById(string? userId);
    User? UpdateProfile(string? userId, string? name, string? organization, string? password);
    bool DeleteUser(string? userId);
    bool ToggleAdmin(string? userId, bool isAdmin);
    bool ToggleStatus(string? userId, bool isActive);
}

public class UserService : IUserService
{
    private readonly IMongoCollection<User> _users;

    public UserService(IMongoDatabase database)
    {
        _users = database.GetCollection<User>("users");
        _users.Indexes.CreateOne(new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(u => u.Email),
            new CreateIndexOptions { Unique = true }));
    }

    public IEnumerable<User> GetUsers() =>
        _users.Find(Builders<User>.Filter.Empty).SortBy(u => u.CreatedAt).ToList();

    public User? FindByEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var normalized = NormalizeEmail(email);
        return _users.Find(u => u.Email == normalized).FirstOrDefault();
    }

    public User? FindById(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        var id = userId.Trim();
        return _users.Find(u => u.UserId == id).FirstOrDefault();
    }

    public User Register(string? name, string? email, string? organization, string? password)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            throw new ArgumentException("Password must be at least 6 characters", nameof(password));

        var user = new User
        {
            UserId = Guid.NewGuid().ToString("N"),
            Name = name.Trim(),
            Email = NormalizeEmail(email),
            Organization = string.IsNullOrWhiteSpace(organization) ? null : organization.Trim(),
            PasswordHash = HashPassword(password),
            IsAdmin = false,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        try
        {
            _users.InsertOne(user);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new InvalidOperationException("User with this email already exists", ex);
        }
        return user;
    }

    public User? Authenticate(string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return null;

        var user = FindByEmail(email);
        if (user is null || !user.IsActive || !VerifyPassword(password, user.PasswordHash)) return null;

        var now = DateTime.UtcNow;
        _users.UpdateOne(u => u.UserId == user.UserId, Builders<User>.Update.Set(u => u.LastLoginAt, now));
        user.LastLoginAt = now;
        return user;
    }

    public User? UpdateProfile(string? userId, string? name, string? organization, string? password)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        var user = FindById(userId);
        if (user is null) return null;

        var updates = new List<UpdateDefinition<User>>();

        if (!string.IsNullOrWhiteSpace(name))
        {
            user.Name = name.Trim();
            updates.Add(Builders<User>.Update.Set(u => u.Name, user.Name));
        }

        user.Organization = string.IsNullOrWhiteSpace(organization) ? null : organization.Trim();
        updates.Add(Builders<User>.Update.Set(u => u.Organization, user.Organization));

        if (!string.IsNullOrWhiteSpace(password))
        {
            if (password.Length < 6)
                throw new ArgumentException("Password must be at least 6 characters", nameof(password));
            user.PasswordHash = HashPassword(password);
            updates.Add(Builders<User>.Update.Set(u => u.PasswordHash, user.PasswordHash));
        }

        if (updates.Count > 0)
            _users.UpdateOne(u => u.UserId == user.UserId, Builders<User>.Update.Combine(updates));

        return user;
    }

    public bool DeleteUser(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;
        var result = _users.DeleteOne(u => u.UserId == userId.Trim());
        return result.DeletedCount > 0;
    }

    public bool ToggleAdmin(string? userId, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;
        var result = _users.UpdateOne(
            u => u.UserId == userId.Trim(),
            Builders<User>.Update.Set(u => u.IsAdmin, isAdmin));
        return result.ModifiedCount > 0;
    }

    public bool ToggleStatus(string? userId, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;
        var result = _users.UpdateOne(
            u => u.UserId == userId.Trim(),
            Builders<User>.Update.Set(u => u.IsActive, isActive));
        return result.ModifiedCount > 0;
    }

    public void PromoteUserToAdmin(string email)
    {
        var user = FindByEmail(email);
        if (user != null && !user.IsAdmin)
            ToggleAdmin(user.UserId, true);
    }

    private static string NormalizeEmail(string email)
    {
        try
        {
            return new MailAddress(email.Trim()).Address.ToLowerInvariant();
        }
        catch (FormatException)
        {
            throw new ArgumentException("Email format is invalid", nameof(email));
        }
    }

    private static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(password)));
    }

    private static bool VerifyPassword(string password, string storedHash) =>
        HashPassword(password) == storedHash;
}
