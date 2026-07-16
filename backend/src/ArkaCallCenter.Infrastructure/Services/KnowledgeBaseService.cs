using System.Text;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Core.Enums;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ArkaCallCenter.Infrastructure.Services;

public class KnowledgeBaseService : IKnowledgeBaseService
{
    private readonly ArkaDbContext _db;
    private readonly IFileTextExtractor _extractor;
    private readonly IModerationService _moderation;
    private readonly IRagService _rag;
    private readonly ISmsEventDispatcher _sms;
    private readonly ILogger<KnowledgeBaseService> _logger;
    private readonly string _uploadsPath;

    public KnowledgeBaseService(
        ArkaDbContext db, IFileTextExtractor extractor, IModerationService moderation,
        IRagService rag, ISmsEventDispatcher sms, IConfiguration config, ILogger<KnowledgeBaseService> logger)
    {
        _db = db;
        _extractor = extractor;
        _moderation = moderation;
        _rag = rag;
        _sms = sms;
        _logger = logger;
        _uploadsPath = config["Storage:UploadsPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(_uploadsPath);
    }

    public Task<KnowledgeBase?> GetAsync(int userId, CancellationToken ct = default)
        => _db.KnowledgeBases.AsNoTracking().FirstOrDefaultAsync(k => k.UserId == userId, ct);

    public async Task<KbResult> SetTextAsync(int userId, string text, CancellationToken ct = default)
    {
        text = (text ?? "").Trim();
        if (string.IsNullOrEmpty(text))
            return new KbResult(false, "متن پایگاه دانش خالی است.", null);
        if (text.Length > KbLimits.MaxTextChars)
            return new KbResult(false, $"حداکثر {KbLimits.MaxTextChars} کاراکتر مجاز است.", null);

        var mod = await _moderation.CheckAsync(text, ct);
        if (!mod.Allowed)
        {
            await NotifyRejectedAsync(userId, ct);
            return new KbResult(false, mod.Reason ?? "محتوا مغایر با قوانین است.", null);
        }

        // پس از تأیید: ارقامِ فارسی/عربی به ارقام انگلیسی تبدیل می‌شوند.
        text = NormalizeDigits(text);

        var kb = await UpsertAsync(userId, ct);
        DeleteFileIfAny(kb);
        kb.SourceType = KbSourceType.Text;
        kb.RawText = text;
        kb.CharCount = text.Length;
        kb.FileName = null;
        kb.FilePath = null;
        kb.FileSizeBytes = 0;
        kb.ModerationStatus = ModerationStatus.Approved;
        kb.ModerationReason = null;
        kb.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _rag.IndexAsync(kb, ct);
        await NotifyUpdatedAsync(userId, ct);
        return new KbResult(true, null, kb);
    }

    public async Task<KbResult> SetFileAsync(int userId, string fileName, string contentType, Stream content, long sizeBytes, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!KbLimits.AllowedExtensions.Contains(ext))
            return new KbResult(false, "فقط فایل‌های txt و Word (docx) مجاز هستند.", null);
        if (sizeBytes > KbLimits.MaxFileBytes)
            return new KbResult(false, "حجم فایل باید حداکثر ۱۰۰ کیلوبایت باشد.", null);

        // ذخیره‌ی موقت فایل
        var storedName = $"{userId}_{Guid.NewGuid():N}{ext}";
        var storedPath = Path.Combine(_uploadsPath, storedName);
        await using (var fs = File.Create(storedPath))
            await content.CopyToAsync(fs, ct);

        string extracted;
        try
        {
            await using var read = File.OpenRead(storedPath);
            extracted = await _extractor.ExtractAsync(read, fileName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File extraction failed");
            TryDelete(storedPath);
            return new KbResult(false, "خواندن محتوای فایل ممکن نشد.", null);
        }

        if (string.IsNullOrWhiteSpace(extracted))
        {
            TryDelete(storedPath);
            return new KbResult(false, "متنی از فایل استخراج نشد.", null);
        }

        var mod = await _moderation.CheckAsync(extracted, ct);
        if (!mod.Allowed)
        {
            // طبق قانون: فایل مغایر باید حذف شود.
            TryDelete(storedPath);
            await NotifyRejectedAsync(userId, ct);
            return new KbResult(false, mod.Reason ?? "محتوای فایل مغایر با قوانین است.", null);
        }

        // پس از تأیید: ارقامِ فارسی/عربی به ارقام انگلیسی تبدیل می‌شوند.
        extracted = NormalizeDigits(extracted);

        var kb = await UpsertAsync(userId, ct);
        DeleteFileIfAny(kb);
        kb.SourceType = KbSourceType.File;
        kb.RawText = extracted;
        kb.CharCount = extracted.Length;
        kb.FileName = fileName;
        kb.FilePath = storedPath;
        kb.FileSizeBytes = sizeBytes;
        kb.ModerationStatus = ModerationStatus.Approved;
        kb.ModerationReason = null;
        kb.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _rag.IndexAsync(kb, ct);
        await NotifyUpdatedAsync(userId, ct);
        return new KbResult(true, null, kb);
    }

    public async Task DeleteAsync(int userId, CancellationToken ct = default)
    {
        var kb = await _db.KnowledgeBases.FirstOrDefaultAsync(k => k.UserId == userId, ct);
        if (kb is null) return;
        DeleteFileIfAny(kb);
        var chunks = await _db.KnowledgeChunks.Where(c => c.KnowledgeBaseId == kb.Id).ToListAsync(ct);
        _db.KnowledgeChunks.RemoveRange(chunks);
        _db.KnowledgeBases.Remove(kb);
        await _db.SaveChangesAsync(ct);
    }

    // ---- helpers ----
    /// <summary>ارقامِ فارسی (۰-۹) و عربی (٠-٩) را به ارقام انگلیسی (0-9) تبدیل می‌کند.</summary>
    private static string NormalizeDigits(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch >= '۰' && ch <= '۹') sb.Append((char)('0' + (ch - '۰')));       // فارسی
            else if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠')));   // عربی
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    private async Task<KnowledgeBase> UpsertAsync(int userId, CancellationToken ct)
    {
        var kb = await _db.KnowledgeBases.FirstOrDefaultAsync(k => k.UserId == userId, ct);
        if (kb is null)
        {
            kb = new KnowledgeBase { UserId = userId };
            _db.KnowledgeBases.Add(kb);
            await _db.SaveChangesAsync(ct); // نیاز به Id برای indexing
        }
        return kb;
    }

    private void DeleteFileIfAny(KnowledgeBase kb)
    {
        if (!string.IsNullOrEmpty(kb.FilePath)) TryDelete(kb.FilePath);
    }

    private void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not delete file {Path}", path); }
    }

    private async Task NotifyUpdatedAsync(int userId, CancellationToken ct)
    {
        var phone = await _db.Users.Where(u => u.Id == userId).Select(u => u.PhoneNumber).FirstOrDefaultAsync(ct);
        await _sms.DispatchAsync(SmsEventType.KnowledgeBaseUpdated, new Dictionary<string, string>(), phone, ct);
    }

    private async Task NotifyRejectedAsync(int userId, CancellationToken ct)
    {
        var phone = await _db.Users.Where(u => u.Id == userId).Select(u => u.PhoneNumber).FirstOrDefaultAsync(ct);
        await _sms.DispatchAsync(SmsEventType.KnowledgeBaseRejected, new Dictionary<string, string>(), phone, ct);
    }
}
