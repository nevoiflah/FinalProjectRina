using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinalProjectRina.Server.DAL;

/// <summary>
/// Real AI Provider using OpenAI ChatGPT API
/// </summary>
public class OpenAiChatProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAiChatProvider(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiKey = configuration["OpenAI:ApiKey"] 
            ?? throw new InvalidOperationException("OpenAI API key not configured");
        _model = configuration["OpenAI:Model"] ?? "gpt-3.5-turbo";
        
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<string> GenerateChatReplyAsync(string prompt)
    {
        const string url = "https://api.openai.com/v1/chat/completions";

        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = "You are a helpful AI assistant. Provide clear, concise, and friendly responses." },
                new { role = "user", content = prompt }
            },
            temperature = 0.7,
            max_tokens = 500
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ChatCompletionResponse>(jsonResponse);

        return result?.Choices?[0]?.Message?.Content 
            ?? throw new InvalidOperationException("No response from AI");
    }

    private class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public Choice[]? Choices { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }

    private class Message
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}