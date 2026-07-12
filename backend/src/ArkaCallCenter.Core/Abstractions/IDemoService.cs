namespace ArkaCallCenter.Core.Abstractions;

public record DemoInfo(
    int Id, string? Label, int? Extension, string Status,
    string? WelcomeText, string? KbText, string? VoiceName,
    int? CallMinuteLimit, int UsedMinutes, bool IsActive);

public record DemoResult(bool Ok, string? Error, DemoInfo? Demo);

/// <summary>
/// مدیریت دموها توسط سوپرادمین. هر دمو یک پروفایل کامل (داخلی ۱–۹۹۹ + پیام خوش‌آمد +
/// پایگاه دانش + گوینده + محدودیت) است که سوپرادمین می‌سازد و کنترل می‌کند.
/// </summary>
public interface IDemoService
{
    Task<IReadOnlyList<DemoInfo>> ListAsync(CancellationToken ct = default);
    Task<DemoResult> CreateAsync(string label, string welcomeText, string kbText,
        string? voice, int? minuteLimit, CancellationToken ct = default);
    Task<DemoResult> UpdateAsync(int id, string? label, string? welcomeText, string? kbText,
        string? voice, int? minuteLimit, bool? isActive, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
