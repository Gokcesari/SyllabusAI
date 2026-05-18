using SyllabusAI.DTOs;

namespace SyllabusAI.Services;

public interface IWeeklyFeedbackService
{
    Task<List<FeedbackQuestionDto>> GetWeeklyFeedbackQuestionsAsync(CancellationToken ct = default);
    Task<CourseFeedbackStatusDto?> GetWeeklyFeedbackStatusForStudentAsync(int studentUserId, int courseId, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> SubmitCourseWeeklyFeedbackAsync(int studentUserId, int courseId, SubmitCourseFeedbackRequest request, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> SetCourseWeeklyFeedbackWindowAsync(int instructorUserId, int courseId, SetCourseFeedbackWindowRequest request, CancellationToken ct = default);
    Task<List<CourseFeedbackItemDto>> GetCourseWeeklyFeedbacksForInstructorAsync(int instructorUserId, int courseId, CancellationToken ct = default);
    Task<CourseFeedbackItemDto?> GetCourseWeeklyFeedbackDetailForInstructorAsync(int instructorUserId, int courseId, int feedbackId, CancellationToken ct = default);
    Task<CourseFeedbackSummaryDto?> GetCourseWeeklyFeedbackSummaryForInstructorAsync(int instructorUserId, int courseId, CancellationToken ct = default);
}
