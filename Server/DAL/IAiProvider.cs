namespace FinalProjectRina.Server.DAL;

public interface IAiProvider
{
    Task<string> GenerateChatReplyAsync(string prompt, List<string>? context = null);
}
