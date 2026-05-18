namespace SyllabusAI.Models;

public class SyllabusChunk
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;

    public string? OriginalSectionTitle { get; set; }
    public string NormalizedCategory { get; set; } = "unknown";
    public int? PageStart { get; set; }
    public int? PageEnd { get; set; }

    // JSON array [0.1, 0.2, ...]
    public string? EmbeddingJson { get; set; }
}
