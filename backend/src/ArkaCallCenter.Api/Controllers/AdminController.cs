using ArkaCallCenter.Api.Models;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ArkaCallCenter.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "SuperAdmin")]
public class AdminController : ControllerBase
{
    private static readonly HashSet<string> SecretKeys = new()
    {
        SettingKeys.OpenAiApiKey,
        SettingKeys.SmsIrApiKey,
    };

    private readonly ArkaDbContext _db;
    private readonly ISettingsService _settings;
    private readonly IOpenAiService _openai;
    private readonly IDemoService _demos;
    private readonly IAsteriskProvisioningService _asterisk;
    private readonly string _uploadsPath;

    public AdminController(ArkaDbContext db, ISettingsService settings, IOpenAiService openai,
        IDemoService demos, IAsteriskProvisioningService asterisk, IConfiguration config)
    {
        _db = db;
        _settings = settings;
        _openai = openai;
        _demos = demos;
        _asterisk = asterisk;
        _uploadsPath = config["Storage:UploadsPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(_uploadsPath);
    }

    // ---------------- Settings (OpenAI, SMS.ir, limits, RAG, default voice) ----------------
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
        => Ok(await _settings.GetAllAsync(ct));

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(UpdateSettingsRequest req, CancellationToken ct)
    {
        foreach (var kv in req.Settings)
        {
            // مقدار ماسک‌شده را نادیده بگیر تا سِری با ماسک بازنویسی نشود.
            if (kv.Value == "********") continue;
            await _settings.SetAsync(kv.Key, kv.Value, SecretKeys.Contains(kv.Key), ct);
        }
        return Ok(new { message = "تنظیمات ذخیره شد." });
    }

    // ---------------- SMS templates ----------------
    [HttpGet("sms-templates")]
    public async Task<IActionResult> GetSmsTemplates(CancellationToken ct)
    {
        var list = await _db.SmsTemplates.AsNoTracking().OrderBy(t => t.EventType).ToListAsync(ct);
        return Ok(list.Select(t => new { eventType = t.EventType.ToString(), t.Body, t.Enabled }));
    }

    [HttpPut("sms-templates")]
    public async Task<IActionResult> UpdateSmsTemplates(UpdateSmsTemplatesRequest req, CancellationToken ct)
    {
        foreach (var dto in req.Templates)
        {
            var t = await _db.SmsTemplates.FirstOrDefaultAsync(x => x.EventType == dto.EventType, ct);
            if (t is null)
            {
                _db.SmsTemplates.Add(new SmsTemplate { EventType = dto.EventType, Body = dto.Body, Enabled = dto.Enabled });
            }
            else
            {
                t.Body = dto.Body;
                t.Enabled = dto.Enabled;
                t.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "قالب پیامک‌ها ذخیره شد." });
    }

    // ---------------- SMS event recipients ----------------
    [HttpGet("sms-events")]
    public async Task<IActionResult> GetSmsEvents(CancellationToken ct)
    {
        var list = await _db.SmsEventRecipients.AsNoTracking().ToListAsync(ct);
        return Ok(list.Select(r => new
        {
            eventType = r.EventType.ToString(),
            r.UseUserOwnNumber,
            r.PhoneNumber,
        }));
    }

    [HttpPut("sms-events")]
    public async Task<IActionResult> UpdateSmsEvents(UpdateSmsEventsRequest req, CancellationToken ct)
    {
        // جای‌گزینی کامل لیست گیرندگان
        var existing = await _db.SmsEventRecipients.ToListAsync(ct);
        _db.SmsEventRecipients.RemoveRange(existing);
        foreach (var r in req.Recipients)
        {
            _db.SmsEventRecipients.Add(new SmsEventRecipient
            {
                EventType = r.EventType,
                UseUserOwnNumber = r.UseUserOwnNumber,
                PhoneNumber = r.PhoneNumber,
            });
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "گیرندگان پیامک ذخیره شد." });
    }

    // ---------------- Voices ----------------
    [HttpGet("voices")]
    public async Task<IActionResult> GetVoices(CancellationToken ct)
    {
        var list = await _db.VoiceOptions.AsNoTracking().OrderBy(v => v.Id).ToListAsync(ct);
        return Ok(list.Select(v => new { v.Name, v.DisplayName, v.Enabled, v.IsDefault }));
    }

    [HttpPut("voices")]
    public async Task<IActionResult> UpdateVoices(UpdateVoicesRequest req, CancellationToken ct)
    {
        foreach (var dto in req.Voices)
        {
            var v = await _db.VoiceOptions.FirstOrDefaultAsync(x => x.Name == dto.Name, ct);
            if (v is null)
            {
                _db.VoiceOptions.Add(new VoiceOption
                {
                    Name = dto.Name, DisplayName = dto.DisplayName, Enabled = dto.Enabled, IsDefault = dto.IsDefault,
                });
            }
            else
            {
                v.DisplayName = dto.DisplayName;
                v.Enabled = dto.Enabled;
                v.IsDefault = dto.IsDefault;
                v.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);

        // یک گوینده‌ی پیش‌فرض یکتا
        var def = req.Voices.FirstOrDefault(v => v.IsDefault);
        if (def is not null)
        {
            foreach (var v in await _db.VoiceOptions.Where(x => x.Name != def.Name && x.IsDefault).ToListAsync(ct))
                v.IsDefault = false;
            await _db.SaveChangesAsync(ct);
            await _settings.SetAsync(SettingKeys.DefaultVoiceName, def.Name, false, ct);
        }
        return Ok(new { message = "گوینده‌ها ذخیره شد." });
    }

    // ---------------- Fallback message (text + TTS) ----------------
    [HttpGet("fallback-message")]
    public async Task<IActionResult> GetFallback(CancellationToken ct)
    {
        var text = await _settings.GetAsync(SettingKeys.FallbackMessageText, "پاسخ این سوال در پایگاه دانش من موجود نیست.", ct);
        var voice = await _settings.GetAsync(SettingKeys.FallbackMessageVoice, "alloy", ct);
        var audioPath = await _settings.GetAsync(SettingKeys.FallbackAudioPath, null, ct);
        return Ok(new { text, voice, hasAudio = !string.IsNullOrEmpty(audioPath) && System.IO.File.Exists(audioPath) });
    }

    /// <summary>ذخیره‌ی متن پیام fallback و تولید نسخه‌ی صوتی با گوینده‌ی منتخب (صرفه‌جویی توکن).</summary>
    [HttpPut("fallback-message")]
    public async Task<IActionResult> SetFallback(FallbackMessageRequest req, CancellationToken ct)
    {
        await _settings.SetAsync(SettingKeys.FallbackMessageText, req.Text, false, ct);
        await _settings.SetAsync(SettingKeys.FallbackMessageVoice, req.Voice, false, ct);

        try
        {
            var audio = await _openai.TextToSpeechAsync(req.Text, req.Voice, ct: ct);
            var path = Path.Combine(_uploadsPath, "fallback.mp3");
            await System.IO.File.WriteAllBytesAsync(path, audio, ct);
            await _settings.SetAsync(SettingKeys.FallbackAudioPath, path, false, ct);
            return Ok(new { message = "متن ذخیره و صوت تولید شد.", audioGenerated = true });
        }
        catch
        {
            // اگر OpenAI پیکربندی نشده باشد، فقط متن ذخیره می‌شود.
            return Ok(new { message = "متن ذخیره شد؛ تولید صوت انجام نشد (کلید OpenAI را بررسی کنید).", audioGenerated = false });
        }
    }

    // ---------------- Users & limits ----------------
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var users = await _db.Users.AsNoTracking()
            .Include(u => u.SmartPhone)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new
            {
                u.Id,
                u.PhoneNumber,
                u.FirstName,
                u.LastName,
                u.BrandName,
                role = u.Role.ToString(),
                u.CallMinuteLimit,
                u.UsedMinutes,
                u.IsActive,
                extension = u.SmartPhone != null ? (int?)u.SmartPhone.Extension : null,
            })
            .ToListAsync(ct);
        return Ok(users);
    }

    [HttpPut("users/{id:int}/limit")]
    public async Task<IActionResult> SetUserLimit(int id, UpdateUserLimitRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();
        user.CallMinuteLimit = req.CallMinuteLimit;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "محدودیت کاربر به‌روزرسانی شد." });
    }

    // ---------------- Demos (keys 1..999) ----------------
    [HttpGet("demos")]
    public async Task<IActionResult> GetDemos(CancellationToken ct) => Ok(await _demos.ListAsync(ct));

    [HttpPost("demos")]
    public async Task<IActionResult> CreateDemo(CreateDemoRequest req, CancellationToken ct)
    {
        var r = await _demos.CreateAsync(req.Label, req.WelcomeText, req.KbText, req.Voice, req.MinuteLimit, ct);
        return r.Ok ? Ok(r.Demo) : BadRequest(new { error = r.Error });
    }

    [HttpPut("demos/{id:int}")]
    public async Task<IActionResult> UpdateDemo(int id, UpdateDemoRequest req, CancellationToken ct)
    {
        var r = await _demos.UpdateAsync(id, req.Label, req.WelcomeText, req.KbText, req.Voice, req.MinuteLimit, req.IsActive, ct);
        return r.Ok ? Ok(r.Demo) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("demos/{id:int}")]
    public async Task<IActionResult> DeleteDemo(int id, CancellationToken ct)
    {
        await _demos.DeleteAsync(id, ct);
        return Ok(new { message = "دمو حذف شد." });
    }

    // ---------------- Main greeting (IVR reception) ----------------
    [HttpGet("main-greeting")]
    public async Task<IActionResult> GetMainGreeting(CancellationToken ct)
    {
        var text = await _settings.GetAsync(SettingKeys.MainGreetingText, "به شرکت ما خوش آمدید. لطفاً شماره داخلی موردنظر را وارد کنید.", ct);
        var voice = await _settings.GetAsync(SettingKeys.MainGreetingVoice, "alloy", ct);
        var sound = await _settings.GetAsync(SettingKeys.MainGreetingAsteriskSound, null, ct);
        return Ok(new { text, voice, asteriskSound = sound, uploaded = !string.IsNullOrEmpty(sound) });
    }

    /// <summary>ذخیره‌ی متن پذیرش، تولید WAV ۸kHz و آپلود آن به ایزابل برای پخش در dialplan.</summary>
    [HttpPut("main-greeting")]
    public async Task<IActionResult> SetMainGreeting(MainGreetingRequest req, CancellationToken ct)
    {
        await _settings.SetAsync(SettingKeys.MainGreetingText, req.Text, false, ct);
        await _settings.SetAsync(SettingKeys.MainGreetingVoice, req.Voice, false, ct);
        try
        {
            var pcm = await _openai.TextToSpeechAsync(req.Text, req.Voice, "pcm", ct);
            var wav = Infrastructure.Audio.AudioConvert.PcmToWav8k(pcm, 24000);
            var localPath = Path.Combine(_uploadsPath, "main-greeting.wav");
            await System.IO.File.WriteAllBytesAsync(localPath, wav, ct);
            await _settings.SetAsync(SettingKeys.MainGreetingAudioPath, localPath, false, ct);

            var soundName = await _asterisk.UploadSoundAsync(wav, "main-greeting", ct);
            if (soundName is not null)
                await _settings.SetAsync(SettingKeys.MainGreetingAsteriskSound, soundName, false, ct);

            return Ok(new
            {
                message = soundName is not null
                    ? "متن ذخیره، صوت تولید و روی ایزابل آپلود شد."
                    : "متن و صوت ذخیره شد؛ آپلود به ایزابل انجام نشد (SSH را بررسی کنید).",
                asteriskSound = soundName,
            });
        }
        catch
        {
            return Ok(new { message = "متن ذخیره شد؛ تولید صوت انجام نشد (کلید OpenAI را بررسی کنید).", asteriskSound = (string?)null });
        }
    }

    // ---------------- Hold music (while AI is thinking) ----------------
    [HttpGet("hold-music")]
    public async Task<IActionResult> GetHoldMusic(CancellationToken ct)
    {
        var path = await _settings.GetAsync(SettingKeys.HoldMusicPath, null, ct);
        var enabled = await _settings.GetAsync(SettingKeys.HoldMusicEnabled, "false", ct);
        return Ok(new { enabled = enabled == "true", hasFile = !string.IsNullOrEmpty(path) && System.IO.File.Exists(path) });
    }

    [HttpPost("hold-music")]
    [RequestSizeLimit(5_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadHoldMusic(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "فایلی ارسال نشد." });
        if (!file.FileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "لطفاً فایل WAV (۱۶ بیت) بارگذاری کنید." });

        try
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var slin = Infrastructure.Audio.AudioConvert.WavToSlin8k(ms.ToArray());
            var path = Path.Combine(_uploadsPath, "hold.sln");
            await System.IO.File.WriteAllBytesAsync(path, slin, ct);
            await _settings.SetAsync(SettingKeys.HoldMusicPath, path, false, ct);
            await _settings.SetAsync(SettingKeys.HoldMusicEnabled, "true", false, ct);
            return Ok(new { message = "موسیقی انتظار ذخیره شد.", seconds = slin.Length / (AudioConvertRate) });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "پردازش فایل ناموفق بود: " + ex.Message });
        }
    }

    private const int AudioConvertRate = 16000; // بایت بر ثانیه SLIN 8kHz (۸۰۰۰ نمونه × ۲ بایت)

    [HttpPut("hold-music/enabled")]
    public async Task<IActionResult> SetHoldEnabled(HoldEnabledRequest req, CancellationToken ct)
    {
        await _settings.SetAsync(SettingKeys.HoldMusicEnabled, req.Enabled ? "true" : "false", false, ct);
        return Ok(new { message = "به‌روزرسانی شد." });
    }

    // ---------------- Tutorial video (uploaded by super admin, shown to users) ----------------
    [HttpPost("tutorial-video")]
    [RequestSizeLimit(314_572_800)] // 300MB
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadTutorialVideo(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "فایلی ارسال نشد." });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".mp4" or ".webm"))
            return BadRequest(new { error = "فقط فرمت mp4 یا webm مجاز است." });

        // حذف فایل قبلی
        var old = await _settings.GetAsync(SettingKeys.TutorialVideoPath, null, ct);
        if (!string.IsNullOrEmpty(old) && System.IO.File.Exists(old))
            try { System.IO.File.Delete(old); } catch { /* ignore */ }

        var path = Path.Combine(_uploadsPath, $"tutorial{ext}");
        await using (var fs = System.IO.File.Create(path))
            await file.CopyToAsync(fs, ct);
        await _settings.SetAsync(SettingKeys.TutorialVideoPath, path, false, ct);
        return Ok(new { message = "ویدیوی آموزشی بارگذاری شد.", sizeBytes = file.Length });
    }

    [HttpDelete("tutorial-video")]
    public async Task<IActionResult> DeleteTutorialVideo(CancellationToken ct)
    {
        var path = await _settings.GetAsync(SettingKeys.TutorialVideoPath, null, ct);
        if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            try { System.IO.File.Delete(path); } catch { /* ignore */ }
        await _settings.SetAsync(SettingKeys.TutorialVideoPath, null, false, ct);
        return Ok(new { message = "ویدیوی آموزشی حذف شد." });
    }

    // ---------------- Token usage reports ----------------
    /// <summary>مصرف توکن به تفکیک کلید API (به‌همراه تاریخ اولین/آخرین استفاده).</summary>
    [HttpGet("usage/keys")]
    public async Task<IActionResult> UsageByKey(CancellationToken ct)
    {
        var rows = await _db.TokenUsages.AsNoTracking()
            .GroupBy(u => u.ApiKeyFingerprint)
            .Select(g => new
            {
                apiKey = g.Key,
                totalTokens = g.Sum(x => (long)x.TotalTokens),
                promptTokens = g.Sum(x => (long)x.PromptTokens),
                completionTokens = g.Sum(x => (long)x.CompletionTokens),
                calls = g.Count(),
                firstUsed = g.Min(x => x.CreatedAt),
                lastUsed = g.Max(x => x.CreatedAt),
            })
            .OrderByDescending(x => x.totalTokens)
            .ToListAsync(ct);
        return Ok(rows);
    }

    /// <summary>مصرف توکن به تفکیک کاربر / شماره موبایل.</summary>
    [HttpGet("usage/users")]
    public async Task<IActionResult> UsageByUser(CancellationToken ct)
    {
        var usage = await _db.TokenUsages.AsNoTracking()
            .GroupBy(u => new { u.UserId, u.PhoneNumber })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.PhoneNumber,
                totalTokens = g.Sum(x => (long)x.TotalTokens),
                promptTokens = g.Sum(x => (long)x.PromptTokens),
                completionTokens = g.Sum(x => (long)x.CompletionTokens),
                calls = g.Count(),
                lastUsed = g.Max(x => x.CreatedAt),
            })
            .OrderByDescending(x => x.totalTokens)
            .ToListAsync(ct);

        // افزودن نام برند/کاربر
        var ids = usage.Where(u => u.UserId != null).Select(u => u.UserId!.Value).Distinct().ToList();
        var users = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.BrandName })
            .ToListAsync(ct);
        var map = users.ToDictionary(u => u.Id);

        var result = usage.Select(u => new
        {
            u.UserId,
            u.PhoneNumber,
            name = u.UserId != null && map.TryGetValue(u.UserId.Value, out var us)
                ? $"{us.FirstName} {us.LastName}".Trim()
                : null,
            brand = u.UserId != null && map.TryGetValue(u.UserId.Value, out var ub) ? ub.BrandName : null,
            u.totalTokens,
            u.promptTokens,
            u.completionTokens,
            u.calls,
            u.lastUsed,
        });
        return Ok(result);
    }
}
