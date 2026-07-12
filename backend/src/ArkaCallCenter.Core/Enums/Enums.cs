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
