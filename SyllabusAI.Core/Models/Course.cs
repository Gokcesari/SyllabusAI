namespace SyllabusAI.Models;

public class Course
{
    public int Id { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SyllabusContent { get; set; } = string.Empty;
    public string? HighlightKeywords { get; set; }
    public int InstructorId { get; set; }
    public User Instructor { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public DateTime? FeedbackOpensAtUtc { get; set; }
    public DateTime? FeedbackClosesAtUtc { get; set; }
    public DateTime? WeeklyFeedbackOpensAtUtc { get; set; }
    public DateTime? WeeklyFeedbackClosesAtUtc { get; set; }

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<SyllabusPdfUpload> SyllabusPdfUploads { get; set; } = new List<SyllabusPdfUpload>();
    public ICollection<SyllabusChunk> SyllabusChunks { get; set; } = new List<SyllabusChunk>();
    public ICollection<CourseFeedback> Feedbacks { get; set; } = new List<CourseFeedback>();
    public ICollection<CourseWeeklyFeedback> WeeklyFeedbacks { get; set; } = new List<CourseWeeklyFeedback>();
    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
}
