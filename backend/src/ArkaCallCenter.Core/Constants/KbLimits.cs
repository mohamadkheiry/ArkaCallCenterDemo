namespace ArkaCallCenter.Core.Constants;

/// <summary>محدودیت‌های پایگاه دانش طبق قوانین کسب‌وکار.</summary>
public static class KbLimits
{
    public const int MaxTextChars = 2000;
    public const long MaxFileBytes = 100 * 1024; // 100KB
    // در مسیر پاسخ مستقیم، کل متن بدون کوتاه‌سازی به مدل داده می‌شود. این سقف
    // بزرگ‌ترین پایگاه فعلی را پوشش می‌دهد و از ورودی کنترل‌نشده جلوگیری می‌کند.
    public const int MaxDirectKnowledgeChars = 90_000;
    public static readonly string[] AllowedExtensions = { ".txt", ".docx" };

    // chunking
    public const int ChunkSize = 500;
    public const int ChunkOverlap = 80;
}
