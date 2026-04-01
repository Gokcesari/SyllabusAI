namespace SyllabusAI.Models;

/// <summary>
/// RAG için müfredat parçası; metin + isteğe bağlı embedding (OpenAI anahtarı varsa doldurulur).
/// </summary>
public class SyllabusChunk
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;

    /// <summary>OpenAI embedding vektörü JSON dizisi [0.1,0.2,...]; yoksa sözcük eşleşmesi kullanılır.</summary>
    public string? EmbeddingJson { get; set; }
}
