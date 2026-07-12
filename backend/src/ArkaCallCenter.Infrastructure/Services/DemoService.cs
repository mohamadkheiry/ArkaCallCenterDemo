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
    private readonly ArkaDbContext _db;
    private readonly IExtensionAllocator _allocator;
    private readonly IAsteriskProvisioningService _asterisk;
    private readonly IRagService _rag;
    private readonly IOpenAiService _openai;
    private readonly ISettingsService _settings;
    private readonly ILogger<DemoService> _logger;
    private readonly string _uploadsPath;

    public DemoService(ArkaDbContext db, IExtensionAllocator allocator, IAsteriskProvisioningService asterisk,
        IRagService rag, IOpenAiService openai, ISettingsService settings, IConfiguration config, ILogger<DemoService> logger)
    {
        _db = db;
        _allocator = allocator;
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

    public async Task<DemoResult> CreateAsync(string label, string welcomeText, string kbText,
        string? voice, int? minuteLimit, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(label))
            return new DemoResult(false, "نام دمو الزامی است.", null);

        var ext = await _allocator.AllocateDemoAsync(ct);

        var user = new User
        {
            IsDemo = true,
            DemoLabel = label.Trim(),
            BrandName = label.Trim(),
            PhoneNumber = $"demo{ext}",
            Role = UserRole.User,
            ProfileCompleted = true,
            IsActive = true,
            VoiceName = voice,
            CallMinuteLimit = minuteLimit,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        // پایگاه دانش (بدون moderation؛ توسط سوپرادمین ساخته می‌شود)
        var kb = new KnowledgeBase
        {
            UserId = user.Id,
            SourceType = KbSourceType.Text,
            RawText = kbText?.Trim() ?? "",
            CharCount = (kbText ?? "").Trim().Length,
            ModerationStatus = ModerationStatus.Approved,
        };
        _db.KnowledgeBases.Add(kb);
        await _db.SaveChangesAsync(ct);
        await TryIndexAsync(kb, ct);

        var secret = GenerateSecret();
        var sp = new SmartPhone
        {
            UserId = user.Id,
            WelcomeMessageText = welcomeText?.Trim(),
            Status = SmartPhoneStatus.Provisioning,
        };
        _db.SmartPhones.Add(sp);
        await _db.SaveChangesAsync(ct);

        await TryWelcomeAudioAsync(user, sp, ct);

        var provision = await _asterisk.ProvisionExtensionAsync(ext, secret, ct);
        sp.Extension = ext;
        sp.SipSecret = secret;
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
            var audio = await _openai.TextToSpeechAsync(sp.WelcomeMessageText, voice, ct: ct);
            var path = Path.Combine(_uploadsPath, $"welcome_demo_{user.Id}.mp3");
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
