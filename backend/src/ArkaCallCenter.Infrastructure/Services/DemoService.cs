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

public class DemoService : IDemoService
{
    private const int DemoExtensionMin = 1;
    private const int DemoExtensionMax = 999;
    private const int ReservedExtensionMin = 100;
    private const int ReservedExtensionMax = 300;

    private readonly ArkaDbContext _db;
    private readonly IAsteriskProvisioningService _asterisk;
    private readonly IRagService _rag;
    private readonly IOpenAiService _openai;
    private readonly ISettingsService _settings;
    private readonly ILogger<DemoService> _logger;
    private readonly string _uploadsPath;

    public DemoService(ArkaDbContext db, IAsteriskProvisioningService asterisk,
        IRagService rag, IOpenAiService openai, ISettingsService settings, IConfiguration config, ILogger<DemoService> logger)
    {
        _db = db;
        _asterisk = asterisk;
        _rag = rag;
        _openai = openai;
        _settings = settings;
        _logger = logger;
        _uploadsPath = config["Storage:UploadsPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(_uploadsPath);
    }

    public async Task<IReadOnlyList<DemoInfo>> ListAsync(CancellationToken ct = default)
    {
        var demos = await _db.Users.AsNoTracking()
            .Where(u => u.IsDemo)
            .Include(u => u.SmartPhone)
            .Include(u => u.KnowledgeBase)
            .OrderBy(u => u.SmartPhone!.Extension)
            .ToListAsync(ct);
        return demos.Select(Map).ToList();
    }

    public async Task<DemoResult> CreateAsync(int extension, string label, string welcomeText, string kbText,
        string? voice, int? minuteLimit, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(label))
            return new DemoResult(false, "نام دمو الزامی است.", null);

        if (extension is < DemoExtensionMin or > DemoExtensionMax)
            return new DemoResult(false, "شماره داخلی دمو باید بین ۱ تا ۹۹۹ باشد.", null);

        if (extension is >= ReservedExtensionMin and <= ReservedExtensionMax)
            return new DemoResult(false, "بازهٔ داخلی ۱۰۰ تا ۳۰۰ برای تلفن‌های انسانی رزرو است.", null);

        if (await _db.SmartPhones.AnyAsync(s => s.Extension == extension, ct))
            return new DemoResult(false, $"داخلی {extension} قبلاً استفاده شده است.", null);

        var user = new User
        {
            IsDemo = true,
            DemoLabel = label.Trim(),
            BrandName = label.Trim(),
            PhoneNumber = $"demo{extension}",
            Role = UserRole.User,
            ProfileCompleted = true,
            IsActive = true,
            VoiceName = voice,
            CallMinuteLimit = minuteLimit,
        };
        // پایگاه دانش (بدون moderation؛ توسط سوپرادمین ساخته می‌شود)
        var kb = new KnowledgeBase
        {
            User = user,
            SourceType = KbSourceType.Text,
            RawText = kbText?.Trim() ?? "",
            CharCount = (kbText ?? "").Trim().Length,
            ModerationStatus = ModerationStatus.Approved,
        };
        var secret = GenerateSecret();
        var sp = new SmartPhone
        {
            User = user,
            Extension = extension,
            SipSecret = secret,
            WelcomeMessageText = welcomeText?.Trim(),
            Status = SmartPhoneStatus.Provisioning,
        };

        user.KnowledgeBase = kb;
        user.SmartPhone = sp;
        _db.Users.Add(user);
        try
        {
            // User، KB و داخلی در یک SaveChanges ثبت می‌شوند تا ایندکس unique از
            // ساخت هم‌زمان دو دمو با یک داخلی جلوگیری کند و رکورد ناقص باقی نماند.
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Could not reserve requested demo extension {Extension}.", extension);
            return new DemoResult(false, $"داخلی {extension} آزاد نیست؛ یک شمارهٔ دیگر انتخاب کنید.", null);
        }

        await TryIndexAsync(kb, ct);

        await TryWelcomeAudioAsync(user, sp, ct);

        var provision = await _asterisk.ProvisionExtensionAsync(extension, secret, ct);
        sp.Status = provision.Success ? SmartPhoneStatus.Active : SmartPhoneStatus.Failed;
        await _db.SaveChangesAsync(ct);

        return new DemoResult(true, provision.Success ? null : "داخلی ساخته شد اما provisioning ایزابل ناموفق بود.", Map(await ReloadAsync(user.Id, ct)));
    }

    public async Task<DemoResult> UpdateAsync(int id, string? label, string? welcomeText, string? kbText,
        string? voice, int? minuteLimit, bool? isActive, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.SmartPhone).Include(u => u.KnowledgeBase)
            .FirstOrDefaultAsync(u => u.Id == id && u.IsDemo, ct);
        if (user is null) return new DemoResult(false, "دمو یافت نشد.", null);

        if (label is not null) { user.DemoLabel = label.Trim(); user.BrandName = label.Trim(); }
        if (voice is not null) user.VoiceName = voice;
        if (minuteLimit.HasValue) user.CallMinuteLimit = minuteLimit;
        if (isActive.HasValue)
        {
            user.IsActive = isActive.Value;
            if (user.SmartPhone is not null)
                user.SmartPhone.Status = isActive.Value ? SmartPhoneStatus.Active : SmartPhoneStatus.Suspended;
        }

        if (welcomeText is not null && user.SmartPhone is not null)
        {
            user.SmartPhone.WelcomeMessageText = welcomeText.Trim();
            await TryWelcomeAudioAsync(user, user.SmartPhone, ct);
        }

        if (kbText is not null)
        {
            user.KnowledgeBase ??= new KnowledgeBase { UserId = user.Id, SourceType = KbSourceType.Text };
            if (user.KnowledgeBase.Id == 0) _db.KnowledgeBases.Add(user.KnowledgeBase);
            user.KnowledgeBase.RawText = kbText.Trim();
            user.KnowledgeBase.CharCount = kbText.Trim().Length;
            user.KnowledgeBase.ModerationStatus = ModerationStatus.Approved;
            await _db.SaveChangesAsync(ct);
            await TryIndexAsync(user.KnowledgeBase, ct);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new DemoResult(true, null, Map(await ReloadAsync(user.Id, ct)));
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.SmartPhone).Include(u => u.KnowledgeBase)
            .FirstOrDefaultAsync(u => u.Id == id && u.IsDemo, ct);
        if (user is null) return;

        if (user.SmartPhone?.Extension is int ext)
            await _asterisk.RemoveExtensionAsync(ext, ct);

        if (user.KnowledgeBase is not null)
        {
            var chunks = await _db.KnowledgeChunks.Where(c => c.KnowledgeBaseId == user.KnowledgeBase.Id).ToListAsync(ct);
            _db.KnowledgeChunks.RemoveRange(chunks);
            _db.KnowledgeBases.Remove(user.KnowledgeBase);
        }
        if (user.SmartPhone is not null) _db.SmartPhones.Remove(user.SmartPhone);
        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
    }

    // ---- helpers ----
    private async Task<User> ReloadAsync(int id, CancellationToken ct) =>
        (await _db.Users.AsNoTracking().Include(u => u.SmartPhone).Include(u => u.KnowledgeBase)
            .FirstAsync(u => u.Id == id, ct));

    private static DemoInfo Map(User u) => new(
        u.Id, u.DemoLabel, u.SmartPhone?.Extension,
        u.SmartPhone?.Status.ToString() ?? "None",
        u.SmartPhone?.WelcomeMessageText, u.KnowledgeBase?.RawText,
        u.VoiceName, u.CallMinuteLimit, u.UsedMinutes, u.IsActive);

    private async Task TryIndexAsync(KnowledgeBase kb, CancellationToken ct)
    {
        try { if (!string.IsNullOrWhiteSpace(kb.RawText)) await _rag.IndexAsync(kb, ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Demo KB indexing failed (OpenAI key?)"); }
    }

    private async Task TryWelcomeAudioAsync(User user, SmartPhone sp, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sp.WelcomeMessageText)) return;
        try
        {
            var voice = user.VoiceName ?? await _settings.GetAsync(SettingKeys.DefaultVoiceName, "alloy", ct) ?? "alloy";
            // WAV avoids MP3 decoding and lets the realtime worker play the greeting immediately.
            var audio = await _openai.TextToSpeechAsync(sp.WelcomeMessageText, voice, "wav", ct);
            var path = Path.Combine(_uploadsPath, $"welcome_demo_{user.Id}.wav");
            await File.WriteAllBytesAsync(path, audio, ct);
            sp.WelcomeAudioPath = path;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Demo welcome TTS failed."); }
    }

    private static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(18);
        return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")[..16];
    }
}
