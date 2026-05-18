using Microsoft.EntityFrameworkCore;
using SyllabusAI.Data;
using SyllabusAI.DTOs;
using SyllabusAI.Models;

namespace SyllabusAI.Services;

public class WeeklyFeedbackService : IWeeklyFeedbackService
{
    private readonly ApplicationDbContext _db;
    public WeeklyFeedbackService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<FeedbackQuestionDto>> GetWeeklyFeedbackQuestionsAsync(CancellationToken ct = default)
    {
        return await _db.Set<WeeklyFeedbackQuestion>().AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.QuestionNo)
            .Select(x => new FeedbackQuestionDto
            {
                QuestionNo = x.QuestionNo,
                QuestionText = x.Text
            })
            .ToListAsync(ct);
    }

    public async Task<CourseFeedbackStatusDto?> GetWeeklyFeedbackStatusForStudentAsync(int studentUserId, int courseId, CancellationToken ct = default)
    {
        var allowed = await _db.Enrollments.AnyAsync(e => e.UserId == studentUserId && e.CourseId == courseId, ct);
        if (!allowed) return null;

        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId, ct);
        if (course == null) return null;

        var existing = await _db.Set<CourseWeeklyFeedback>().AsNoTracking()
            .FirstOrDefaultAsync(f => f.CourseId == courseId && f.StudentUserId == studentUserId, ct);

        var configured = course.WeeklyFeedbackOpensAtUtc.HasValue && course.WeeklyFeedbackClosesAtUtc.HasValue;
        var now = DateTime.UtcNow;
        var open = configured
                   && now >= course.WeeklyFeedbackOpensAtUtc!.Value
                   && now <= course.WeeklyFeedbackClosesAtUtc!.Value;

        string? msg = null;
        if (!configured)
            msg = "The weekly feedback window for this course is not open yet.";
        else if (now < course.WeeklyFeedbackOpensAtUtc!.Value)
            msg = "The weekly feedback window has not started yet.";
        else if (now > course.WeeklyFeedbackClosesAtUtc!.Value)
            msg = "The weekly feedback window has closed.";

        return new CourseFeedbackStatusDto
        {
            WindowConfigured = configured,
            WindowOpen = open,
            OpensAtUtc = course.WeeklyFeedbackOpensAtUtc,
            ClosesAtUtc = course.WeeklyFeedbackClosesAtUtc,
            HasSubmitted = existing != null,
            MyRating = existing?.Rating,
            MyComment = existing?.Comment,
            Message = msg
        };
    }

    public async Task<(bool Ok, string? Error)> SubmitCourseWeeklyFeedbackAsync(int studentUserId, int courseId, SubmitCourseFeedbackRequest request, CancellationToken ct = default)
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

        var configured = course.WeeklyFeedbackOpensAtUtc.HasValue && course.WeeklyFeedbackClosesAtUtc.HasValue;
        if (!configured)
            return (false, "No weekly feedback window is configured.");

        var now = DateTime.UtcNow;
        if (now < course.WeeklyFeedbackOpensAtUtc!.Value || now > course.WeeklyFeedbackClosesAtUtc!.Value)
            return (false, "Weekly feedback is not being accepted right now (outside the allowed dates).");

        var dup = await _db.Set<CourseWeeklyFeedback>().AnyAsync(f => f.CourseId == courseId && f.StudentUserId == studentUserId, ct);
        if (dup)
            return (false, "You have already submitted weekly feedback for this course.");

        var questions = await _db.Set<WeeklyFeedbackQuestion>().AsNoTracking()
            .Where(q => q.IsActive)
            .OrderBy(q => q.QuestionNo)
            .ToListAsync(ct);
        if (questions.Count == 0)
            return (false, "Weekly survey questions are not configured.");
        if (request.Answers == null || request.Answers.Count != questions.Count)
            return (false, "Answer all weekly survey questions with a rating from 1 to 5.");

        var map = request.Answers
            .GroupBy(a => a.QuestionNo)
            .ToDictionary(g => g.Key, g => g.Last().Rating);
        foreach (var q in questions)
        {
            if (!map.TryGetValue(q.QuestionNo, out var rating) || rating < 1 || rating > 5)
                return (false, "Invalid weekly survey answers. Every question must be rated from 1 to 5.");
        }

        var avg = (byte)Math.Clamp((int)Math.Round(map.Values.Average(x => x), MidpointRounding.AwayFromZero), 1, 5);
        var feedback = new CourseWeeklyFeedback
        {
            CourseId = courseId,
            StudentUserId = studentUserId,
            Rating = avg,
            Comment = string.IsNullOrEmpty(comment) ? null : comment,
            SubmittedAtUtc = DateTime.UtcNow
        };
        _db.Set<CourseWeeklyFeedback>().Add(feedback);
        await _db.SaveChangesAsync(ct);

        var answers = questions.Select(q => new CourseWeeklyFeedbackAnswer
        {
            CourseWeeklyFeedbackId = feedback.Id,
            WeeklyFeedbackQuestionId = q.Id,
            Rating = map[q.QuestionNo]
        }).ToList();
        _db.Set<CourseWeeklyFeedbackAnswer>().AddRange(answers);
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SetCourseWeeklyFeedbackWindowAsync(int instructorUserId, int courseId, SetCourseFeedbackWindowRequest request, CancellationToken ct = default)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == instructorUserId, ct);
        if (course == null)
            return (false, "Course not found or not owned by you.");

        if (request.OpensAtUtc == null && request.ClosesAtUtc == null)
        {
            course.WeeklyFeedbackOpensAtUtc = null;
            course.WeeklyFeedbackClosesAtUtc = null;
            await _db.SaveChangesAsync(ct);
            return (true, null);
        }

        if (request.OpensAtUtc == null || request.ClosesAtUtc == null)
            return (false, "To open the window, send both start and end (UTC); to close it, send both as empty.");

        if (request.OpensAtUtc.Value >= request.ClosesAtUtc.Value)
            return (false, "End time must be after start time.");

        course.WeeklyFeedbackOpensAtUtc = request.OpensAtUtc.Value;
        course.WeeklyFeedbackClosesAtUtc = request.ClosesAtUtc.Value;
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<List<CourseFeedbackItemDto>> GetCourseWeeklyFeedbacksForInstructorAsync(int instructorUserId, int courseId, CancellationToken ct = default)
    {
        var owns = await _db.Courses.AnyAsync(c => c.Id == courseId && c.InstructorId == instructorUserId, ct);
        if (!owns) return new List<CourseFeedbackItemDto>();

        var feedbacks = await _db.Set<CourseWeeklyFeedback>().AsNoTracking()
            .Include(f => f.Student)
            .Include(f => f.Answers)
            .ThenInclude(a => a.WeeklyFeedbackQuestion)
            .Where(f => f.CourseId == courseId)
            .OrderByDescending(f => f.SubmittedAtUtc)
            .ToListAsync(ct);
        var dto = feedbacks.Select(f => new CourseFeedbackItemDto
        {
            Id = f.Id,
            StudentEmail = f.Student.Email,
            StudentName = BuildAnonymizedDisplayName(f.Student?.FullName, f.Student?.Email),
            Rating = f.Rating,
            Comment = f.Comment,
            SubmittedAtUtc = f.SubmittedAtUtc,
            Answers = f.Answers
                .OrderBy(x => x.WeeklyFeedbackQuestion.QuestionNo)
                .Select(x => new SurveyQuestionResponseDto
                {
                    QuestionNo = x.WeeklyFeedbackQuestion.QuestionNo,
                    QuestionText = x.WeeklyFeedbackQuestion.Text,
                    Rating = x.Rating
                }).ToList()
        }).ToList();
        return dto;
    }

    public async Task<CourseFeedbackItemDto?> GetCourseWeeklyFeedbackDetailForInstructorAsync(int instructorUserId, int courseId, int feedbackId, CancellationToken ct = default)
    {
        var owns = await _db.Courses.AnyAsync(c => c.Id == courseId && c.InstructorId == instructorUserId, ct);
        if (!owns) return null;

        var row = await _db.Set<CourseWeeklyFeedback>().AsNoTracking()
            .Include(f => f.Student)
            .Include(f => f.Answers)
            .ThenInclude(a => a.WeeklyFeedbackQuestion)
            .FirstOrDefaultAsync(f => f.Id == feedbackId && f.CourseId == courseId, ct);
        if (row == null) return null;

        return new CourseFeedbackItemDto
        {
            Id = row.Id,
            StudentEmail = row.Student.Email,
            StudentName = BuildAnonymizedDisplayName(row.Student?.FullName, row.Student?.Email),
            Rating = row.Rating,
            Comment = row.Comment,
            SubmittedAtUtc = row.SubmittedAtUtc,
            Answers = row.Answers
                .OrderBy(x => x.WeeklyFeedbackQuestion.QuestionNo)
                .Select(x => new SurveyQuestionResponseDto
                {
                    QuestionNo = x.WeeklyFeedbackQuestion.QuestionNo,
                    QuestionText = x.WeeklyFeedbackQuestion.Text,
                    Rating = x.Rating
                }).ToList()
        };
    }

    public async Task<CourseFeedbackSummaryDto?> GetCourseWeeklyFeedbackSummaryForInstructorAsync(int instructorUserId, int courseId, CancellationToken ct = default)
    {
        var owns = await _db.Courses.AnyAsync(c => c.Id == courseId && c.InstructorId == instructorUserId, ct);
        if (!owns) return null;

        var rows = await _db.Set<CourseWeeklyFeedback>().AsNoTracking()
            .Include(f => f.Answers)
            .ThenInclude(a => a.WeeklyFeedbackQuestion)
            .Where(f => f.CourseId == courseId)
            .ToListAsync(ct);

        var questions = await _db.Set<WeeklyFeedbackQuestion>().AsNoTracking()
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
                var q = summary.Questions.FirstOrDefault(x => x.QuestionNo == a.WeeklyFeedbackQuestion.QuestionNo);
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
