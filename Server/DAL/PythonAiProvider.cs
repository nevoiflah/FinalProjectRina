using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinalProjectRina.Server.DAL;

/// <summary>
/// AI Provider that delegates to the Python Microservice
/// Satisfies the "Python for AI models" requirement
/// </summary>
public class PythonAiProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _pythonServiceUrl;

    public PythonAiProvider(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
        // Default to localhost:5000 if not configured
        _pythonServiceUrl = configuration["PythonService:Url"] ?? "http://localhost:5000";
    }

    public async Task<string> GenerateChatReplyAsync(
        string prompt,
        List<string>? context = null,
        List<ChatTurn>? history = null,
        string? language = null)
    {
        var url = $"{_pythonServiceUrl}/chat";

        var requestBody = new
        {
            message = prompt,
            // gpt-4o-mini follows the structured advisor prompt far better than gpt-3.5-turbo
            // and has much stronger multi-turn memory in Hebrew/Arabic, at a low cost.
            model = "gpt-4o-mini",
            context = context ?? new List<string>(),
            history = (history ?? new List<ChatTurn>())
                .Select(t => new { role = t.Role, content = t.Content })
                .ToList(),
            language = language ?? string.Empty
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<PythonResponse>(jsonResponse);

            return result?.Reply ?? throw new InvalidOperationException("No reply from Python service");
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Failed to connect to Python AI Service at {_pythonServiceUrl}. Make sure 'python app.py' is running.", ex);
        }
    }

    private class PythonResponse
    {
        [JsonPropertyName("reply")]
        public string? Reply { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
