using ArkaCallCenter.Core.Common;

namespace ArkaCallCenter.Core.Entities;

/// <summary>
/// ثبت هر بار مصرف توکن از سرویس‌های OpenAI، برای گزارش‌گیری به تفکیک
/// «کلید API» و «کاربر/شماره موبایل» در پنل سوپرادمین.
/// </summary>
public class TokenUsage : BaseEntity
{
    /// <summary>اثرانگشت کلید API استفاده‌شده (ماسک‌شده، بدون افشای کلید کامل).</summary>
    public string ApiKeyFingerprint { get; set; } = "unknown";

    /// <summary>کاربر مرتبط (در صورت وجود). null برای مصرف سیستمی.</summary>
    public int? UserId { get; set; }

    /// <summary>شماره موبایل کاربر (denormalized برای گزارش per-mobile).</summary>
    public string? PhoneNumber { get; set; }

    public string Model { get; set; } = "";

    /// <summary>نوع عملیات: Embedding | Chat | Realtime | Tts.</summary>
    public string Operation { get; set; } = "";

    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}
