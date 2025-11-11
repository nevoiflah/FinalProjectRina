namespace FinalProjectRina.Server.DAL;

public record SpeechSynthesisResult(byte[] Buffer, string MimeType);

public interface ISpeechProvider
{
    Task<string> TranscribeAsync(Stream audioStream, string fileName);
    Task<SpeechSynthesisResult> SynthesizeAsync(string text);
}
