using FinalProjectRina.Server.DAL;

namespace FinalProjectRina.Server.BL;

public interface IChatService
{
    Task<string> GenerateReplyAsync(string? message);
}

public class ChatService : IChatService
{
    private readonly IAiProvider _aiProvider;

    public ChatService(IAiProvider aiProvider)
    {
        _aiProvider = aiProvider;
    }

    public Task<string> GenerateReplyAsync(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message text is required", nameof(message));
        }

        return _aiProvider.GenerateChatReplyAsync(message.Trim());
    }
}
