using Microsoft.EntityFrameworkCore;
using SyllabusAI.Data;
using SyllabusAI.DTOs;
using SyllabusAI.Models;

namespace SyllabusAI.Services;

public class CourseService : ICourseService
{
    private readonly ApplicationDbContext _db;

    public CourseService(ApplicationDbContext db) => _db = db;

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
        return ToDto(course);
    }

    public async Task<List<CourseDto>> GetInstructorCoursesAsync(int instructorUserId, CancellationToken ct = default)
    {
        var list = await _db.Courses
            .Include(c => c.Instructor)
            .Where(c => c.InstructorId == instructorUserId)
            .OrderBy(c => c.CourseCode).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<List<CourseDto>> GetMyEnrolledCoursesAsync(int studentUserId, CancellationToken ct = default)
    {
        var list = await _db.Enrollments
            .Where(e => e.UserId == studentUserId)
            .Include(e => e.Course).ThenInclude(c => c.Instructor)
            .OrderBy(e => e.Course.CourseCode)
            .Select(e => e.Course)
            .ToListAsync(ct);
        return list.Select(ToDto).ToList();
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

    private static CourseDto ToDto(Course c) => new()
    {
        Id = c.Id,
        CourseCode = c.CourseCode,
        Title = c.Title,
        InstructorName = c.Instructor?.FullName ?? c.Instructor?.Email,
        HasSyllabus = !string.IsNullOrWhiteSpace(c.SyllabusContent)
    };
}
