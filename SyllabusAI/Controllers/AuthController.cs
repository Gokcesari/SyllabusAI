using Microsoft.AspNetCore.Mvc;
using SyllabusAI.DTOs;
using SyllabusAI.Services;

namespace SyllabusAI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, IConfiguration config, ILogger<AuthController> logger)
    {
        _authService = authService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Giriş sayfası için ayar: demo mod ve şifre ipucu (veritabanı bağlı değilken test için).
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var useDemoAuth = _config.GetValue<bool>("UseDemoAuth");
        if (useDemoAuth)
            return Ok(new { useDemoAuth = true, demoPasswordHint = _config["DemoPassword"] ?? "Test123!" });
        return Ok(new { useDemoAuth = false });
    }

    /// <summary>
    /// Giriş: email ve şifre ile DB kontrolü yapılır (veya demo modda sadece domain + demo şifre).
    /// Öğrenci ve eğitmen ortak login sayfası (Figure 1) bu endpoint'i kullanır.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "E-posta ve şifre gerekli." });

        var result = await _authService.LoginAsync(request, cancellationToken);

        if (result.ErrorCode == "DomainNotAllowed")
        {
            _logger.LogWarning("İzin verilmeyen e-posta domaini: {Email}", request.Email);
            return StatusCode(403, new { message = result.ErrorMessage });
        }

        if (!result.Success)
        {
            _logger.LogWarning("Başarısız giriş denemesi: {Email}", request.Email);
            return Unauthorized(new { message = result.ErrorMessage ?? "E-posta veya şifre hatalı." });
        }

        _logger.LogInformation("Başarılı giriş: {Email}, Rol: {Role}", request.Email, result.Response!.User.Role);
        return Ok(result.Response);
    }
}
