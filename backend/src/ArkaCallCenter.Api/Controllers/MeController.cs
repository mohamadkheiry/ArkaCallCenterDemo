using ArkaCallCenter.Api.Extensions;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArkaCallCenter.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly ArkaDbContext _db;
    public MeController(ArkaDbContext db) => _db = db;

    /// <summary>اطلاعات کاربر جاری.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var id = User.GetUserId();
        var user = await _db.Users
            .Include(u => u.SmartPhone)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();

        return Ok(new
        {
            user.Id,
            user.PhoneNumber,
            user.FirstName,
            user.LastName,
            user.BrandName,
            role = user.Role.ToString(),
            user.ProfileCompleted,
            user.VoiceName,
            user.CallMinuteLimit,
            user.UsedMinutes,
            smartPhone = user.SmartPhone == null ? null : new
            {
                user.SmartPhone.Extension,
                status = user.SmartPhone.Status.ToString(),
                user.SmartPhone.WelcomeMessageText,
            },
        });
    }
}
