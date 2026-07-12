using ArkaCallCenter.Core.Entities;

namespace ArkaCallCenter.Core.Abstractions;

public record RagHit(string Content, double Score);

public record RagAnswer(bool Found, IReadOnlyList<RagHit> Hits, string Context);

/// <summary>
/// سیستم RAG: ساخت chunk و embedding از پایگاه دانش و بازیابی مرتبط‌ترین بخش‌ها.
/// </summary>
public interface IRagService
{
    /// <summary>متن پایگاه دانش را chunk و embedding می‌کند و chunkها را ذخیره می‌کند.</summary>
    Task IndexAsync(KnowledgeBase kb, CancellationToken ct = default);

    /// <summary>
    /// مرتبط‌ترین chunkها را برای یک پرسش بازیابی می‌کند. اگر بیشترین شباهت زیر
    /// آستانه باشد، Found=false برمی‌گردد (یعنی پاسخ در پایگاه دانش نیست).
    /// </summary>
    Task<RagAnswer> RetrieveAsync(int userId, string query, CancellationToken ct = default);
}
