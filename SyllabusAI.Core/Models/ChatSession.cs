namespace SyllabusAI.Models;

public class ChatSession
{
    public int Id { get; set; }
    public int StudentUserId { get; set; }
    public User StudentUser { get; set; } = null!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
