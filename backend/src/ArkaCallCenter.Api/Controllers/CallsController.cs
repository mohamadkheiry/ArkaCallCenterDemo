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
            })
            .ToListAsync(ct);
        return Ok(calls);
    }
}
