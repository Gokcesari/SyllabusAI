using SyllabusAI.DTOs;

namespace SyllabusAI.Services;

public interface ICourseService
{
    Task<CourseDto?> CreateCourseAsync(int instructorUserId, CreateCourseRequest request, CancellationToken ct = default);
    Task<List<CourseDto>> GetInstructorCoursesAsync(int instructorUserId, CancellationToken ct = default);
    Task<List<CourseDto>> GetMyEnrolledCoursesAsync(int studentUserId, CancellationToken ct = default);
    Task<EnrollResult> EnrollByCourseCodeAsync(int studentUserId, string courseCode, CancellationToken ct = default);
    /// <summary>Öğrenci: Kayıtlı dersten çıkar (Enrollment satırını siler).</summary>
    Task<bool> UnenrollAsync(int studentUserId, int courseId, CancellationToken ct = default);
    Task<SyllabusDto?> GetSyllabusForStudentAsync(int studentUserId, int courseId, CancellationToken ct = default);
    /// <summary>Öğrenci: derse yüklenen son syllabus dosyasının ham içeriği (kayıtlıysa).</summary>
    Task<SyllabusFileStreamDto?> GetSyllabusFileForStudentAsync(int studentUserId, int courseId, CancellationToken ct = default);
    Task<SyllabusPdfUploadResponseDto?> UploadSyllabusFileAsync(int instructorUserId, int courseId, Stream fileStream, string originalFileName, CancellationToken ct = default);

    Task<CourseFeedbackStatusDto?> GetFeedbackStatusForStudentAsync(int studentUserId, int courseId, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> SubmitCourseFeedbackAsync(int studentUserId, int courseId, SubmitCourseFeedbackRequest request, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> SetCourseFeedbackWindowAsync(int instructorUserId, int courseId, SetCourseFeedbackWindowRequest request, CancellationToken ct = default);
    Task<List<CourseFeedbackItemDto>> GetCourseFeedbacksForInstructorAsync(int instructorUserId, int courseId, CancellationToken ct = default);
}

public enum EnrollResult { Ok, AlreadyEnrolled, CourseNotFound }
