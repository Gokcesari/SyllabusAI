namespace SyllabusAI.Models;

/// <summary>
/// Öğrencinin ders kodu girerek eklediği (yıldızladığı) ders kaydı. Veritabanına yazılır.
/// </summary>
public class Enrollment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
}
