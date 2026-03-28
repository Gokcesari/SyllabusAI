using System.Text;
using UglyToad.PdfPig;

namespace SyllabusAI.Services;

public class PdfPigTextExtractor : IPdfTextExtractor
{
    public string ExtractText(ReadOnlyMemory<byte> pdfBytes)
    {
        try
        {
            using var ms = new MemoryStream(pdfBytes.ToArray());
            using var document = PdfDocument.Open(ms);
            var sb = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                var t = page.Text;
                if (!string.IsNullOrWhiteSpace(t))
                    sb.AppendLine(t);
            }
            return sb.ToString().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }
}
