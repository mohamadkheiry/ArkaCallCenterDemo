using System.Text.Json;
using ArkaCallCenter.Api.Extensions;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArkaCallCenter.Api.Controllers;

[ApiController]
[Route("api/calls")]
[Authorize]
public class CallsController : ControllerBase
{
    private readonly ArkaDbContext _db;
    private readonly IOpenAiService _openai;
    private readonly ISettingsService _settings;
    private readonly string _uploadsPath;

    public CallsController(ArkaDbContext db, IOpenAiService openai, ISettingsService settings, IConfiguration config)
    {
        _db = db;
        _openai = openai;
        _settings = settings;
        _uploadsPath = config["Storage:UploadsPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(_uploadsPath);
    }

    /// <summary>آخرین تماس‌های دریافتی روی تلفن هوشمند کاربر.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var calls = await _db.CallSessions.AsNoTracking()
            .Where(c => c.SmartPhone.UserId == userId)
            .OrderByDescending(c => c.StartedAt)
            .Take(100)
            .Select(c => new
            {
                c.Id,
                c.CallerId,
                c.StartedAt,
                c.DurationSeconds,
                c.AnsweredFromKb,
                hasRecording = c.RecordingPath != null,
            })
            .ToListAsync(ct);
        return Ok(calls);
    }

    /// <summary>
    /// فهرستِ سوالاتِ بی‌پاسخِ همه‌ی تماس‌های کاربر (سوالاتی که پاسخشان در پایگاه دانش نبود).
    /// هر مورد با callId و index مشخص می‌شود تا صوتش از مسیرِ audio گرفته شود.
    /// </summary>
    [HttpGet("unanswered")]
    public async Task<IActionResult> Unanswered(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var rows = await _db.CallSessions.AsNoTracking()
            .Where(c => c.SmartPhone.UserId == userId && c.UnansweredQuestionsJson != null)
            .OrderByDescending(c => c.StartedAt)
            .Select(c => new { c.Id, c.CallerId, c.StartedAt, c.UnansweredQuestionsJson })
            .Take(200)
            .ToListAsync(ct);

        var items = new List<object>();
        foreach (var r in rows)
        {
            var questions = SafeParse(r.UnansweredQuestionsJson);
            for (var i = 0; i < questions.Count; i++)
                items.Add(new
                {
                    callId = r.Id,
                    index = i,
                    question = questions[i],
                    callerId = r.CallerId,
                    startedAt = r.StartedAt,
                });
        }
        return Ok(items);
    }

    /// <summary>صوتِ (TTS) یک سوالِ بی‌پاسخِ مشخص. متن سمتِ سرور از تماسِ همان کاربر خوانده می‌شود.</summary>
    [HttpGet("{id:int}/unanswered/{index:int}/audio")]
    public async Task<IActionResult> UnansweredAudio(int id, int index, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var json = await _db.CallSessions.AsNoTracking()
            .Where(c => c.Id == id && c.SmartPhone.UserId == userId)
            .Select(c => c.UnansweredQuestionsJson)
            .FirstOrDefaultAsync(ct);
        if (json is null) return NotFound();

        var questions = SafeParse(json);
        if (index < 0 || index >= questions.Count) return NotFound();
        var text = questions[index];
        if (string.IsNullOrWhiteSpace(text)) return NotFound();

        // کش روی دیسک تا هر پخش، یک درخواستِ TTS جدید (و هزینه) نسازد.
        var cachePath = Path.Combine(_uploadsPath, $"unanswered_{id}_{index}.mp3");
        if (!System.IO.File.Exists(cachePath))
        {
            var voice = await ResolveVoiceAsync(userId, ct);
            byte[] audio;
            try
            {
                audio = await _openai.TextToSpeechAsync(text, voice, ct: ct);
            }
            catch (Exception)
            {
                return StatusCode(502, new { error = "تولید صوت ممکن نشد؛ لطفاً بعداً تلاش کنید." });
            }
            await System.IO.File.WriteAllBytesAsync(cachePath, audio, ct);
        }
        return PhysicalFile(cachePath, "audio/mpeg", enableRangeProcessing: true);
    }

    /// <summary>جزئیات یک تماسِ خودِ کاربر به‌همراه متن مکالمه (رونوشت).</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOne(int id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var c = await _db.CallSessions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.SmartPhone.UserId == userId, ct);
        if (c is null) return NotFound();
        return Ok(new
        {
            c.Id,
            c.CallerId,
            c.StartedAt,
            c.EndedAt,
            c.DurationSeconds,
            c.AnsweredFromKb,
            transcript = c.TranscriptJson,
            unansweredQuestions = SafeParse(c.UnansweredQuestionsJson),
            hasRecording = !string.IsNullOrEmpty(c.RecordingPath) && System.IO.File.Exists(c.RecordingPath),
        });
    }

    /// <summary>استریم فایل ضبط‌شده‌ی مکالمه — فقط برای صاحبِ همان تماس.</summary>
    [HttpGet("{id:int}/recording")]
    public async Task<IActionResult> GetRecording(int id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var path = await _db.CallSessions.AsNoTracking()
            .Where(c => c.Id == id && c.SmartPhone.UserId == userId)
            .Select(c => c.RecordingPath).FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, "audio/wav", enableRangeProcessing: true);
    }

    // ---- helpers ----
    private static List<string> SafeParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch (JsonException) { return new List<string>(); }
    }

    private async Task<string> ResolveVoiceAsync(int userId, CancellationToken ct)
    {
        var userVoice = await _db.Users.Where(u => u.Id == userId).Select(u => u.VoiceName).FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(userVoice)) return userVoice!;
        return await _settings.GetAsync(SettingKeys.DefaultVoiceName, "alloy", ct) ?? "alloy";
    }
}
