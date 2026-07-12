using ArkaCallCenter.Core.Common;
using ArkaCallCenter.Core.Enums;

namespace ArkaCallCenter.Core.Entities;

/// <summary>
/// تنظیمات سراسری key/value که سوپرادمین ویرایش می‌کند
/// (baseURL/apiKey اوپن‌ای‌آی، تنظیمات SMS.ir، گوینده‌ی پیش‌فرض، محدودیت پیش‌فرض،
/// آستانه‌ی RAG، متن/مسیر وویسِ پیام fallback و ...).
/// مقادیر حساس رمزنگاری‌شده ذخیره می‌شوند.
/// </summary>
public class AppSetting : BaseEntity
{
    public string Key { get; set; } = default!;
    public string? Value { get; set; }
    public bool IsSecret { get; set; }
    public string? Description { get; set; }
}

/// <summary>قالب متن پیامک برای هر رویداد. متغیرها با {placeholder} پشتیبانی می‌شوند.</summary>
public class SmsTemplate : BaseEntity
{
    public SmsEventType EventType { get; set; }
    public string Body { get; set; } = default!;
    public bool Enabled { get; set; } = true;
}

/// <summary>گیرندگان پیامک برای هر رویداد.</summary>
public class SmsEventRecipient : BaseEntity
{
    public SmsEventType EventType { get; set; }

    /// <summary>اگر true، پیامک به شماره‌ی خودِ کاربرِ مرتبط با رویداد ارسال می‌شود.</summary>
    public bool UseUserOwnNumber { get; set; }

    /// <summary>شماره‌ی ثابت گیرنده (وقتی UseUserOwnNumber=false).</summary>
    public string? PhoneNumber { get; set; }
}

/// <summary>گوینده‌های مجاز برای انتخاب کاربر و پیش‌فرض سوپرادمین.</summary>
public class VoiceOption : BaseEntity
{
    public string Name { get; set; } = default!;          // شناسه‌ی فنی (مثلاً alloy)
    public string DisplayName { get; set; } = default!;    // نام نمایشی فارسی
    public string Provider { get; set; } = "openai";
    public bool IsDefault { get; set; }
    public bool Enabled { get; set; } = true;

    /// <summary>مسیر فایل نمونه‌صدای این گوینده (mp3) برای پیش‌نمایش کاربر.</summary>
    public string? SampleAudioPath { get; set; }
}
