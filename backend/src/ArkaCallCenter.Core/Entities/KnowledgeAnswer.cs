using ArkaCallCenter.Core.Common;
using ArkaCallCenter.Core.Enums;

namespace ArkaCallCenter.Core.Entities;

/// <summary>
/// یک سؤال و پاسخ مستقل در پایگاه دانش کاربر. پاسخ صوتی هنگام ذخیره ساخته می‌شود
/// تا در تماس هیچ مدل زبانی پاسخ تازه‌ای تولید نکند.
/// </summary>
public class KnowledgeAnswer : BaseEntity
{
    public int KnowledgeBaseId { get; set; }
    public KnowledgeBase KnowledgeBase { get; set; } = default!;

    public string Question { get; set; } = default!;
    public string NormalizedQuestion { get; set; } = default!;
    public string Answer { get; set; } = default!;
    public int SortOrder { get; set; }

    public string? AudioPath { get; set; }
    public string? AudioHash { get; set; }
    public KnowledgeAnswerAudioStatus AudioStatus { get; set; } = KnowledgeAnswerAudioStatus.Pending;
    public string? AudioError { get; set; }
}
