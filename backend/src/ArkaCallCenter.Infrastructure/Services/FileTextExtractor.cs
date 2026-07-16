using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using ArkaCallCenter.Core.Abstractions;

namespace ArkaCallCenter.Infrastructure.Services;

/// <summary>استخراج متن از فایل‌های txt و Word (docx).</summary>
public class FileTextExtractor : IFileTextExtractor
{
    public bool CanHandle(string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".txt" or ".docx";
    }

    public async Task<string> ExtractAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;

        return ext switch
        {
            ".txt" => Encoding.UTF8.GetString(ms.ToArray()),
            ".docx" => ExtractDocx(ms),
            _ => throw new NotSupportedException($"نوع فایل پشتیبانی نمی‌شود: {ext}"),
        };
    }

    /// <summary>docx یک ZIP است؛ متن از word/document.xml (عناصر w:t) استخراج می‌شود.</summary>
    private static string ExtractDocx(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = zip.GetEntry("word/document.xml")
                    ?? throw new NotSupportedException("فایل Word معتبر نیست.");
        using var es = entry.Open();
        var doc = XDocument.Load(es);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        var sb = new StringBuilder();
        foreach (var para in doc.Descendants(w + "p"))
        {
            foreach (var t in para.Descendants(w + "t"))
                sb.Append(t.Value);
            // شکستِ خط بین پاراگراف‌ها
            sb.Append('\n');
        }
        return sb.ToString().Trim();
    }
}
