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
    /// <summary>Eğitmen listesi: geri bildirim zaman penceresi (UTC). İkisi de null ise kapalı.</summary>
    public DateTime? FeedbackOpensAtUtc { get; set; }
    public DateTime? FeedbackClosesAtUtc { get; set; }
    /// <summary>Eğitmen: gelen geri bildirim sayısı.</summary>
    public int FeedbackResponseCount { get; set; }
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

/// <summary>Öğrenci: kayıtlı derste indirilebilir/önizlenebilir son yüklenen dosya (PDF veya docx).</summary>
public record SyllabusFileStreamDto(byte[] Bytes, string ContentType, string FileName);

/// <summary>Öğrenci: bu derste geri bildirim gönderebilir mi?</summary>
public class CourseFeedbackStatusDto
{
    public bool WindowConfigured { get; set; }
    public bool WindowOpen { get; set; }
    public DateTime? OpensAtUtc { get; set; }
    public DateTime? ClosesAtUtc { get; set; }
    public bool HasSubmitted { get; set; }
    public byte? MyRating { get; set; }
    public string? MyComment { get; set; }
    public string? Message { get; set; }
}

public class SubmitCourseFeedbackRequest
{
    public byte Rating { get; set; }
    public string? Comment { get; set; }
}

/// <summary>Eğitmen: geri bildirim penceresini ayarla (UTC ISO). İkisini de null göndermek pencereyi kapatır.</summary>
public class SetCourseFeedbackWindowRequest
{
    public DateTime? OpensAtUtc { get; set; }
    public DateTime? ClosesAtUtc { get; set; }
}

public class CourseFeedbackItemDto
{
    public int Id { get; set; }
    public string StudentEmail { get; set; } = string.Empty;
    public string? StudentName { get; set; }
    public byte Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
}

public class SyllabusPdfUploadResponseDto
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    /// <summary>pdf | docx</summary>
    public string FileKind { get; set; } = "pdf";
    public int ExtractedCharacterCount { get; set; }

    /// <summary>RAG için oluşturulan metin parça sayısı (0 ise metin boş veya indekslenemedi).</summary>
    public int RagChunkCount { get; set; }

    public DateTime UploadedAtUtc { get; set; }
}
