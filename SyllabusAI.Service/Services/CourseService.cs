using System.Globalization;
using AutoMapper;
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
    private readonly IMapper _mapper;

    public CourseService(ApplicationDbContext db, IWebHostEnvironment env, ISyllabusFileTextExtractor fileText, ISyllabusRagIndexService ragIndex, IMapper mapper)
    {
        _db = db;
        _env = env;
        _fileText = fileText;
        _ragIndex = ragIndex;
        _mapper = mapper;
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
        return _mapper.Map<CourseDto>(course);
    }

    public async Task<List<CourseDto>> GetInstructorCoursesAsync(int instructorUserId, CancellationToken ct = default)
    {
        var courses = await _db.Courses.AsNoTracking()
            .Include(c => c.Instructor)
            .Include(c => c.Feedbacks)
            .Include(c => c.WeeklyFeedbacks)
            .Where(c => c.InstructorId == instructorUserId)
            .OrderBy(c => c.CourseCode)
            .ToListAsync(ct);
        return _mapper.Map<List<CourseDto>>(courses);
    }

    public async Task<List<CourseDto>> GetMyEnrolledCoursesAsync(int studentUserId, CancellationToken ct = default)
    {
        var list = await _db.Enrollments
            .Where(e => e.UserId == studentUserId)
            .Include(e => e.Course).ThenInclude(c => c.Instructor)
            .OrderBy(e => e.Course.CourseCode)
            .Select(e => e.Course)
            .ToListAsync(ct);
        return _mapper.Map<List<CourseDto>>(list);
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

    public async Task<(bool Ok, string? Error)> DeleteCourseAsync(int instructorUserId, int courseId, CancellationToken ct = default)
    {
        var course = await _db.Courses
            .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == instructorUserId, ct);
        if (course == null)
            return (false, "Course not found or not owned by you.");

        var uploads = await _db.SyllabusPdfUploads
                        .Where(u => u.CourseId == courseId && u.IsActive)
            .ToListAsync(ct);
        var chunks = await _db.SyllabusChunks
            .Where(c => c.CourseId == courseId)
            .ToListAsync(ct);
        var enrollments = await _db.Enrollments
            .Where(e => e.CourseId == courseId)
            .ToListAsync(ct);
        var feedbacks = await _db.CourseFeedbacks
            .Where(f => f.CourseId == courseId)
            .ToListAsync(ct);
        var feedbackIds = feedbacks.Select(f => f.Id).ToList();
        var answers = feedbackIds.Count == 0
            ? new List<CourseFeedbackAnswer>()
            : await _db.CourseFeedbackAnswers.Where(a => feedbackIds.Contains(a.CourseFeedbackId)).ToListAsync(ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            if (answers.Count > 0) _db.CourseFeedbackAnswers.RemoveRange(answers);
            if (feedbacks.Count > 0) _db.CourseFeedbacks.RemoveRange(feedbacks);
            if (enrollments.Count > 0) _db.Enrollments.RemoveRange(enrollments);
            if (chunks.Count > 0) _db.SyllabusChunks.RemoveRange(chunks);
            if (uploads.Count > 0) _db.SyllabusPdfUploads.RemoveRange(uploads);
            _db.Courses.Remove(course);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        foreach (var upload in uploads)
        {
            if (string.IsNullOrWhiteSpace(upload.StoredRelativePath)) continue;
            try
            {
                var rel = upload.StoredRelativePath.Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, rel));
                var root = Path.GetFullPath(_env.ContentRootPath);
                if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                if (File.Exists(fullPath)) File.Delete(fullPath);
            }
            catch
            {
                // Keep DB delete successful even if a leftover file cannot be deleted.
            }
        }

        return (true, null);
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
        var dto = _mapper.Map<SyllabusDto>(course);
        dto.HighlightKeywords = keywords.Length > 0 ? keywords : null;
        return dto;
    }

    public async Task<List<FeedbackQuestionDto>> GetFeedbackQuestionsAsync(CancellationToken ct = default)
    {
        return await _db.FeedbackQuestions.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.QuestionNo)
            .Select(x => new FeedbackQuestionDto
            {
                QuestionNo = x.QuestionNo,
                QuestionText = x.Text
            })
            .ToListAsync(ct);
    }

    public async Task<SyllabusFileStreamDto?> GetSyllabusFileForStudentAsync(int studentUserId, int courseId, CancellationToken ct = default)
    {
        var allowed = await _db.Enrollments.AnyAsync(e => e.UserId == studentUserId && e.CourseId == courseId, ct);
        if (!allowed) return null;

        var upload = await _db.SyllabusPdfUploads
            .AsNoTracking()
                        .Where(u => u.CourseId == courseId && u.IsActive)
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

        var previousUploads = await _db.SyllabusPdfUploads.Where(x => x.CourseId == courseId && x.IsActive).ToListAsync(ct);
        foreach (var old in previousUploads) old.IsActive = false;

        var row = new SyllabusPdfUpload
        {
            CourseId = courseId,
            OriginalFileName = Path.GetFileName(originalFileName) ?? $"syllabus{ext}",
            StoredRelativePath = relative,
            ExtractedText = extracted,
            IsActive = true,
            UploadedAtUtc = DateTime.UtcNow
        };
        _db.SyllabusPdfUploads.Add(row);

        course.SyllabusContent = extracted;
        course.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _ragIndex.ReindexCourseAsync(courseId, extracted, ct);

        var chunkCount = await _db.SyllabusChunks.CountAsync(c => c.CourseId == courseId, ct);

        var dto = _mapper.Map<SyllabusPdfUploadResponseDto>(row);
        dto.FileKind = kind;
        dto.ExtractedCharacterCount = extracted.Length;
        dto.RagChunkCount = chunkCount;
        return dto;
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
            msg = "The feedback window for this course is not open yet. You can submit when the instructor sets the dates.";
        else if (now < course.FeedbackOpensAtUtc!.Value)
            msg = "The feedback window has not started yet.";
        else if (now > course.FeedbackClosesAtUtc!.Value)
            msg = "The feedback window has closed.";

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
            return (false, "Rating must be between 1 and 5.");

        var comment = request.Comment?.Trim();
        if (!string.IsNullOrEmpty(comment) && comment.Length > 2000)
            return (false, "Comment cannot exceed 2000 characters.");

        var allowed = await _db.Enrollments.AnyAsync(e => e.UserId == studentUserId && e.CourseId == courseId, ct);
        if (!allowed)
            return (false, "You are not enrolled in this course.");

        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == courseId, ct);
        if (course == null)
            return (false, "Course not found.");

        var configured = course.FeedbackOpensAtUtc.HasValue && course.FeedbackClosesAtUtc.HasValue;
        if (!configured)
            return (false, "No feedback window is configured.");

        var now = DateTime.UtcNow;
        if (now < course.FeedbackOpensAtUtc!.Value || now > course.FeedbackClosesAtUtc!.Value)
            return (false, "Feedback is not being accepted right now (outside the allowed dates).");

        var dup = await _db.CourseFeedbacks.AnyAsync(f => f.CourseId == courseId && f.StudentUserId == studentUserId, ct);
        if (dup)
            return (false, "You have already submitted feedback for this course.");

        var questions = await _db.FeedbackQuestions.AsNoTracking()
            .Where(q => q.IsActive)
            .OrderBy(q => q.QuestionNo)
            .ToListAsync(ct);
        if (questions.Count == 0)
            return (false, "Survey questions are not configured.");
        if (request.Answers == null || request.Answers.Count != questions.Count)
            return (false, "Answer all survey questions with a rating from 1 to 5.");

        var map = request.Answers
            .GroupBy(a => a.QuestionNo)
            .ToDictionary(g => g.Key, g => g.Last().Rating);
        foreach (var q in questions)
        {
            if (!map.TryGetValue(q.QuestionNo, out var rating) || rating < 1 || rating > 5)
                return (false, "Invalid survey answers. Every question must be rated from 1 to 5.");
        }

        var avg = (byte)Math.Clamp((int)Math.Round(map.Values.Average(x => x), MidpointRounding.AwayFromZero), 1, 5);
        var feedback = new CourseFeedback
        {
            CourseId = courseId,
            StudentUserId = studentUserId,
            Rating = avg,
            Comment = string.IsNullOrEmpty(comment) ? null : comment,
            SubmittedAtUtc = DateTime.UtcNow
        };
        _db.CourseFeedbacks.Add(feedback);
        await _db.SaveChangesAsync(ct);

        var answers = questions.Select(q => new CourseFeedbackAnswer
        {
            CourseFeedbackId = feedback.Id,
            FeedbackQuestionId = q.Id,
            Rating = map[q.QuestionNo]
        }).ToList();
        _db.CourseFeedbackAnswers.AddRange(answers);
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SetCourseFeedbackWindowAsync(int instructorUserId, int courseId, SetCourseFeedbackWindowRequest request, CancellationToken ct = default)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == instructorUserId, ct);
        if (course == null)
            return (false, "Course not found or not owned by you.");

        if (request.OpensAtUtc == null && request.ClosesAtUtc == null)
        {
            course.FeedbackOpensAtUtc = null;
            course.FeedbackClosesAtUtc = null;
            await _db.SaveChangesAsync(ct);
            return (true, null);
        }

        if (request.OpensAtUtc == null || request.ClosesAtUtc == null)
            return (false, "To open the window, send both start and end (UTC); to close it, send both as empty.");

        if (request.OpensAtUtc.Value >= request.ClosesAtUtc.Value)
            return (false, "End time must be after start time.");

        course.FeedbackOpensAtUtc = request.OpensAtUtc.Value;
        course.FeedbackClosesAtUtc = request.ClosesAtUtc.Value;
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<List<CourseFeedbackItemDto>> GetCourseFeedbacksForInstructorAsync(int instructorUserId, int courseId, CancellationToken ct = default)
    {
        var owns = await _db.Courses.AnyAsync(c => c.Id == courseId && c.InstructorId == instructorUserId, ct);
        if (!owns) return new List<CourseFeedbackItemDto>();

        var feedbacks = await _db.CourseFeedbacks.AsNoTracking()
            .Include(f => f.Student)
            .Include(f => f.Answers)
            .ThenInclude(a => a.FeedbackQuestion)
            .Where(f => f.CourseId == courseId)
            .OrderByDescending(f => f.SubmittedAtUtc)
            .ToListAsync(ct);
        var dto = _mapper.Map<List<CourseFeedbackItemDto>>(feedbacks);
        for (var i = 0; i < feedbacks.Count; i++)
        {
            dto[i].StudentName = BuildAnonymizedDisplayName(feedbacks[i].Student?.FullName, feedbacks[i].Student?.Email);
            dto[i].Answers = feedbacks[i].Answers
                .OrderBy(x => x.FeedbackQuestion.QuestionNo)
                .Select(x => new SurveyQuestionResponseDto
                {
                    QuestionNo = x.FeedbackQuestion.QuestionNo,
                    QuestionText = x.FeedbackQuestion.Text,
                    Rating = x.Rating
                })
                .ToList();
        }
        return dto;
    }

    public async Task<CourseFeedbackItemDto?> GetCourseFeedbackDetailForInstructorAsync(int instructorUserId, int courseId, int feedbackId, CancellationToken ct = default)
    {
        var owns = await _db.Courses.AnyAsync(c => c.Id == courseId && c.InstructorId == instructorUserId, ct);
        if (!owns) return null;

        var row = await _db.CourseFeedbacks.AsNoTracking()
            .Include(f => f.Student)
            .Include(f => f.Answers)
            .ThenInclude(a => a.FeedbackQuestion)
            .FirstOrDefaultAsync(f => f.Id == feedbackId && f.CourseId == courseId, ct);
        if (row == null) return null;

        var dto = _mapper.Map<CourseFeedbackItemDto>(row);
        dto.StudentName = BuildAnonymizedDisplayName(row.Student?.FullName, row.Student?.Email);
        dto.Answers = row.Answers
            .OrderBy(x => x.FeedbackQuestion.QuestionNo)
            .Select(x => new SurveyQuestionResponseDto
            {
                QuestionNo = x.FeedbackQuestion.QuestionNo,
                QuestionText = x.FeedbackQuestion.Text,
                Rating = x.Rating
            })
            .ToList();
        return dto;
    }

    public async Task<CourseFeedbackSummaryDto?> GetCourseFeedbackSummaryForInstructorAsync(int instructorUserId, int courseId, CancellationToken ct = default)
    {
        var owns = await _db.Courses.AnyAsync(c => c.Id == courseId && c.InstructorId == instructorUserId, ct);
        if (!owns) return null;

        var rows = await _db.CourseFeedbacks.AsNoTracking()
            .Include(f => f.Answers)
            .ThenInclude(a => a.FeedbackQuestion)
            .Where(f => f.CourseId == courseId)
            .ToListAsync(ct);

        var questions = await _db.FeedbackQuestions.AsNoTracking()
            .Where(q => q.IsActive)
            .OrderBy(q => q.QuestionNo)
            .ToListAsync(ct);

        var summary = new CourseFeedbackSummaryDto
        {
            TotalResponses = rows.Count,
            Questions = questions
                .Select(q => new CourseFeedbackQuestionSummaryDto { QuestionNo = q.QuestionNo, QuestionText = q.Text })
                .ToList()
        };

        foreach (var row in rows)
        {
            foreach (var a in row.Answers)
            {
                var q = summary.Questions.FirstOrDefault(x => x.QuestionNo == a.FeedbackQuestion.QuestionNo);
                if (q == null) continue;
                if (a.Rating == 1) q.Rate1Count++;
                else if (a.Rating == 2) q.Rate2Count++;
                else if (a.Rating == 3) q.Rate3Count++;
                else if (a.Rating == 4) q.Rate4Count++;
                else if (a.Rating == 5) q.Rate5Count++;
            }
        }

        foreach (var q in summary.Questions)
        {
            q.TotalAnswers = q.Rate1Count + q.Rate2Count + q.Rate3Count + q.Rate4Count + q.Rate5Count;
            if (q.TotalAnswers == 0)
            {
                q.AverageRating = 0;
                q.AveragePercent = 0;
                continue;
            }

            var weightedTotal =
                (q.Rate1Count * 1) +
                (q.Rate2Count * 2) +
                (q.Rate3Count * 3) +
                (q.Rate4Count * 4) +
                (q.Rate5Count * 5);

            q.AverageRating = Math.Round((double)weightedTotal / q.TotalAnswers, 2, MidpointRounding.AwayFromZero);
            q.AveragePercent = Math.Round((q.AverageRating / 5d) * 100d, 2, MidpointRounding.AwayFromZero);
        }

        return summary;
    }

    private static string BuildAnonymizedDisplayName(string? fullName, string? email)
    {
        var source = string.IsNullOrWhiteSpace(fullName) ? email ?? "Student" : fullName;
        var parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
        {
            var first = MaskName(parts[0]);
            var last = MaskName(parts[1]);
            return $"{first} {last}";
        }
        if (parts.Length == 1)
            return MaskName(parts[0]);
        return "Student";
    }

    private static string MaskName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "*";
        var t = s.Trim();
        if (t.Length == 1) return t.ToUpperInvariant() + "*";
        return t[0].ToString().ToUpperInvariant() + new string('*', t.Length - 1);
    }

}


