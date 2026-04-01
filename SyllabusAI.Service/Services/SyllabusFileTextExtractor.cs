using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace SyllabusAI.Services;

public class SyllabusFileTextExtractor : ISyllabusFileTextExtractor
{
    private readonly IPdfTextExtractor _pdf;

    public SyllabusFileTextExtractor(IPdfTextExtractor pdf) => _pdf = pdf;

    public bool TryExtract(ReadOnlyMemory<byte> bytes, string originalFileName, out string text)
    {
        text = string.Empty;
        var ext = Path.GetExtension(originalFileName ?? "").ToLowerInvariant();
        if (ext == ".pdf")
        {
            text = _pdf.ExtractText(bytes).Trim();
            return true;
        }

        if (ext == ".docx")
        {
            text = ExtractDocx(bytes.ToArray()).Trim();
            return true;
        }

        return false;
    }

    private static string ExtractDocx(byte[] raw)
    {
        try
        {
            using var ms = new MemoryStream(raw, writable: false);
            using var doc = WordprocessingDocument.Open(ms, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return string.Empty;

            var sb = new StringBuilder();
            foreach (var para in body.Elements<Paragraph>())
            {
                foreach (var t in para.Descendants<Text>())
                    sb.Append(t.Text);
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }
}
