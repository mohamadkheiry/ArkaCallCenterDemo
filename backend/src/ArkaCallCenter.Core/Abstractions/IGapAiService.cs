namespace ArkaCallCenter.Core.Abstractions;

/// <summary>مسیرهای بیرونی صوت تماس: Whisper، بازسازی متن و TTS کش‌شونده.</summary>
public interface IGapAiService
{
    Task<string> TranscribeAsync(byte[] wav8k, CancellationToken ct = default);
    Task<string> CleanTranscriptAsync(string transcript, CancellationToken ct = default);
    Task<byte[]> GenerateSpeechWav8kAsync(string text, string? voice = null, CancellationToken ct = default);
    Task<int?> SelectMatchingQuestionAsync(
        string cleanedQuestion,
        IReadOnlyList<GapQuestionCandidate> candidates,
        CancellationToken ct = default);
}

public record GapQuestionCandidate(int Id, string Question);
