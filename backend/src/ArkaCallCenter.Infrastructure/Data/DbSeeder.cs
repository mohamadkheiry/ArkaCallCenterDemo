using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArkaCallCenter.Infrastructure.Data;

/// <summary>مقداردهی اولیه: تنظیمات پیش‌فرض، گوینده‌ها، قالب پیامک‌ها و سوپرادمین.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(ArkaDbContext db, string? superAdminPhone, CancellationToken ct = default)
    {
        await SeedSettingsAsync(db, ct);
        await SeedVoicesAsync(db, ct);
        await SeedSmsTemplatesAsync(db, ct);
        await SeedSuperAdminAsync(db, superAdminPhone, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedSettingsAsync(ArkaDbContext db, CancellationToken ct)
    {
        var defaults = new Dictionary<string, string>
        {
            [SettingKeys.OpenAiBaseUrl] = "https://api.openai.com/v1",
            [SettingKeys.OpenAiEmbeddingModel] = "text-embedding-3-small",
            [SettingKeys.OpenAiRealtimeModel] = "gpt-realtime",
            [SettingKeys.OpenAiTtsModel] = "gpt-4o-mini-tts",
            [SettingKeys.DefaultVoiceName] = "alloy",
            [SettingKeys.DefaultCallMinuteLimit] = "30",
            [SettingKeys.CallLimitWarningPercent] = "80",
            [SettingKeys.RagSimilarityThreshold] = "0.35",
            [SettingKeys.RagTopK] = "4",
            [SettingKeys.FallbackMessageText] = "پاسخ این سوال در پایگاه دانش من موجود نیست.",
            [SettingKeys.FallbackMessageVoice] = "alloy",
        };

        var existing = await db.AppSettings.Select(x => x.Key).ToListAsync(ct);
        foreach (var kv in defaults)
        {
            if (!existing.Contains(kv.Key))
                db.AppSettings.Add(new AppSetting { Key = kv.Key, Value = kv.Value });
        }
    }

    private static async Task SeedVoicesAsync(ArkaDbContext db, CancellationToken ct)
    {
        if (await db.VoiceOptions.AnyAsync(ct)) return;
        // گوینده‌های استاندارد OpenAI realtime/TTS
        var voices = new (string name, string display, bool def)[]
        {
            ("alloy", "الوی (خنثی)", true),
            ("echo", "اکو (مردانه)", false),
            ("shimmer", "شیمر (زنانه)", false),
            ("ash", "اَش", false),
            ("ballad", "بالاد", false),
            ("coral", "کورال", false),
            ("sage", "سیج", false),
            ("verse", "ورس", false),
        };
        foreach (var v in voices)
            db.VoiceOptions.Add(new VoiceOption { Name = v.name, DisplayName = v.display, IsDefault = v.def });
    }

    private static async Task SeedSmsTemplatesAsync(ArkaDbContext db, CancellationToken ct)
    {
        var existing = await db.SmsTemplates.Select(x => x.EventType).ToListAsync(ct);
        var templates = new Dictionary<SmsEventType, string>
        {
            [SmsEventType.OtpRequested] = "کد ورود شما به سامانه آرکا: {code}",
            [SmsEventType.UserRegistered] = "{firstName} عزیز، به سامانه تلفن هوشمند آرکا خوش آمدید.",
            [SmsEventType.SmartPhoneCreated] = "تلفن هوشمند شما ساخته شد. شماره داخلی: {extension}",
            [SmsEventType.KnowledgeBaseRejected] = "فایل/متن ارسالی به دلیل مغایرت با قوانین حذف شد. لطفاً محتوای مجاز بارگذاری کنید.",
            [SmsEventType.KnowledgeBaseUpdated] = "پایگاه دانش شما با موفقیت به‌روزرسانی شد.",
            [SmsEventType.CallLimitNearlyReached] = "به سقف زمان مکالمه نزدیک شده‌اید.",
            [SmsEventType.CallLimitReached] = "سقف زمان مکالمه شما به پایان رسید.",
            [SmsEventType.NewCallReceived] = "یک تماس جدید روی داخلی {extension} دریافت شد.",
            [SmsEventType.SystemAlert] = "{message}",
        };
        foreach (var t in templates)
        {
            if (!existing.Contains(t.Key))
                db.SmsTemplates.Add(new SmsTemplate { EventType = t.Key, Body = t.Value, Enabled = true });
        }

        // به‌صورت پیش‌فرض، رویدادهای مرتبط با کاربر به شماره‌ی خود کاربر می‌روند.
        if (!await db.SmsEventRecipients.AnyAsync(ct))
        {
            foreach (var evt in new[] { SmsEventType.OtpRequested, SmsEventType.UserRegistered,
                SmsEventType.SmartPhoneCreated, SmsEventType.KnowledgeBaseRejected,
                SmsEventType.KnowledgeBaseUpdated, SmsEventType.CallLimitNearlyReached,
                SmsEventType.CallLimitReached })
            {
                db.SmsEventRecipients.Add(new SmsEventRecipient { EventType = evt, UseUserOwnNumber = true });
            }
        }
    }

    private static async Task SeedSuperAdminAsync(ArkaDbContext db, string? phone, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone)) return;
        var admin = await db.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phone, ct);
        if (admin is null)
        {
            db.Users.Add(new User
            {
                PhoneNumber = phone,
                Role = UserRole.SuperAdmin,
                FirstName = "مدیر",
                LastName = "سامانه",
                BrandName = "آرکا",
                ProfileCompleted = true,
            });
        }
        else if (admin.Role != UserRole.SuperAdmin)
        {
            admin.Role = UserRole.SuperAdmin;
        }
    }
}
