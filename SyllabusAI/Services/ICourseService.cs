using SyllabusAI.DTOs;

namespace SyllabusAI.Services;

public interface ICourseService
{
    Task<CourseDto?> CreateCourseAsync(int instructorUserId, CreateCourseRequest request, CancellationToken ct = default);
    Task<List<CourseDto>> GetInstructorCoursesAsync(int instructorUserId, CancellationToken ct = default);
    Task<List<CourseDto>> GetMyEnrolledCoursesAsync(int studentUserId, CancellationToken ct = default);
    Task<EnrollResult> EnrollByCourseCodeAsync(int studentUserId, string courseCode, CancellationToken ct = default);
    Task<SyllabusDto?> GetSyllabusForStudentAsync(int studentUserId, int courseId, CancellationToken ct = default);
}

public enum EnrollResult { Ok, AlreadyEnrolled, CourseNotFound }
