using SyllabusAI.DTOs;

namespace SyllabusAI.Services;

public interface IAuthService
{
    /// <summary>
    /// E-posta domain kontrolü (@bahcesehir.edu.tr / @bau.edu.tr / @ou.bau.edu.tr) sonra DB ve JWT.
    /// </summary>
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
