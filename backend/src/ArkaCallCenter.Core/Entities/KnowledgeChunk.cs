using ArkaCallCenter.Core.Common;

namespace ArkaCallCenter.Core.Entities;

/// <summary>
/// یک تکه از پایگاه دانش به‌همراه بردار embedding آن (برای RAG).
/// بردار به‌صورت JSON آرایه‌ی float ذخیره می‌شود؛ چون حجم KB کوچک است،
/// شباهت cosine در حافظه محاسبه می‌شود.
/// </summary>
public class KnowledgeChunk : BaseEntity
{
    public int KnowledgeBaseId { get; set; }
    public KnowledgeBase KnowledgeBase { get; set; } = default!;

    public int ChunkIndex { get; set; }
    public string Content { get; set; } = default!;

    /// <summary>بردار embedding به‌صورت JSON (float[]).</summary>
    public string EmbeddingJson { get; set; } = "[]";
}
