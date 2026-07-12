namespace ArkaCallCenter.Core.Abstractions;

/// <summary>
/// دسترسی به سرویس‌های OpenAI (embeddings، chat، TTS). baseURL و apiKey از
/// تنظیمات سوپرادمین خوانده می‌شوند.
/// </summary>
public interface IOpenAiService
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);

    /// <summary>یک درخواست chat ساده؛ متن پاسخ را برمی‌گرداند.</summary>
    Task<string> ChatAsync(string systemPrompt, string userPrompt, bool jsonMode = false, CancellationToken ct = default);

    /// <summary>تبدیل متن به گفتار؛ بایت‌های صوتی (mp3) را برمی‌گرداند.</summary>
    Task<byte[]> TextToSpeechAsync(string text, string voice, CancellationToken ct = default);
}
