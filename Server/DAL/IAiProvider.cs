namespace FinalProjectRina.Server.DAL;

public interface IAiProvider
{
    Task<string> GenerateChatReplyAsync(string prompt);
}
