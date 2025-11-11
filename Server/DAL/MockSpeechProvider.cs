using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace FinalProjectRina.Server.DAL;

public class MockSpeechProvider : ISpeechProvider
{
    private const string DefaultMime = "audio/wav";

    public async Task<string> TranscribeAsync(Stream audioStream, string fileName)
    {
        await using var ms = new MemoryStream();
        await audioStream.CopyToAsync(ms);
        var buffer = ms.ToArray();

        using var sha1 = SHA1.Create();
        var digest = Convert.ToHexString(sha1.ComputeHash(buffer)).ToLowerInvariant()[..8];
        var readableSize = (buffer.Length / 1024d).ToString("F1");
        var label = string.IsNullOrWhiteSpace(fileName) ? "microphone-input" : fileName;

        return $"Mock transcript ({label}, {readableSize}KB, ref {digest})";
    }

    public Task<SpeechSynthesisResult> SynthesizeAsync(string text)
    {
        var freq = 480 + Math.Min(text.Length * 3, 160);
        var duration = 800 + Math.Min(text.Length * 12, 700);
        var wav = CreateToneWav(duration, freq);
        return Task.FromResult(new SpeechSynthesisResult(wav, DefaultMime));
    }

    private static byte[] CreateToneWav(int durationMs, int freqHz, int sampleRate = 22_050)
    {
        var totalSamples = (int)Math.Floor(durationMs / 1000d * sampleRate);
        const int headerSize = 44;
        const int bytesPerSample = 2;
        var dataSize = totalSamples * bytesPerSample;
        var buffer = new byte[headerSize + dataSize];

        WriteString(buffer, 0, "RIFF");
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4, 4), (uint)(36 + dataSize));
        WriteString(buffer, 8, "WAVE");
        WriteString(buffer, 12, "fmt ");
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(16, 4), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(20, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(22, 2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(24, 4), (uint)sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(28, 4), (uint)(sampleRate * bytesPerSample));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(32, 2), bytesPerSample);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(34, 2), 16);
        WriteString(buffer, 36, "data");
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(40, 4), (uint)dataSize);

        var offset = headerSize;
        for (var i = 0; i < totalSamples; i++)
        {
            var t = i / (double)sampleRate;
            var sample = Math.Sin(2 * Math.PI * freqHz * t);
            var clamped = Math.Clamp(sample, -1d, 1d);
            var s16 = (short)(clamped * short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(offset, 2), s16);
            offset += bytesPerSample;
        }

        return buffer;
    }

    private static void WriteString(byte[] buffer, int offset, string value)
    {
        Encoding.ASCII.GetBytes(value, 0, value.Length, buffer, offset);
    }
}
