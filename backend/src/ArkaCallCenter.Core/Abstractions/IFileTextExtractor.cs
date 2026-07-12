namespace ArkaCallCenter.Core.Abstractions;

/// <summary>استخراج متن از فایل‌های txt و pdf.</summary>
public interface IFileTextExtractor
{
    bool CanHandle(string fileName, string contentType);
    Task<string> ExtractAsync(Stream stream, string fileName, CancellationToken ct = default);
}
