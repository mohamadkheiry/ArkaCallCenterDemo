using ArkaCallCenter.Api.Extensions;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ArkaCallCenter.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ControllerBase
{
    private static readonly string[] AllowedImageExt = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly ArkaDbContext _db;
    private readonly IAuthService _auth;
    private readonly ISettingsService _settings;
    private readonly string _uploadsPath;

    public MeController(ArkaDbContext db, IAuthService auth, ISettingsService settings, IConfiguration config)
    {
        _db = db;
        _auth = auth;
        _settings = settings;
        _uploadsPath = config["Storage:UploadsPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(_uploadsPath);
    }

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

        // شماره‌ی پذیرش که کاربر باید ابتدا با آن تماس بگیرد (پیش‌فرض 02191008288).
        var receptionNumber = await _settings.GetAsync(SettingKeys.ReceptionNumber, "02191008288", ct);

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
            receptionNumber,
            hasAvatar = !string.IsNullOrEmpty(user.AvatarPath),
            smartPhone = user.SmartPhone == null ? null : new
            {
                user.SmartPhone.Extension,
                status = user.SmartPhone.Status.ToString(),
                user.SmartPhone.WelcomeMessageText,
            },
        });
    }

    public record SetVoiceRequest(string VoiceName);

    /// <summary>انتخاب گوینده‌ی صدای کاربر (باید از گوینده‌های فعال باشد).</summary>
    [HttpPut("voice")]
    public async Task<IActionResult> SetVoice(SetVoiceRequest req, CancellationToken ct)
    {
        var exists = await _db.VoiceOptions.AnyAsync(v => v.Enabled && v.Name == req.VoiceName, ct);
        if (!exists) return BadRequest(new { error = "گوینده‌ی انتخابی معتبر نیست." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == User.GetUserId(), ct);
        if (user is null) return NotFound();
        user.VoiceName = req.VoiceName;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { user.VoiceName });
    }

    // ---------------- Phone change (with OTP to the new number) ----------------
    public record PhoneChangeRequest(string NewPhone);
    public record PhoneConfirmRequest(string NewPhone, string Code);

    [HttpPost("phone/request-change")]
    public async Task<IActionResult> RequestPhoneChange(PhoneChangeRequest req, CancellationToken ct)
    {
        var (ok, error) = await _auth.RequestPhoneChangeAsync(User.GetUserId(), req.NewPhone, ct);
        return ok ? Ok(new { message = "کد تأیید به شماره‌ی جدید ارسال شد." }) : BadRequest(new { error });
    }

    [HttpPost("phone/confirm-change")]
    public async Task<IActionResult> ConfirmPhoneChange(PhoneConfirmRequest req, CancellationToken ct)
    {
        var (ok, error) = await _auth.ConfirmPhoneChangeAsync(User.GetUserId(), req.NewPhone, req.Code, ct);
        return ok ? Ok(new { message = "شماره با موفقیت تغییر کرد." }) : BadRequest(new { error });
    }

    // ---------------- Avatar ----------------
    [HttpPost("avatar")]
    [RequestSizeLimit(4_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAvatar(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "فایلی ارسال نشد." });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExt.Contains(ext))
            return BadRequest(new { error = "فقط تصویر (jpg, png, webp) مجاز است." });
        if (file.Length > 3 * 1024 * 1024)
            return BadRequest(new { error = "حجم تصویر باید حداکثر ۳ مگابایت باشد." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == User.GetUserId(), ct);
        if (user is null) return NotFound();

        var stored = ext == ".jpeg" ? ".jpg" : ext;
        var path = Path.Combine(_uploadsPath, $"avatar_{user.Id}{stored}");
        // حذف نسخه‌های قبلی با پسوند دیگر
        foreach (var e in AllowedImageExt.Append(".jpg").Distinct())
        {
            var p = Path.Combine(_uploadsPath, $"avatar_{user.Id}{e}");
            if (p != path && System.IO.File.Exists(p)) try { System.IO.File.Delete(p); } catch { }
        }
        await using (var fs = System.IO.File.Create(path))
            await file.CopyToAsync(fs, ct);
        user.AvatarPath = path;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "تصویر پروفایل به‌روزرسانی شد." });
    }

    [HttpDelete("avatar")]
    public async Task<IActionResult> DeleteAvatar(CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == User.GetUserId(), ct);
        if (user is null) return NotFound();
        if (!string.IsNullOrEmpty(user.AvatarPath) && System.IO.File.Exists(user.AvatarPath))
            try { System.IO.File.Delete(user.AvatarPath); } catch { }
        user.AvatarPath = null;
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "تصویر پروفایل حذف شد." });
    }
}
