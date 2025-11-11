namespace FinalProjectRina.Server.DAL;

public class MockAiProvider : IAiProvider
{
    private static readonly string[] Replies =
    [
        "That's an interesting point! Here's a quick thought:",
        "Thanks for sharing that. Here is what I can offer:",
        "Good question — here is a concise answer:",
        "Let me walk you through it:",
        "Here is something to consider:"
    ];

    private readonly Random _random = new();

    public Task<string> GenerateChatReplyAsync(string prompt)
    {
        var prefix = Replies[_random.Next(Replies.Length)];
        return Task.FromResult($"{prefix} {prompt}");
    }
}
