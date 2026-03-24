namespace SyllabusAI.Models;

/// <summary>
/// Eğitmenin eklediği ders. Ders kodu ile öğrenci eşleşir; her dersin müfredat metni ayrı tutulur.
/// </summary>
public class Course
{
    public int Id { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>Müfredat / syllabus metni (öğrenci inceleyecek, AI bu metne göre cevap verecek)</summary>
    public string SyllabusContent { get; set; } = string.Empty;
    /// <summary>Öne çıkarılacak bölümler (örn. sınav tarihi, devam) - JSON veya virgülle ayrılmış anahtar kelimeler</summary>
    public string? HighlightKeywords { get; set; }
    public int InstructorId { get; set; }
    public User Instructor { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
