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
}
