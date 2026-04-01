using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SyllabusAI.Data;
using SyllabusAI.DTOs;
using SyllabusAI.Models;

namespace SyllabusAI.Services;

public class UserManagementService : IUserManagementService
{
    private readonly ApplicationDbContext _db;
    private readonly IMapper _mapper;

    public UserManagementService(ApplicationDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<(bool Ok, string? Error, UserSummaryDto? User)> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim();
        if (!EmailDomainPolicy.IsAllowedEmail(email))
            return (false, EmailDomainPolicy.DomainErrorMessage, null);

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return (false, "Şifre en az 8 karakter olmalıdır.", null);

        var roleName = request.Role.Trim();
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct);
        if (role is null)
            return (false, "Geçersiz rol. Student, Instructor veya Admin olmalıdır.", null);

        var exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower(), ct);
        if (exists)
            return (false, "Bu e-posta adresi zaten kayıtlı.", null);

        var user = new User
        {
            Email = email,
            FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim(),
            RoleId = role.Id,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        user.Role = role;

        var dto = _mapper.Map<UserSummaryDto>(user);
        dto.Role = role.Name;
        return (true, null, dto);
    }

    public async Task<(bool Ok, string? Error)> UpdatePasswordByEmailAsync(UpdateUserPasswordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return (false, "Yeni şifre en az 8 karakter olmalıdır.");

        var email = request.Email.Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), ct);
        if (user is null)
            return (false, "Kullanıcı bulunamadı.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }
}
