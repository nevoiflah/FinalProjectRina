namespace FinalProjectRina.Server.DAL;

/// <summary>One prior conversation turn. Role is "user" or "assistant".</summary>
public record ChatTurn(string Role, string Content);

public interface IAiProvider
{
    Task<string> GenerateChatReplyAsync(
        string prompt,
        List<string>? context = null,
        List<ChatTurn>? history = null,
        string? language = null);
}
