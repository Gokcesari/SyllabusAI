namespace SyllabusAI.DTOs;

public class LoginResult
{
    public bool Success => Response != null;
    public LoginResponse? Response { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; } // "DomainNotAllowed" | "InvalidCredentials"

    public static LoginResult Ok(LoginResponse response) => new() { Response = response };
    public static LoginResult DomainNotAllowed(string message) => new() { ErrorCode = "DomainNotAllowed", ErrorMessage = message };
    public static LoginResult InvalidCredentials() => new() { ErrorCode = "InvalidCredentials", ErrorMessage = "E-posta veya şifre hatalı." };
}
