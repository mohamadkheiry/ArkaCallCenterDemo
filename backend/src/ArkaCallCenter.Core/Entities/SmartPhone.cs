using ArkaCallCenter.Core.Common;
using ArkaCallCenter.Core.Enums;

namespace ArkaCallCenter.Core.Entities;

public class SmartPhone : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = default!;

    /// <summary>
    /// داخلی تخصیص‌یافته روی ایزابل، در بازه‌ی ۱۰۰۰–۹۹۹۹ (یکتا).
    /// null یعنی هنوز تخصیص نیافته (تلفن در حال آماده‌سازی است و فقط پیام خوش‌آمد ثبت شده).
    /// </summary>
    public int? Extension { get; set; }

    /// <summary>رمز SIP داخلی (رمزنگاری‌شده در DB).</summary>
    public string? SipSecret { get; set; }

    public string? WelcomeMessageText { get; set; }

    /// <summary>مسیر فایل صوتی خوش‌آمد از پیش‌ساخته (TTS).</summary>
    public string? WelcomeAudioPath { get; set; }

    public SmartPhoneStatus Status { get; set; } = SmartPhoneStatus.Provisioning;

    /// <summary>
    /// میزانِ پایبندیِ پاسخ‌های AI به پایگاه دانش، ۱۰ تا ۱۰۰ درصد. هرچه بالاتر،
    /// پاسخ‌ها دقیق‌تر و نزدیک‌تر به پایگاه دانش (خلاقیتِ کمتر). به temperature نگاشت
    /// می‌شود: درصدِ بالاتر = temperature پایین‌تر.
    /// </summary>
    public int AnswerAccuracyPercent { get; set; } = 70;

    public ICollection<CallSession> CallSessions { get; set; } = new List<CallSession>();
}
