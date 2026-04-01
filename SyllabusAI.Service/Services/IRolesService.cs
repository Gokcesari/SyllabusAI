using SyllabusAI.DTOs;

namespace SyllabusAI.Services;

public interface IRolesService
{
    Task<List<RoleDto>> GetAllAsync(CancellationToken ct = default);
    Task<(bool Created, RoleDto Role)> EnsureRoleAsync(CreateRoleRequest request, CancellationToken ct = default);
}
