namespace FinalProjectRina.Server.DAL;

/// <summary>
/// A single turn of the conversation, used to give the AI provider memory of
/// what was already said. Role is "user" or "assistant".
/// </summary>
public record ChatTurn(string Role, string Content);

public interface IAiProvider
{
    Task<string> GenerateChatReplyAsync(
        string prompt,
        List<string>? context = null,
        List<ChatTurn>? history = null,
        string? language = null);
}
