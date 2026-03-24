namespace SyllabusAI.Models;

/// <summary>
/// Login sayfasında email ve şifre ile doğrulanacak kullanıcı (öğrenci/eğitmen).
/// GitHub'tan indirilen veritabanı şemasına göre alanlar güncellenebilir.
/// </summary>
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? FullName { get; set; }

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
