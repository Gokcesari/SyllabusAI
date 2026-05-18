namespace SyllabusAI.DTOs;

public class ChatRequest
{
    public int CourseId { get; set; }
    public int? SessionId { get; set; }
    public string Question { get; set; } = string.Empty;
    public List<ChatTurnDto> History { get; set; } = new();
}

public class ChatTurnDto
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

public class ChatResponse
{
    public int SessionId { get; set; }
    public string Answer { get; set; } = string.Empty;
    public bool FromSyllabus { get; set; }
    public bool IsOutOfScope { get; set; }

    // lexical | embedding | none
    public string RetrievalMethod { get; set; } = "none";

    public List<string> SourceSnippets { get; set; } = new();
    public List<string> SourceSections { get; set; } = new();
    public bool FallbackTriggered { get; set; }
}

public class ChatCourseAnalyticsDto
{
    public int CourseId { get; set; }
    public int TotalQuestions { get; set; }
    public int OutOfScopeQuestions { get; set; }
    public Dictionary<string, int> CategoryCounts { get; set; } = new();
}

public class RagEvalCaseDto
{
    public int CourseId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string ExpectedKeyword { get; set; } = string.Empty;
}

public class RagEvalResultItemDto
{
    public string Question { get; set; } = string.Empty;
    public string ExpectedKeyword { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Answer { get; set; } = string.Empty;
}

public class RagEvalSummaryDto
{
    public int Total { get; set; }
    public int Passed { get; set; }
    public List<RagEvalResultItemDto> Results { get; set; } = new();
}
