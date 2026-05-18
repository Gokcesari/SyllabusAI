using SyllabusAI.DTOs;

namespace SyllabusAI.Services;

public interface IAiService
{
    Task<ChatResponse> AskAsync(int userId, ChatRequest request, CancellationToken ct = default);
    Task<ChatResponse> AskInstructorAsync(int instructorUserId, ChatRequest request, CancellationToken ct = default);
    Task<ChatCourseAnalyticsDto?> GetCourseAnalyticsAsync(int instructorUserId, int courseId, CancellationToken ct = default);
    Task<RagEvalSummaryDto> EvaluateAsync(int instructorUserId, IReadOnlyList<RagEvalCaseDto> cases, CancellationToken ct = default);
}
