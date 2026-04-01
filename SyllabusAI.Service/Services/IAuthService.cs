using SyllabusAI.DTOs;

namespace SyllabusAI.Services;

public interface IAuthService
{
    /// <summary>
    /// E-posta domain kontrolü (@bahcesehir.edu.tr / @bau.edu.tr / @ou.bau.edu.tr) sonra DB ve JWT.
    /// </summary>
    /// <param name="webPortalClient">true ise (tarayıcı giriş sayfası); Admin rolüne JWT verilmez. Swagger vb. false.</param>
    Task<LoginResult> LoginAsync(LoginRequest request, bool webPortalClient = false, CancellationToken cancellationToken = default);
}
