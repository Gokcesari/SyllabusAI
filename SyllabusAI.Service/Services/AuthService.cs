using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SyllabusAI.Data;
using SyllabusAI.DTOs;

namespace SyllabusAI.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly IMapper _mapper;

    public AuthService(ApplicationDbContext db, IConfiguration config, IMapper mapper)
    {
        _db = db;
        _config = config;
        _mapper = mapper;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, bool webPortalClient = false, CancellationToken cancellationToken = default)
    {
        if (!EmailDomainPolicy.IsAllowedEmail(request.Email))
            return LoginResult.DomainNotAllowed(EmailDomainPolicy.DomainErrorMessage);

        var normalizedEmail = request.Email.Trim();
        var password = request.Password?.Trim() ?? string.Empty;

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail.ToLower(), cancellationToken);

        if (user == null)
            return LoginResult.InvalidCredentials();

        if (string.IsNullOrEmpty(user.PasswordHash)
            || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return LoginResult.InvalidCredentials();

        var roleName = user.Role?.Name
                       ?? await _db.Roles.AsNoTracking()
                           .Where(r => r.Id == user.RoleId)
                           .Select(r => r.Name)
                           .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(roleName))
            return LoginResult.InvalidCredentials();

        // Admin: web giriş sayfasından JWT verilmez; Swagger / araçlar aynı endpoint ile token alabilir.
        if (webPortalClient && string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return LoginResult.AdminWebLoginForbidden(
                "Yönetici hesabı ile bu giriş ekranından oturum açılamaz. Kullanıcı yönetimi için Swagger üzerinden oturum açın.");
        }

        var jwt = GenerateJwt(user, roleName);
        var expiresInSec = _config.GetValue<int>("Jwt:ExpiresInSeconds");

        var userInfo = _mapper.Map<UserInfo>(user);
        userInfo.Role = roleName;

        return LoginResult.Ok(new LoginResponse
        {
            AccessToken = jwt,
            TokenType = "Bearer",
            ExpiresInSeconds = expiresInSec,
            User = userInfo
        });
    }

    private string GenerateJwt(Models.User user, string roleName)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddSeconds(_config.GetValue<int>("Jwt:ExpiresInSeconds"));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, roleName)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
