namespace SyllabusAI.Models;

/// <summary>
/// Haftalik ders geri bildirimi icin sabit soru seti.
/// </summary>
public class WeeklyFeedbackQuestion
{
    public int Id { get; set; }
    public int QuestionNo { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<CourseWeeklyFeedbackAnswer> Answers { get; set; } = new List<CourseWeeklyFeedbackAnswer>();
}
