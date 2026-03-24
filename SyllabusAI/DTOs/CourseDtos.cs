namespace SyllabusAI.DTOs;

public class CreateCourseRequest
{
    public string CourseCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SyllabusContent { get; set; } = string.Empty;
    public string? HighlightKeywords { get; set; }
}

public class CourseDto
{
    public int Id { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? InstructorName { get; set; }
    public bool HasSyllabus { get; set; }
}

public class SyllabusDto
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SyllabusContent { get; set; } = string.Empty;
    public string[]? HighlightKeywords { get; set; }
}

public class EnrollByCodeRequest
{
    public string CourseCode { get; set; } = string.Empty;
}
