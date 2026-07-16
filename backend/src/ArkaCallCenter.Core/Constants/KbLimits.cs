namespace ArkaCallCenter.Core.Constants;

/// <summary>محدودیت‌های پایگاه دانش طبق قوانین کسب‌وکار.</summary>
public static class KbLimits
{
    public const int MaxTextChars = 2000;
    public const long MaxFileBytes = 100 * 1024; // 100KB
    public static readonly string[] AllowedExtensions = { ".txt", ".docx" };

    // chunking
    public const int ChunkSize = 500;
    public const int ChunkOverlap = 80;
}
