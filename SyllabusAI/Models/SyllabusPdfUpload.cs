namespace SyllabusAI.Models;

/// <summary>
/// Eğitmenin yüklediği syllabus PDF kaydı; ham dosya yolu ve çıkarılan metin.
/// </summary>
public class SyllabusPdfUpload
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public string OriginalFileName { get; set; } = string.Empty;
    /// <summary>Uygulama köküne göre relatif yol, örn. Data/Uploads/12/abc.pdf</summary>
    public string StoredRelativePath { get; set; } = string.Empty;

    /// <summary>PDF'ten çıkarılan düz metin (sonraki aşamada parser burayı bölecek).</summary>
    public string ExtractedText { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
