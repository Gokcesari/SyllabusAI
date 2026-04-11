namespace SyllabusAI.Models;

/// <summary>
/// Tum dersler icin ortak, sabit anket soru seti.
/// </summary>
public class FeedbackQuestion
{
    public int Id { get; set; }
    public int QuestionNo { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<CourseFeedbackAnswer> Answers { get; set; } = new List<CourseFeedbackAnswer>();
}
