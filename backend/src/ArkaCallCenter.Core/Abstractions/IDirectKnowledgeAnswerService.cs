namespace ArkaCallCenter.Core.Abstractions;

/// <summary>نتیجهٔ پاسخ‌گویی مستقیم با استفاده از کل پایگاه دانش تأییدشدهٔ کاربر.</summary>
public enum DirectKnowledgeOutcome
{
    /// <summary>پاسخ با شاهد عینی از پایگاه دانش تولید شده است.</summary>
    Answered,

    /// <summary>پرسش در حوزهٔ کسب‌وکار است، اما پایگاه دانش پاسخ مستندی ندارد.</summary>
    InDomainUnknown,

    /// <summary>پرسش به‌وضوح خارج از حوزهٔ کسب‌وکار است.</summary>
    OutOfDomain,

    /// <summary>پایگاه دانش تأییدشده و قابل استفاده‌ای وجود ندارد.</summary>
    KnowledgeBaseEmpty,

    /// <summary>کل پایگاه دانش از حد امن ورودی مدل بزرگ‌تر است و کوتاه نشده است.</summary>
    KnowledgeBaseTooLarge,

    /// <summary>به‌دلیل اختلال یا پاسخ نامعتبر سرویس هوش مصنوعی، نتیجهٔ قابل اتکایی تولید نشد.</summary>
    ServiceUnavailable,
}

public sealed record DirectKnowledgeAnswer(
    DirectKnowledgeOutcome Outcome,
    string AnswerText,
    IReadOnlyList<string> Evidence,
    string? ScopeDescription = null)
{
    public bool Answered => Outcome == DirectKnowledgeOutcome.Answered;
}

/// <summary>
/// پرسش را همراه با کل متن پایگاه دانش تأییدشده به مدل می‌دهد. این مسیر هیچ chunk،
/// embedding یا بازیابی RAG انجام نمی‌دهد و پاسخ مستند را همان‌جا تولید می‌کند.
/// </summary>
public interface IDirectKnowledgeAnswerService
{
    Task<DirectKnowledgeAnswer> AnswerAsync(
        int userId,
        string question,
        int accuracyPercent,
        CancellationToken ct = default);
}
