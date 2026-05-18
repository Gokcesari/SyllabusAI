namespace SyllabusAI.Models;

/// <summary>
/// Ogrencinin haftalik ders geri bildirimi (tek ders + tek ogrenci + tek pencere icin bir gonderim).
/// </summary>
public class CourseWeeklyFeedback
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int StudentUserId { get; set; }
    public User Student { get; set; } = null!;
    public byte Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<CourseWeeklyFeedbackAnswer> Answers { get; set; } = new List<CourseWeeklyFeedbackAnswer>();
}
