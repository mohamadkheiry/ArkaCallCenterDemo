using ArkaCallCenter.Api.Extensions;
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
    public CallsController(ArkaDbContext db) => _db = db;

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
}
