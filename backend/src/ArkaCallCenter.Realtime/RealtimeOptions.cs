namespace ArkaCallCenter.Realtime;

/// <summary>تنظیمات worker پل صوتی.</summary>
public class RealtimeOptions
{
    /// <summary>پورت TCP که سرور AudioSocket روی آن گوش می‌دهد (Asterisk به آن وصل می‌شود).</summary>
    public int AudioSocketPort { get; set; } = 9092;

    /// <summary>آدرس bind سرور AudioSocket.</summary>
    public string AudioSocketHost { get; set; } = "0.0.0.0";

    /// <summary>
    /// تماس بعد از این تعداد ثانیه سکوت کامل بسته می‌شود. صفر یا مقدار منفی، قطع خودکار را غیرفعال می‌کند.
    /// </summary>
    public int IdleTimeoutSeconds { get; set; } = 60;

    /// <summary>Speech-to-text model used for caller audio inside realtime sessions.</summary>
    public string TranscriptionModel { get; set; } = "gpt-4o-transcribe";

    /// <summary>BCP-47/ISO language hint for caller transcription.</summary>
    public string TranscriptionLanguage { get; set; } = "fa";

    /// <summary>Domain vocabulary hint supplied to the transcription model.</summary>
    public string TranscriptionPrompt { get; set; }
        = "گفت‌وگوی تلفنی فارسی درباره خدمات شرکت، بیمه، خودرو و پرسش‌های مشتری";
}
