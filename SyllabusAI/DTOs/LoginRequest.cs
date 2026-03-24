using System.ComponentModel.DataAnnotations;

namespace SyllabusAI.DTOs;

/// <summary>
/// Login sayfasından gönderilen email ve şifre (Figure 1 - Common login page).
/// </summary>
public class LoginRequest
{
    [Required(ErrorMessage = "E-posta gerekli")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre gerekli")]
    [MinLength(1)]
    public string Password { get; set; } = string.Empty;
}
