namespace SyllabusAI.DTOs;

public class ChatRequest
{
    public int CourseId { get; set; }
    public string Question { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Answer { get; set; } = string.Empty;
    public bool FromSyllabus { get; set; }
    public bool IsOutOfScope { get; set; }

    /// <summary>lexical | embedding | none</summary>
    public string RetrievalMethod { get; set; } = "none";

    /// <summary>Arama sonucu kullanılan kısa alıntılar (UI).</summary>
    public List<string> SourceSnippets { get; set; } = new();

    /// <summary>Eşleşme zayıfsa veya genel yanıt verildiyse true.</summary>
    public bool FallbackTriggered { get; set; }
}
