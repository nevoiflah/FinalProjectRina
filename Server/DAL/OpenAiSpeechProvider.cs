using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FinalProjectRina.Server.DAL;

/// <summary>
/// Real Speech Provider using OpenAI Whisper (STT) and TTS
/// Requires: OpenAI API Key
/// </summary>
public class OpenAiSpeechProvider : ISpeechProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public OpenAiSpeechProvider(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiKey = configuration["OpenAI:ApiKey"] 
            ?? throw new InvalidOperationException("OpenAI API key not configured");
        
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<string> TranscribeAsync(Stream audioStream, string fileName)
    {
        // OpenAI Whisper API endpoint
        const string url = "https://api.openai.com/v1/audio/transcriptions";

        using var content = new MultipartFormDataContent();
        
        // Copy stream to memory
        await using var memoryStream = new MemoryStream();
        await audioStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        Console.WriteLine($"[STT] Audio size: {memoryStream.Length} bytes");

        // Add audio file
        var audioContent = new StreamContent(memoryStream);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
        content.Add(audioContent, "file", fileName ?? "audio.webm");

        // Add model parameter
        content.Add(new StringContent("whisper-1"), "model");

        Console.WriteLine($"[STT] Sending request to OpenAI Whisper API");
        
        var response = await _httpClient.PostAsync(url, content);
        
        Console.WriteLine($"[STT] Response status: {response.StatusCode}");
        
        var jsonResponse = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[STT] Response content: {jsonResponse}");
        
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI API error: {jsonResponse}");
        }

        var result = JsonSerializer.Deserialize<WhisperResponse>(jsonResponse);
        
        Console.WriteLine($"[STT] Deserialized text: '{result?.Text}'");

        if (string.IsNullOrWhiteSpace(result?.Text))
        {
            Console.WriteLine($"[STT] Warning: Empty transcript received");
            return string.Empty;
        }

        return result.Text;
    }

    public async Task<SpeechSynthesisResult> SynthesizeAsync(string text)
    {
        // OpenAI TTS API endpoint
        const string url = "https://api.openai.com/v1/audio/speech";

        var requestBody = new
        {
            model = "tts-1",
            input = text,
            voice = "nova" // Options: alloy, echo, fable, onyx, nova, shimmer
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var audioData = await response.Content.ReadAsByteArrayAsync();
        return new SpeechSynthesisResult(audioData, "audio/mpeg");
    }

    private class WhisperResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }
}