namespace SyllabusAI.Services;

public interface IPdfTextExtractor
{
    /// <summary>PDF baytlarından düz metin çıkarır; başarısız veya boş sayfa durumunda boş string dönebilir.</summary>
    string ExtractText(ReadOnlyMemory<byte> pdfBytes);
}
