namespace SyllabusAI.Models;

public class SyllabusPdfUpload
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredRelativePath { get; set; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
