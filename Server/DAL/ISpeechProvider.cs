namespace FinalProjectRina.Server.DAL;

public record SpeechSynthesisResult(byte[] Buffer, string MimeType);

public interface ISpeechProvider
{
    Task<string> TranscribeAsync(Stream audioStream, string fileName, string language = "he");
    Task<SpeechSynthesisResult> SynthesizeAsync(string text, string? voice = null, double speed = 1.0);
}
