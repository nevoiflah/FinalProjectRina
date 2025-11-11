using FinalProjectRina.Server.DAL;
using Microsoft.AspNetCore.Http;

namespace FinalProjectRina.Server.BL;

public interface ISpeechService
{
    Task<string> TranscribeAsync(IFormFile? audioFile);
    Task<SpeechSynthesisResult> SynthesizeAsync(string? text);
}

public class SpeechService : ISpeechService
{
    private readonly ISpeechProvider _speechProvider;

    public SpeechService(ISpeechProvider speechProvider)
    {
        _speechProvider = speechProvider;
    }

    public async Task<string> TranscribeAsync(IFormFile? audioFile)
    {
        if (audioFile is null || audioFile.Length == 0)
        {
            throw new ArgumentException("Audio payload is required", nameof(audioFile));
        }

        await using var stream = audioFile.OpenReadStream();
        return await _speechProvider.TranscribeAsync(stream, audioFile.FileName);
    }

    public Task<SpeechSynthesisResult> SynthesizeAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text is required", nameof(text));
        }

        return _speechProvider.SynthesizeAsync(text.Trim());
    }
}
