namespace SyllabusAI.Models;

/// <summary>
/// Öğrencinin derse tek seferlik geri bildirimi (dönem sonu anket mantığı).
/// </summary>
public class CourseFeedback
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int StudentUserId { get; set; }
    public User Student { get; set; } = null!;
    /// <summary>1–5</summary>
    public byte Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
}
