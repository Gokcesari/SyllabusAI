namespace SyllabusAI.Services;

/// <summary>PDF veya Word (.docx) syllabus dosyasından düz metin çıkarır.</summary>
public interface ISyllabusFileTextExtractor
{
    /// <summary>.pdf veya .docx için true ve metin; diğer uzantılar için false.</summary>
    bool TryExtract(ReadOnlyMemory<byte> bytes, string originalFileName, out string text);
}
