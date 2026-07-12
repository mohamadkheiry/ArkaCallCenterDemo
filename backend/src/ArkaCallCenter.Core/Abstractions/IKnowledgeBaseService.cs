using ArkaCallCenter.Core.Entities;

namespace ArkaCallCenter.Core.Abstractions;

public record KbResult(bool Ok, string? Error, KnowledgeBase? Kb);

public interface IKnowledgeBaseService
{
    Task<KnowledgeBase?> GetAsync(int userId, CancellationToken ct = default);

    /// <summary>ثبت/به‌روزرسانی پایگاه دانش از متن (≤۲۰۰۰ کاراکتر).</summary>
    Task<KbResult> SetTextAsync(int userId, string text, CancellationToken ct = default);

    /// <summary>ثبت/به‌روزرسانی پایگاه دانش از فایل txt/pdf (≤۱۰۰KB).</summary>
    Task<KbResult> SetFileAsync(int userId, string fileName, string contentType, Stream content, long sizeBytes, CancellationToken ct = default);

    Task DeleteAsync(int userId, CancellationToken ct = default);
}
