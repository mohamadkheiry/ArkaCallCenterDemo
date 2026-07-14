using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ArkaCallCenter.Infrastructure.Data;

/// <summary>مقداردهی اولیه: تنظیمات پیش‌فرض، گوینده‌ها، قالب پیامک‌ها و سوپرادمین.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(ArkaDbContext db, IConfiguration config, CancellationToken ct = default)
    {
        await SeedSettingsAsync(db, config, ct);
        await SeedVoicesAsync(db, ct);
        await SeedSmsTemplatesAsync(db, ct);
        await SeedSuperAdminAsync(db, config["SuperAdmin:PhoneNumber"], ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// تنظیمات را seed می‌کند. مقادیرِ از env (config) اولویت دارند تا استقرار کاملاً
    /// از روی .env قابل انجام باشد؛ اگر env نبود، مقدار پیش‌فرض. کلیدها فقط وقتی که
    /// از قبل وجود نداشته باشند اضافه می‌شوند (تغییرات پنل بازنویسی نمی‌شود).
    /// </summary>
    private static async Task SeedSettingsAsync(ArkaDbContext db, IConfiguration config, CancellationToken ct)
    {
        // نگاشت: کلید تنظیم → (مقدار env، مقدار پیش‌فرض، آیا سِری)
        var defaults = new (string key, string? envVal, string? fallback, bool secret)[]
        {
            (SettingKeys.OpenAiBaseUrl, config["OPENAI_BASE_URL"], "https://api.openai.com/v1", false),
            (SettingKeys.OpenAiApiKey, config["OPENAI_API_KEY"], null, true),
            (SettingKeys.OpenAiChatModel, config["OPENAI_CHAT_MODEL"], "gpt-4o-mini", false),
            (SettingKeys.OpenAiEmbeddingModel, config["OPENAI_EMBEDDING_MODEL"], "text-embedding-3-small", false),
            (SettingKeys.OpenAiRealtimeModel, config["OPENAI_REALTIME_MODEL"], "gpt-realtime", false),
            (SettingKeys.OpenAiTtsModel, config["OPENAI_TTS_MODEL"], "gpt-4o-mini-tts", false),
            (SettingKeys.SmsIrApiKey, config["SMSIR_API_KEY"], null, true),
            (SettingKeys.SmsIrVerifyTemplateId, config["SMSIR_VERIFY_TEMPLATE_ID"], null, false),
            (SettingKeys.SmsIrLineNumber, config["SMSIR_LINE_NUMBER"], null, false),
            (SettingKeys.DefaultVoiceName, config["DEFAULT_VOICE"], "alloy", false),
            (SettingKeys.DefaultCallMinuteLimit, config["DEFAULT_CALL_MINUTES"], "30", false),
            (SettingKeys.CallLimitWarningPercent, null, "80", false),
            (SettingKeys.RagSimilarityThreshold, config["RAG_SIMILARITY_THRESHOLD"], "0.35", false),
            (SettingKeys.RagTopK, config["RAG_TOP_K"], "4", false),
            (SettingKeys.FallbackMessageText, null, "پاسخ این سوال در پایگاه دانش من موجود نیست.", false),
            (SettingKeys.FallbackMessageVoice, null, "alloy", false),
        };

        var existing = await db.AppSettings.Select(x => x.Key).ToListAsync(ct);
        foreach (var (key, envVal, fallback, secret) in defaults)
        {
            if (existing.Contains(key)) continue;
            var value = !string.IsNullOrWhiteSpace(envVal) ? envVal.Trim() : fallback;
            // کلیدهای سِری فقط وقتی از env آمده باشند ساخته می‌شوند (رکورد خالی نسازیم).
            if (secret && string.IsNullOrWhiteSpace(value)) continue;
            db.AppSettings.Add(new AppSetting { Key = key, Value = value, IsSecret = secret });
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
