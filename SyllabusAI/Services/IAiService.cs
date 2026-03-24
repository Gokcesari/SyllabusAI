using SyllabusAI.DTOs;

namespace SyllabusAI.Services;

public interface IAiService
{
    Task<ChatResponse> AskAsync(int userId, ChatRequest request, CancellationToken ct = default);
}
