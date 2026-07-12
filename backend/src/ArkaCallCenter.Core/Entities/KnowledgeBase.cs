using ArkaCallCenter.Core.Common;
using ArkaCallCenter.Core.Enums;

namespace ArkaCallCenter.Core.Entities;

/// <summary>
/// پایگاه دانش هر کاربر. طبق قوانین کسب‌وکار، هر کاربر فقط یک منبع فعال دارد:
/// یا متن (≤۲۰۰۰ کاراکتر) یا فایل txt/pdf (≤۱۰۰KB).
/// </summary>
public class KnowledgeBase : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = default!;

    public KbSourceType SourceType { get; set; }

    /// <summary>محتوای متنی (چه متنِ مستقیم، چه متنِ استخراج‌شده از فایل).</summary>
    public string? RawText { get; set; }

    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public long FileSizeBytes { get; set; }
    public int CharCount { get; set; }

    public ModerationStatus ModerationStatus { get; set; } = ModerationStatus.Pending;

    /// <summary>دلیل رد شدن در صورت مغایرت با قوانین.</summary>
    public string? ModerationReason { get; set; }

    public ICollection<KnowledgeChunk> Chunks { get; set; } = new List<KnowledgeChunk>();
}
