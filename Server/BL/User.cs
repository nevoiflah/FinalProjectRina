using System.Collections.Concurrent;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace FinalProjectRina.Server.BL;

public record User(string Id, string Name, string Email, string Organization, DateTime CreatedAt);

public interface IUserService
{
    IEnumerable<User> GetUsers();
    User Register(string? name, string? email, string? organization, string? password);
    User? Authenticate(string? email, string? password);
    User? FindByEmail(string? email);
}

public class UserService : IUserService
{
    private readonly ConcurrentDictionary<string, StoredUser> _users = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<User> GetUsers() => _users.Values
        .Select(u => u.Profile)
        .OrderBy(u => u.CreatedAt);

    public User? FindByEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        _users.TryGetValue(NormalizeEmail(email), out var stored);
        return stored?.Profile;
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

        if (string.IsNullOrWhiteSpace(organization))
        {
            throw new ArgumentException("Organization is required", nameof(organization));
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            throw new ArgumentException("Password must be at least 6 characters", nameof(password));
        }

        var normalizedEmail = NormalizeEmail(email);
        var user = new User(Guid.NewGuid().ToString("N"), name.Trim(), normalizedEmail, organization.Trim(), DateTime.UtcNow);
        var entry = new StoredUser(user, HashPassword(password));

        if (!_users.TryAdd(normalizedEmail, entry))
        {
            throw new InvalidOperationException("User with this email already exists");
        }

        return user;
    }

    public User? Authenticate(string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        if (!_users.TryGetValue(NormalizeEmail(email), out var stored))
        {
            return null;
        }

        return VerifyPassword(password, stored.PasswordHash) ? stored.Profile : null;
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
        return Convert.ToBase64String(bytes);
    }

    private static bool VerifyPassword(string password, string storedHash) =>
        HashPassword(password) == storedHash;

    private record StoredUser(User Profile, string PasswordHash);
}
