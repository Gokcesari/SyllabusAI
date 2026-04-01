using SyllabusAI.DTOs;

namespace SyllabusAI.Services;

public interface IUserManagementService
{
    Task<(bool Ok, string? Error, UserSummaryDto? User)> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> UpdatePasswordByEmailAsync(UpdateUserPasswordRequest request, CancellationToken ct = default);
}
