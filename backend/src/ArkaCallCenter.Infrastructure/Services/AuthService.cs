using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArkaCallCenter.Infrastructure.Services;

public class AuthService : IAuthService
{
    private const int OtpTtlMinutes = 2;
    private const int MaxVerifyAttempts = 5;

    private readonly ArkaDbContext _db;
    private readonly ISmsSender _sms;
    private readonly IVoiceCaller _voice;
    private readonly ITokenService _tokens;
    private readonly ILogger<AuthService> _logger;

    public AuthService(ArkaDbContext db, ISmsSender sms, IVoiceCaller voice, ITokenService tokens, ILogger<AuthService> logger)
    {
        _db = db;
        _sms = sms;
        _voice = voice;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<(bool ok, string? error)> RequestOtpAsync(string phoneNumber, CancellationToken ct = default)
    {
        phoneNumber = NormalizePhone(phoneNumber);
        if (!IsValidIranianMobile(phoneNumber))
            return (false, "شماره موبایل نامعتبر است.");

        // جلوگیری از ارسال مکرر: اگر کد فعالِ کمتر از ۶۰ ثانیه وجود دارد، دوباره نساز.
        var recent = await _db.OtpCodes
            .Where(x => x.PhoneNumber == phoneNumber && !x.Consumed && x.CreatedAt > DateTime.UtcNow.AddSeconds(-60))
            .AnyAsync(ct);
        if (recent)
            return (false, "کد قبلاً ارسال شده است؛ کمی صبر کنید.");

        var code = Random.Shared.Next(100000, 1000000).ToString();
        _db.OtpCodes.Add(new OtpCode
        {
            PhoneNumber = phoneNumber,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(OtpTtlMinutes),
        });
        await _db.SaveChangesAsync(ct);

        // ارسال کد از طریق قالب verify سرویس SMS.ir.
        await _sms.SendVerifyCodeAsync(phoneNumber, code, ct);
        return (true, null);
    }

    public async Task<(bool ok, string? error)> RequestOtpByCallAsync(string phoneNumber, CancellationToken ct = default)
    {
        phoneNumber = NormalizePhone(phoneNumber);
        if (!IsValidIranianMobile(phoneNumber))
            return (false, "شماره موبایل نامعتبر است.");

        // کدِ فعالِ موجود (همان کدی که با پیامک رفته) را بردار؛ اگر نبود، بساز.
        var otp = await _db.OtpCodes
            .Where(x => x.PhoneNumber == phoneNumber && !x.Consumed && x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (otp is null)
        {
            var code = Random.Shared.Next(100000, 1000000).ToString();
            otp = new OtpCode
            {
                PhoneNumber = phoneNumber,
                Code = code,
                ExpiresAt = DateTime.UtcNow.AddMinutes(OtpTtlMinutes),
            };
            _db.OtpCodes.Add(otp);
            await _db.SaveChangesAsync(ct);
        }

        // با فایل‌های صوتیِ ضبط‌شده خوانده می‌شود: صدای اولیه + ارقام + «مجدداً…» + ارقام.
        var ok = await _voice.CallOtpAsync(phoneNumber, otp.Code, ct);
        return ok ? (true, null) : (false, "برقراری تماس ممکن نشد؛ لطفاً دوباره تلاش کنید.");
    }

    public async Task<VerifyOtpResult> VerifyOtpAsync(string phoneNumber, string code, CancellationToken ct = default)
    {
        phoneNumber = NormalizePhone(phoneNumber);
        var otp = await _db.OtpCodes
            .Where(x => x.PhoneNumber == phoneNumber && !x.Consumed)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (otp is null)
            return new VerifyOtpResult(false, null, false, false, "کدی برای این شماره یافت نشد.");
        if (otp.ExpiresAt < DateTime.UtcNow)
            return new VerifyOtpResult(false, null, false, false, "کد منقضی شده است.");
        if (otp.Attempts >= MaxVerifyAttempts)
            return new VerifyOtpResult(false, null, false, false, "تعداد تلاش‌ها بیش از حد مجاز است.");

        otp.Attempts++;
        if (otp.Code != code)
        {
            await _db.SaveChangesAsync(ct);
            return new VerifyOtpResult(false, null, false, false, "کد وارد شده نادرست است.");
        }

        otp.Consumed = true;

        var user = await _db.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber, ct);
        var isNew = user is null;
        if (user is not null && !user.IsActive)
        {
            // کد مصرف‌شده تلقی می‌شود ولی ورود مجاز نیست (حساب غیرفعال‌شده توسط ادمین).
            await _db.SaveChangesAsync(ct);
            return new VerifyOtpResult(false, null, false, false, "حساب کاربری شما غیرفعال است. با پشتیبانی تماس بگیرید.");
        }
        if (user is null)
        {
            user = new User { PhoneNumber = phoneNumber, ProfileCompleted = false };
            _db.Users.Add(user);
        }
        await _db.SaveChangesAsync(ct);

        var token = _tokens.CreateToken(user);
        return new VerifyOtpResult(true, token, isNew, user.ProfileCompleted, null);
    }

    public async Task<User?> CompleteProfileAsync(int userId, string firstName, string lastName, string brandName, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return null;

        user.FirstName = firstName.Trim();
        user.LastName = lastName.Trim();
        user.BrandName = brandName.Trim();
        user.ProfileCompleted = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<(bool ok, string? error)> RequestPhoneChangeAsync(int userId, string newPhone, CancellationToken ct = default)
    {
        newPhone = NormalizePhone(newPhone);
        if (!IsValidIranianMobile(newPhone))
            return (false, "شماره موبایل جدید نامعتبر است.");

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return (false, "کاربر یافت نشد.");
        if (user.PhoneNumber == newPhone) return (false, "شماره جدید با شماره فعلی یکسان است.");
        if (await _db.Users.AnyAsync(x => x.PhoneNumber == newPhone && x.Id != userId, ct))
            return (false, "این شماره قبلاً توسط کاربر دیگری ثبت شده است.");

        var recent = await _db.OtpCodes
            .Where(x => x.PhoneNumber == newPhone && !x.Consumed && x.CreatedAt > DateTime.UtcNow.AddSeconds(-60))
            .AnyAsync(ct);
        if (recent) return (false, "کد قبلاً ارسال شده است؛ کمی صبر کنید.");

        var code = Random.Shared.Next(100000, 1000000).ToString();
        _db.OtpCodes.Add(new OtpCode
        {
            PhoneNumber = newPhone,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(OtpTtlMinutes),
        });
        await _db.SaveChangesAsync(ct);

        // کد به شماره‌ی جدید (از طریق قالب verify) ارسال می‌شود تا مالکیت آن تأیید شود.
        await _sms.SendVerifyCodeAsync(newPhone, code, ct);
        return (true, null);
    }

    public async Task<(bool ok, string? error)> ConfirmPhoneChangeAsync(int userId, string newPhone, string code, CancellationToken ct = default)
    {
        newPhone = NormalizePhone(newPhone);
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return (false, "کاربر یافت نشد.");

        var otp = await _db.OtpCodes
            .Where(x => x.PhoneNumber == newPhone && !x.Consumed)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (otp is null) return (false, "کدی برای این شماره یافت نشد.");
        if (otp.ExpiresAt < DateTime.UtcNow) return (false, "کد منقضی شده است.");
        if (otp.Attempts >= MaxVerifyAttempts) return (false, "تعداد تلاش‌ها بیش از حد مجاز است.");

        otp.Attempts++;
        if (otp.Code != code)
        {
            await _db.SaveChangesAsync(ct);
            return (false, "کد وارد شده نادرست است.");
        }

        // بازبینی نهایی یکتایی (رقابت احتمالی)
        if (await _db.Users.AnyAsync(x => x.PhoneNumber == newPhone && x.Id != userId, ct))
            return (false, "این شماره در این فاصله توسط کاربر دیگری ثبت شد.");

        otp.Consumed = true;
        user.PhoneNumber = newPhone;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    // ---- helpers ----
    private static string NormalizePhone(string phone)
    {
        phone = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (phone.StartsWith("0098")) phone = "0" + phone[4..];
        else if (phone.StartsWith("98") && phone.Length == 12) phone = "0" + phone[2..];
        else if (phone.StartsWith("9") && phone.Length == 10) phone = "0" + phone;
        return phone;
    }

    private static bool IsValidIranianMobile(string phone) =>
        phone.Length == 11 && phone.StartsWith("09");
}
