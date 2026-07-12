using System.Security.Cryptography;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Core.Enums;
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
    private readonly ILogger<SmartPhoneService> _logger;
    private readonly string _uploadsPath;

    public SmartPhoneService(
        ArkaDbContext db, IExtensionAllocator allocator, IAsteriskProvisioningService asterisk,
        IOpenAiService openai, ISettingsService settings, ISmsEventDispatcher sms,
        IConfiguration config, ILogger<SmartPhoneService> logger)
    {
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

    public async Task<SmartPhone?> SetWelcomeAsync(int userId, string text, CancellationToken ct = default)
    {
        text = (text ?? "").Trim();
        var sp = await _db.SmartPhones.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (sp is null)
        {
            sp = new SmartPhone { UserId = userId, Status = SmartPhoneStatus.Provisioning };
            _db.SmartPhones.Add(sp);
        }
        sp.WelcomeMessageText = text;
        sp.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await GenerateWelcomeAudioAsync(userId, sp, ct);
        await _db.SaveChangesAsync(ct);
        return sp;
    }

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

        // قبلاً ساخته شده؟
        if (sp.Extension is not null && sp.Status == SmartPhoneStatus.Active)
            return new SmartPhoneResult(true, null, sp);

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

        // اطمینان از وجود وویس خوش‌آمد
        if (string.IsNullOrEmpty(sp.WelcomeAudioPath))
            await GenerateWelcomeAudioAsync(userId, sp, ct);

        await _db.SaveChangesAsync(ct);

        await _sms.DispatchAsync(SmsEventType.SmartPhoneCreated,
            new Dictionary<string, string> { ["extension"] = extension.ToString(), ["firstName"] = user.FirstName ?? "" },
            user.PhoneNumber, ct);

        return new SmartPhoneResult(true, null, sp);
    }

    // ---- helpers ----
    private async Task GenerateWelcomeAudioAsync(int userId, SmartPhone sp, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sp.WelcomeMessageText)) return;
        try
        {
            var voice = await ResolveVoiceAsync(userId, ct);
            var audio = await _openai.TextToSpeechAsync(sp.WelcomeMessageText, voice, ct: ct);
            var path = Path.Combine(_uploadsPath, $"welcome_{userId}.mp3");
            await File.WriteAllBytesAsync(path, audio, ct);
            sp.WelcomeAudioPath = path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Welcome TTS generation failed for user {UserId}", userId);
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
