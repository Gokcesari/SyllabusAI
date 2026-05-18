namespace SyllabusAI.Models;

public class ChatMessage
{
    public int Id { get; set; }
    public int ChatSessionId { get; set; }
    public ChatSession ChatSession { get; set; } = null!;

    // user | assistant
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public string? RetrievedChunkIdsJson { get; set; }
    public string? RetrievedCategoriesJson { get; set; }
    public bool IsOutOfScope { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
