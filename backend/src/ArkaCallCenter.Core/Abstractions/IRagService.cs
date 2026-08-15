using ArkaCallCenter.Core.Entities;

namespace ArkaCallCenter.Core.Abstractions;

public record RagHit(string Content, double Score);

/// <summary>نتیجهٔ مسیریابی یک پرسش پس از جست‌وجوی پایگاه دانش.</summary>
public enum RagOutcome
{
    /// <summary>یک یا چند قطعهٔ معتبر برای ساخت پاسخ پیدا شد.</summary>
    AnswerCandidate,

    /// <summary>پرسش در حوزهٔ کسب‌وکار است، اما پاسخ قابل اتکایی در محتوا پیدا نشد.</summary>
    InDomainUnknown,

    /// <summary>پرسش به‌وضوح خارج از حوزهٔ همین کسب‌وکار است.</summary>
    OutOfDomain,

    /// <summary>کاربر هنوز پایگاه دانش قابل استفاده‌ای ندارد.</summary>
    KnowledgeBaseEmpty,

    /// <summary>به‌دلیل اختلال فنی، نتیجهٔ قابل اتکایی از بازیابی به دست نیامد.</summary>
    RetrievalUnavailable,
}

public record RagAnswer(
    RagOutcome Outcome,
    IReadOnlyList<RagHit> Hits,
    string Context,
    string? ScopeDescription = null)
{
    public bool Found => Outcome == RagOutcome.AnswerCandidate;
}

/// <summary>
/// سیستم RAG: ساخت chunk و embedding از پایگاه دانش و بازیابی مرتبط‌ترین بخش‌ها.
/// </summary>
public interface IRagService
{
    /// <summary>متن پایگاه دانش را chunk و embedding می‌کند و chunkها را ذخیره می‌کند.</summary>
    Task IndexAsync(KnowledgeBase kb, CancellationToken ct = default);

    /// <summary>
    /// در صورت قدیمی‌بودن یا تفاوت مدل embedding، ایندکس کاربر را با مدل فعلی بازسازی می‌کند.
    /// </summary>
    Task EnsureIndexAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// مرتبط‌ترین chunkها را برای یک پرسش بازیابی می‌کند و بین «خارج از حوزه»،
    /// «مرتبط اما بی‌پاسخ» و «قطعهٔ پاسخ‌گو» تفاوت می‌گذارد.
    /// </summary>
    Task<RagAnswer> RetrieveAsync(int userId, string query, CancellationToken ct = default);
}
