using ArkaCallCenter.Core.Common;

namespace ArkaCallCenter.Core.Entities;

/// <summary>لاگ یک تماس ورودی و نحوه‌ی پاسخ‌گویی هوش مصنوعی.</summary>
public class CallSession : BaseEntity
{
    public int SmartPhoneId { get; set; }
    public SmartPhone SmartPhone { get; set; } = default!;

    public string? CallerId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public int DurationSeconds { get; set; }

    /// <summary>آیا پاسخ از پایگاه دانش داده شد (true) یا پیام fallback پلی شد (false).</summary>
    public bool AnsweredFromKb { get; set; }

    /// <summary>رونوشت گفتگو به‌صورت JSON (نوبت‌های caller/assistant).</summary>
    public string? TranscriptJson { get; set; }

    /// <summary>مسیر فایل ضبط‌شده‌ی مکالمه (WAV ۸kHz) برای پخش در پنل.</summary>
    public string? RecordingPath { get; set; }
}
