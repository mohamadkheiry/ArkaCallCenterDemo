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
    private readonly string _uploadsPath;

    public AdminController(ArkaDbContext db, ISettingsService settings, IOpenAiService openai, IConfiguration config)
    {
        _db = db;
        _settings = settings;
        _openai = openai;
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
            var audio = await _openai.TextToSpeechAsync(req.Text, req.Voice, ct);
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
}
