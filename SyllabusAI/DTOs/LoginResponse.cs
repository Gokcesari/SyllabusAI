namespace SyllabusAI.DTOs;

/// <summary>
/// Başarılı girişte dönen JWT ve kullanıcı bilgisi (rol: Student/Instructor/Admin).
/// </summary>
public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresInSeconds { get; set; }
    public UserInfo User { get; set; } = null!;
}

public class UserInfo
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string Role { get; set; } = string.Empty; // "Student", "Instructor", "Admin"
}
