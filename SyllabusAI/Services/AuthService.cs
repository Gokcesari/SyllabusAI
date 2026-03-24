using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SyllabusAI.Data;
using SyllabusAI.DTOs;

namespace SyllabusAI.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var roleByDomain = EmailDomainPolicy.GetRoleByEmail(request.Email);
        if (roleByDomain == null)
            return LoginResult.DomainNotAllowed(EmailDomainPolicy.DomainErrorMessage);

        var useDemoAuth = _config.GetValue<bool>("UseDemoAuth");
        var demoPassword = _config["DemoPassword"] ?? "Test123!";

        if (useDemoAuth)
        {
            if (request.Password != demoPassword)
                return LoginResult.InvalidCredentials();
            var token = GenerateJwtForDemo(request.Email.Trim(), roleByDomain);
            var expiresIn = _config.GetValue<int>("Jwt:ExpiresInSeconds");
            return LoginResult.Ok(new LoginResponse
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresInSeconds = expiresIn,
                User = new UserInfo
                {
                    Id = 0,
                    Email = request.Email.Trim(),
                    FullName = roleByDomain == "Student" ? "Demo Öğrenci" : "Demo Eğitmen",
                    Role = roleByDomain
                }
            });
        }

        // Bu aşamada sadece 3 test hesabı ile girişe izin veriyoruz (istenen kural).
        if (!EmailDomainPolicy.IsAllowedSpecificEmail(request.Email))
            return LoginResult.DomainNotAllowed(EmailDomainPolicy.EmailWhitelistErrorMessage);

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == request.Email.Trim(), cancellationToken);

        if (user == null)
            return LoginResult.InvalidCredentials();
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return LoginResult.InvalidCredentials();

        var jwt = GenerateJwt(user, roleByDomain);
        var expiresInSec = _config.GetValue<int>("Jwt:ExpiresInSeconds");

        return LoginResult.Ok(new LoginResponse
        {
            AccessToken = jwt,
            TokenType = "Bearer",
            ExpiresInSeconds = expiresInSec,
            User = new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Role = roleByDomain
            }
        });
    }

    private string GenerateJwtForDemo(string email, string roleByDomain)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddSeconds(_config.GetValue<int>("Jwt:ExpiresInSeconds"));
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "0"),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, roleByDomain)
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

    private string GenerateJwt(Models.User user, string roleByDomain)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddSeconds(_config.GetValue<int>("Jwt:ExpiresInSeconds"));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, roleByDomain)
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
