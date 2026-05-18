namespace SyllabusAI.Models;

/// <summary>
/// Tek bir haftalik geri bildirimdeki soru puanlari (1-5).
/// </summary>
public class CourseWeeklyFeedbackAnswer
{
    public int Id { get; set; }
    public int CourseWeeklyFeedbackId { get; set; }
    public CourseWeeklyFeedback CourseWeeklyFeedback { get; set; } = null!;
    public int WeeklyFeedbackQuestionId { get; set; }
    public WeeklyFeedbackQuestion WeeklyFeedbackQuestion { get; set; } = null!;
    public byte Rating { get; set; }
}
