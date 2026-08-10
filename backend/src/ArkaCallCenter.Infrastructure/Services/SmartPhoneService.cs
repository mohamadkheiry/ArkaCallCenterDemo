using System.Security.Cryptography;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Core.Enums;
using ArkaCallCenter.Infrastructure.Audio;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ArkaCallCenter.Infrastructure.Services;

public class SmartPhoneService : ISmartPhoneService
{
    private readonly ArkaDbContext _db;
    private readonly IExtensionAllocator _allocator;
    private readonly IAsteriskProvisioningService _asterisk;
    private readonly IOpenAiService _openai;
    private readonly ISettingsService _settings;
    private readonly ISmsEventDispatcher _sms;
    private readonly ICrmLeadService _crm;
    private readonly IBaleNotifier _bale;
    private readonly ILogger<SmartPhoneService> _logger;
    private readonly string _uploadsPath;

    public SmartPhoneService(
        ArkaDbContext db, IExtensionAllocator allocator, IAsteriskProvisioningService asterisk,
        IOpenAiService openai, ISettingsService settings, ISmsEventDispatcher sms,
        ICrmLeadService crm, IBaleNotifier bale, IConfiguration config, ILogger<SmartPhoneService> logger)
    {
        _crm = crm;
        _bale = bale;
        _db = db;
        _allocator = allocator;
        _asterisk = asterisk;
        _openai = openai;
        _settings = settings;
        _sms = sms;
        _logger = logger;
        _uploadsPath = config["Storage:UploadsPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(_uploadsPath);
    }

    public Task<SmartPhone?> GetAsync(int userId, CancellationToken ct = default)
        => _db.SmartPhones.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);

    public async Task<SmartPhone?> SetAccuracyAsync(int userId, int percent, CancellationToken ct = default)
    {
        percent = Math.Clamp(percent, 10, 100);
        var sp = await _db.SmartPhones.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (sp is null)
        {
            sp = new SmartPhone { UserId = userId, Status = SmartPhoneStatus.Provisioning };
            _db.SmartPhones.Add(sp);
        }
        sp.AnswerAccuracyPercent = percent;
        sp.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return sp;
    }

    public async Task<WelcomeAudioResult> SetWelcomeAsync(int userId, string text, CancellationToken ct = default)
    {
        text = (text ?? "").Trim();
        var sp = await _db.SmartPhones.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        var generated = await GenerateWelcomeAudioAsync(userId, text, ct);
        if (!generated.Ok)
            return new WelcomeAudioResult(false, generated.Error, null);

        if (sp is null)
        {
            sp = new SmartPhone { UserId = userId, Status = SmartPhoneStatus.Provisioning };
            _db.SmartPhones.Add(sp);
        }
        sp.WelcomeMessageText = text;
        sp.WelcomeAudioPath = generated.Path;
        sp.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new WelcomeAudioResult(true, null, sp);
    }

    // تخصیصِ داخلی TOCTOU-safe نیست (خواندنِ لیست → انتخاب → ذخیره). این قفلِ درون‌پردازه‌ای
    // دو درخواستِ همزمانِ «ساخت» را ترتیبی می‌کند تا شماره‌ی تکراری provision/ذخیره نشود.
    private static readonly SemaphoreSlim _createLock = new(1, 1);

    public async Task<SmartPhoneResult> CreateAsync(int userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.SmartPhone)
            .Include(u => u.KnowledgeBase)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return new SmartPhoneResult(false, "کاربر یافت نشد.", null);

        // پیش‌نیازها
        if (user.KnowledgeBase is null || user.KnowledgeBase.ModerationStatus != ModerationStatus.Approved)
            return new SmartPhoneResult(false, "ابتدا یک پایگاه دانش تأییدشده ثبت کنید.", null);

        var sp = user.SmartPhone;
        if (sp is null || string.IsNullOrWhiteSpace(sp.WelcomeMessageText))
            return new SmartPhoneResult(false, "ابتدا پیام خوش‌آمد را ثبت کنید.", null);

        if (!HasStaticWelcomeAudio(sp))
            return new SmartPhoneResult(
                false,
                "فایل صوتی ثابت پیام خوش‌آمد آماده نیست؛ لطفاً پیام خوش‌آمد را دوباره ذخیره کنید.",
                null);

        // قبلاً ساخته شده؟
        if (sp.Extension is not null && sp.Status == SmartPhoneStatus.Active)
            return new SmartPhoneResult(true, null, sp);

        await _createLock.WaitAsync(ct);
        try
        {
            var extension = await _allocator.AllocateAsync(ct);
            var secret = GenerateSecret();

            var provision = await _asterisk.ProvisionExtensionAsync(extension, secret, ct);
            if (!provision.Success)
            {
                sp.Status = SmartPhoneStatus.Failed;
                await _db.SaveChangesAsync(ct);
                return new SmartPhoneResult(false, provision.Error ?? "ساخت داخلی ناموفق بود.", null);
            }

            sp.Extension = extension;
            sp.SipSecret = secret;
            sp.Status = SmartPhoneStatus.Active;
            sp.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            await _sms.DispatchAsync(SmsEventType.SmartPhoneCreated,
                new Dictionary<string, string> { ["extension"] = extension.ToString(), ["firstName"] = user.FirstName ?? "" },
                user.PhoneNumber, ct);

            // مرحله‌ی ۳ لید: داخلی ساخته شد → توضیحاتِ داخلی هم برای تیم فروش ارسال می‌شود.
            _crm.Enqueue(LeadStage.SmartPhoneCreated, user.PhoneNumber);
            _bale.Enqueue(LeadStage.SmartPhoneCreated, user.PhoneNumber);

            return new SmartPhoneResult(true, null, sp);
        }
        finally
        {
            _createLock.Release();
        }
    }

    // ---- helpers ----
    private async Task<(bool Ok, string? Error, string? Path)> GenerateWelcomeAudioAsync(
        int userId,
        string welcomeText,
        CancellationToken ct)
    {
        try
        {
            var voice = await ResolveVoiceAsync(userId, ct);
            // این درخواست فقط هنگام ذخیره/تغییر پیام انجام می‌شود؛ تماس‌ها همین فایل ثابت را پخش می‌کنند.
            var audio = await _openai.TextToSpeechAsync(welcomeText, voice, "wav", ct);
            if (AudioConvert.WavToSlin8k(audio).Length == 0)
                throw new InvalidDataException("فایل صوتی تولیدشده خالی است.");

            var path = Path.Combine(_uploadsPath, $"welcome_{userId}.wav");
            await WriteAtomicallyAsync(path, audio, ct);
            return (true, null, path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Welcome TTS generation failed for user {UserId}", userId);
            return (false, "تولید فایل صوتی پیام خوش‌آمد انجام نشد؛ تنظیمات سرویس هوش مصنوعی را بررسی و دوباره تلاش کنید.", null);
        }
    }

    private static bool HasStaticWelcomeAudio(SmartPhone sp)
    {
        if (string.IsNullOrWhiteSpace(sp.WelcomeAudioPath) ||
            !Path.GetExtension(sp.WelcomeAudioPath).Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(sp.WelcomeAudioPath))
            return false;

        try
        {
            return AudioConvert.WavToSlin8k(File.ReadAllBytes(sp.WelcomeAudioPath)).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task WriteAtomicallyAsync(string path, byte[] audio, CancellationToken ct)
    {
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(tempPath, audio, ct);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch { /* پاک‌سازی فایل موقت نباید نتیجه اصلی را تغییر دهد. */ }
        }
    }

    private async Task<string> ResolveVoiceAsync(int userId, CancellationToken ct)
    {
        var userVoice = await _db.Users.Where(u => u.Id == userId).Select(u => u.VoiceName).FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(userVoice)) return userVoice!;
        return await _settings.GetAsync(SettingKeys.DefaultVoiceName, "alloy", ct) ?? "alloy";
    }

    private static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(18);
        return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")[..16];
    }
}
