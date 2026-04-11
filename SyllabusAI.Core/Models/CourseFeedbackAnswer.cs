namespace SyllabusAI.Models;

/// <summary>
/// Tek bir geri bildirimdeki soru puanlari (1-5).
/// </summary>
public class CourseFeedbackAnswer
{
    public int Id { get; set; }
    public int CourseFeedbackId { get; set; }
    public CourseFeedback CourseFeedback { get; set; } = null!;
    public int FeedbackQuestionId { get; set; }
    public FeedbackQuestion FeedbackQuestion { get; set; } = null!;
    public byte Rating { get; set; }
}
