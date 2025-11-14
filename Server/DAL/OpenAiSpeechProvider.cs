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

        // Add audio file
        var audioContent = new StreamContent(memoryStream);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
        content.Add(audioContent, "file", fileName ?? "audio.webm");

        // Add model parameter
        content.Add(new StringContent("whisper-1"), "model");

        var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<WhisperResponse>(jsonResponse);

        return result?.Text ?? throw new InvalidOperationException("No transcription returned");
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
        public string Text { get; set; } = string.Empty;
    }
}