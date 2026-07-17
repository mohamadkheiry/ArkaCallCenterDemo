namespace ArkaCallCenter.Core.Enums;

public enum UserRole
{
    User = 0,
    SuperAdmin = 1,
}

public enum KbSourceType
{
    Text = 0,
    File = 1,
}

public enum ModerationStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

public enum SmartPhoneStatus
{
    Provisioning = 0,
    Active = 1,
    Suspended = 2,
    Failed = 3,
}

/// <summary>
/// رویدادهایی که می‌توانند پیامک تولید کنند. سوپرادمین برای هرکدام قالب متن و
/// شماره‌های گیرنده را تنظیم می‌کند.
/// </summary>
public enum SmsEventType
{
    OtpRequested = 0,
    UserRegistered = 1,
    SmartPhoneCreated = 2,
    KnowledgeBaseRejected = 3,
    KnowledgeBaseUpdated = 4,
    CallLimitNearlyReached = 5,
    CallLimitReached = 6,
    NewCallReceived = 7,
    SystemAlert = 8,
}

/// <summary>
/// مراحلِ لیدِ کاربرِ دمو که به سامانه‌های بیرونی (CRM فروش، کانال بله) اطلاع داده می‌شود.
/// هر مرحله برای هر شماره و هر مقصد
/// فقط یک‌بار ارسال می‌شود (حداکثر سه ارسال به‌ازای هر کاربر).
/// </summary>
public enum LeadStage
{
    /// <summary>۱) کاربر شماره‌ی موبایلش را وارد کرد (هنوز نام/داخلی ندارد).</summary>
    PhoneEntered = 1,
    /// <summary>۲) کاربر نام و نام‌خانوادگی (پروفایل) را تکمیل کرد.</summary>
    ProfileCompleted = 2,
    /// <summary>۳) کاربر تلفن هوشمند (داخلی) ساخت.</summary>
    SmartPhoneCreated = 3,
}
