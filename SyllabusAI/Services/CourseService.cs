using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SyllabusAI.Data;
using SyllabusAI.DTOs;
using SyllabusAI.Models;
using System.IO;

namespace SyllabusAI.Services;

public class CourseService : ICourseService
{
    private const long MaxPdfBytes = 20 * 1024 * 1024;

    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ISyllabusFileTextExtractor _fileText;
    private readonly ISyllabusRagIndexService _ragIndex;

    public CourseService(ApplicationDbContext db, IWebHostEnvironment env, ISyllabusFileTextExtractor fileText, ISyllabusRagIndexService ragIndex)
    {
        _db = db;
        _env = env;
        _fileText = fileText;
        _ragIndex = ragIndex;
    }

    public async Task<CourseDto?> CreateCourseAsync(int instructorUserId, CreateCourseRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CourseCode)) return null;
        var exists = await _db.Courses.AnyAsync(c => c.InstructorId == instructorUserId && c.CourseCode == request.CourseCode.Trim(), ct);
        if (exists) return null;
        var course = new Course
        {
            CourseCode = request.CourseCode.Trim().ToUpperInvariant(),
            Title = request.Title?.Trim() ?? request.CourseCode,
            SyllabusContent = request.SyllabusContent ?? "",
            HighlightKeywords = request.HighlightKeywords?.Trim(),
            InstructorId = instructorUserId
        };
        _db.Courses.Add(course);
        await _db.SaveChangesAsync(ct);
        await _ragIndex.ReindexCourseAsync(course.Id, course.SyllabusContent, ct);
        return ToDto(course);
    }

    public async Task<List<CourseDto>> GetInstructorCoursesAsync(int instructorUserId, CancellationToken ct = default)
    {
        return await _db.Courses
            .AsNoTracking()
            .Where(c => c.InstructorId == instructorUserId)
            .OrderBy(c => c.CourseCode)
            .Select(c => new CourseDto
            {
                Id = c.Id,
                CourseCode = c.CourseCode,
                Title = c.Title,
                InstructorName = c.Instructor.FullName ?? c.Instructor.Email,
                HasSyllabus = !string.IsNullOrWhiteSpace(c.SyllabusContent),
                FeedbackOpensAtUtc = c.FeedbackOpensAtUtc,
                FeedbackClosesAtUtc = c.FeedbackClosesAtUtc,
                FeedbackResponseCount = c.Feedbacks.Count
            })
            .ToListAsync(ct);
    }

    public async Task<List<CourseDto>> GetMyEnrolledCoursesAsync(int studentUserId, CancellationToken ct = default)
    {
        var list = await _db.Enrollments
            .Where(e => e.UserId == studentUserId)
            .Include(e => e.Course).ThenInclude(c => c.Instructor)
            .OrderBy(e => e.Course.CourseCode)
            .Select(e => e.Course)
            .ToListAsync(ct);
        return list.Select(c => ToDto(c, 0)).ToList();
    }

    public async Task<EnrollResult> EnrollByCourseCodeAsync(int studentUserId, string courseCode, CancellationToken ct = default)
    {
        var code = courseCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code)) return EnrollResult.CourseNotFound;
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.CourseCode == code, ct);
        if (course == null) return EnrollResult.CourseNotFound;
        var already = await _db.Enrollments.AnyAsync(e => e.UserId == studentUserId && e.CourseId == course.Id, ct);
        if (already) return EnrollResult.AlreadyEnrolled;
        _db.Enrollments.Add(new Enrollment { UserId = studentUserId, CourseId = course.Id });
        await _db.SaveChangesAsync(ct);
        return EnrollResult.Ok;
    }

    public async Task<bool> UnenrollAsync(int studentUserId, int courseId, CancellationToken ct = default)
    {
        var row = await _db.Enrollments.FirstOrDefaultAsync(e => e.UserId == studentUserId && e.CourseId == courseId, ct);
        if (row == null) return false;
        _db.Enrollments.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SyllabusDto?> GetSyllabusForStudentAsync(int studentUserId, int courseId, CancellationToken ct = default)
    {
        var allowed = await _db.Enrollments.AnyAsync(e => e.UserId == studentUserId && e.CourseId == courseId, ct);
        if (!allowed) return null;
        var course = await _db.Courses.Include(c => c.Instructor).FirstOrDefaultAsync(c => c.Id == courseId, ct);
        if (course == null) return null;
        var keywords = string.IsNullOrWhiteSpace(course.HighlightKeywords)
            ? Array.Empty<string>()
            : course.HighlightKeywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new SyllabusDto
        {
            CourseId = course.Id,
            CourseCode = course.CourseCode,
            Title = course.Title,
            SyllabusContent = course.SyllabusContent,
            HighlightKeywords = keywords.Length > 0 ? keywords : null
        };
    }

    public async Task<SyllabusFileStreamDto?> GetSyllabusFileForStudentAsync(int studentUserId, int courseId, CancellationToken ct = default)
    {
        var allowed = await _db.Enrollments.AnyAsync(e => e.UserId == studentUserId && e.CourseId == courseId, ct);
        if (!allowed) return null;

        var upload = await _db.SyllabusPdfUploads
            .AsNoTracking()
            .Where(u => u.CourseId == courseId)
            .OrderByDescending(u => u.UploadedAtUtc)
            .FirstOrDefaultAsync(ct);
        if (upload == null || string.IsNullOrWhiteSpace(upload.StoredRelativePath)) return null;

        var rel = upload.StoredRelativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, rel));
        var root = Path.GetFullPath(_env.ContentRootPath);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            return null;

        var bytes = await File.ReadAllBytesAsync(fullPath, ct);
        var ext = Path.GetExtension(upload.OriginalFileName).ToLowerInvariant();
        if (ext != ".pdf" && ext != ".docx") ext = Path.GetExtension(fullPath).ToLowerInvariant();
        var contentType = ext == ".docx"
            ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            : "application/pdf";
        var name = string.IsNullOrWhiteSpace(upload.OriginalFileName) ? "syllabus" + ext : upload.OriginalFileName;
        return new SyllabusFileStreamDto(bytes, contentType, name);
    }

    public async Task<SyllabusPdfUploadResponseDto?> UploadSyllabusFileAsync(int instructorUserId, int courseId, Stream fileStream, string originalFileName, CancellationToken ct = default)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == instructorUserId, ct);
        if (course == null) return null;

        await using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms, ct);
        if (ms.Length == 0 || ms.Length > MaxPdfBytes) return null;

        var ext = Path.GetExtension(originalFileName ?? "").ToLowerInvariant();
        if (ext != ".pdf" && ext != ".docx") return null;

        var bytes = ms.ToArray();
        if (!_fileText.TryExtract(bytes, originalFileName ?? "", out var extracted))
            return null;

        var dir = Path.Combine(_env.ContentRootPath, "Data", "Uploads", courseId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dir);
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, storedName);
        await File.WriteAllBytesAsync(fullPath, bytes, ct);

        var relative = Path.GetRelativePath(_env.ContentRootPath, fullPath).Replace('\\', '/');
        var kind = ext == ".docx" ? "docx" : "pdf";

        var row = new SyllabusPdfUpload
        {
            CourseId = courseId,
            OriginalFileName = Path.GetFileName(originalFileName) ?? $"syllabus{ext}",
            StoredRelativePath = relative,
            ExtractedText = extracted,
            UploadedAtUtc = DateTime.UtcNow
        };
        _db.SyllabusPdfUploads.Add(row);

        course.SyllabusContent = extracted;
        course.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _ragIndex.ReindexCourseAsync(courseId, extracted, ct);

        var chunkCount = await _db.SyllabusChunks.CountAsync(c => c.CourseId == courseId, ct);

        return new SyllabusPdfUploadResponseDto
        {
            Id = row.Id,
            CourseId = courseId,
            OriginalFileName = row.OriginalFileName,
            FileKind = kind,
            ExtractedCharacterCount = extracted.Length,
            RagChunkCount = chunkCount,
            UploadedAtUtc = row.UploadedAtUtc
        };
    }

    public async Task<CourseFeedbackStatusDto?> GetFeedbackStatusForStudentAsync(int studentUserId, int courseId, CancellationToken ct = default)
    {
        var allowed = await _db.Enrollments.AnyAsync(e => e.UserId == studentUserId && e.CourseId == courseId, ct);
        if (!allowed) return null;

        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId, ct);
        if (course == null) return null;

        var existing = await _db.CourseFeedbacks.AsNoTracking()
            .FirstOrDefaultAsync(f => f.CourseId == courseId && f.StudentUserId == studentUserId, ct);

        var configured = course.FeedbackOpensAtUtc.HasValue && course.FeedbackClosesAtUtc.HasValue;
        var now = DateTime.UtcNow;
        var open = configured
                   && now >= course.FeedbackOpensAtUtc!.Value
                   && now <= course.FeedbackClosesAtUtc!.Value;

        string? msg = null;
        if (!configured)
            msg = "Bu ders için geri bildirim penceresi henüz açılmadı. Eğitmen tarih aralığı tanımlayınca buradan gönderebilirsiniz.";
        else if (now < course.FeedbackOpensAtUtc!.Value)
            msg = "Geri bildirim penceresi henüz başlamadı.";
        else if (now > course.FeedbackClosesAtUtc!.Value)
            msg = "Geri bildirim penceresi kapandı.";

        return new CourseFeedbackStatusDto
        {
            WindowConfigured = configured,
            WindowOpen = open,
            OpensAtUtc = course.FeedbackOpensAtUtc,
            ClosesAtUtc = course.FeedbackClosesAtUtc,
            HasSubmitted = existing != null,
            MyRating = existing?.Rating,
            MyComment = existing?.Comment,
            Message = msg
        };
    }

    public async Task<(bool Ok, string? Error)> SubmitCourseFeedbackAsync(int studentUserId, int courseId, SubmitCourseFeedbackRequest request, CancellationToken ct = default)
    {
        if (request.Rating < 1 || request.Rating > 5)
            return (false, "Puan 1 ile 5 arasında olmalıdır.");

        var comment = request.Comment?.Trim();
        if (!string.IsNullOrEmpty(comment) && comment.Length > 2000)
            return (false, "Yorum en fazla 2000 karakter olabilir.");

        var allowed = await _db.Enrollments.AnyAsync(e => e.UserId == studentUserId && e.CourseId == courseId, ct);
        if (!allowed)
            return (false, "Bu derse kayıtlı değilsiniz.");

        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == courseId, ct);
        if (course == null)
            return (false, "Ders bulunamadı.");

        var configured = course.FeedbackOpensAtUtc.HasValue && course.FeedbackClosesAtUtc.HasValue;
        if (!configured)
            return (false, "Geri bildirim penceresi tanımlı değil.");

        var now = DateTime.UtcNow;
        if (now < course.FeedbackOpensAtUtc!.Value || now > course.FeedbackClosesAtUtc!.Value)
            return (false, "Geri bildirim şu an kabul edilmiyor (tarih aralığı dışında).");

        var dup = await _db.CourseFeedbacks.AnyAsync(f => f.CourseId == courseId && f.StudentUserId == studentUserId, ct);
        if (dup)
            return (false, "Bu ders için zaten geri bildirim gönderdiniz.");

        _db.CourseFeedbacks.Add(new CourseFeedback
        {
            CourseId = courseId,
            StudentUserId = studentUserId,
            Rating = request.Rating,
            Comment = string.IsNullOrEmpty(comment) ? null : comment,
            SubmittedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SetCourseFeedbackWindowAsync(int instructorUserId, int courseId, SetCourseFeedbackWindowRequest request, CancellationToken ct = default)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == instructorUserId, ct);
        if (course == null)
            return (false, "Ders bulunamadı veya size ait değil.");

        if (request.OpensAtUtc == null && request.ClosesAtUtc == null)
        {
            course.FeedbackOpensAtUtc = null;
            course.FeedbackClosesAtUtc = null;
            await _db.SaveChangesAsync(ct);
            return (true, null);
        }

        if (request.OpensAtUtc == null || request.ClosesAtUtc == null)
            return (false, "Pencereyi açmak için hem başlangıç hem bitiş (UTC) gönderin; kapatmak için ikisini de boş gönderin.");

        if (request.OpensAtUtc.Value >= request.ClosesAtUtc.Value)
            return (false, "Bitiş tarihi başlangıçtan sonra olmalıdır.");

        course.FeedbackOpensAtUtc = request.OpensAtUtc.Value;
        course.FeedbackClosesAtUtc = request.ClosesAtUtc.Value;
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<List<CourseFeedbackItemDto>> GetCourseFeedbacksForInstructorAsync(int instructorUserId, int courseId, CancellationToken ct = default)
    {
        var owns = await _db.Courses.AnyAsync(c => c.Id == courseId && c.InstructorId == instructorUserId, ct);
        if (!owns) return new List<CourseFeedbackItemDto>();

        return await _db.CourseFeedbacks.AsNoTracking()
            .Where(f => f.CourseId == courseId)
            .OrderByDescending(f => f.SubmittedAtUtc)
            .Join(_db.Users.AsNoTracking(), f => f.StudentUserId, u => u.Id, (f, u) => new CourseFeedbackItemDto
            {
                Id = f.Id,
                StudentEmail = u.Email,
                StudentName = u.FullName,
                Rating = f.Rating,
                Comment = f.Comment,
                SubmittedAtUtc = f.SubmittedAtUtc
            })
            .ToListAsync(ct);
    }

    private static CourseDto ToDto(Course c, int feedbackResponseCount = 0) => new()
    {
        Id = c.Id,
        CourseCode = c.CourseCode,
        Title = c.Title,
        InstructorName = c.Instructor?.FullName ?? c.Instructor?.Email,
        HasSyllabus = !string.IsNullOrWhiteSpace(c.SyllabusContent),
        FeedbackOpensAtUtc = c.FeedbackOpensAtUtc,
        FeedbackClosesAtUtc = c.FeedbackClosesAtUtc,
        FeedbackResponseCount = feedbackResponseCount
    };
}
