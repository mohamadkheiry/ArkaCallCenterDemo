using System.Text;
using ArkaCallCenter.Core.Abstractions;
using UglyToad.PdfPig;

namespace ArkaCallCenter.Infrastructure.Services;

/// <summary>استخراج متن از فایل‌های txt و pdf.</summary>
public class FileTextExtractor : IFileTextExtractor
{
    public bool CanHandle(string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".txt" or ".pdf";
    }

    public async Task<string> ExtractAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        // به حافظه کپی می‌کنیم تا PdfPig بتواند seek کند.
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;

        return ext switch
        {
            ".txt" => Encoding.UTF8.GetString(ms.ToArray()),
            ".pdf" => ExtractPdf(ms),
            _ => throw new NotSupportedException($"نوع فایل پشتیبانی نمی‌شود: {ext}"),
        };
    }

    private static string ExtractPdf(Stream stream)
    {
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(stream);
        foreach (var page in doc.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString().Trim();
    }
}
