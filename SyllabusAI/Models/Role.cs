namespace SyllabusAI.Models;

/// <summary>
/// Kullanıcı rolleri: Öğrenci, Eğitmen, Admin (RBAC - raporla uyumlu).
/// </summary>
public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // "Student", "Instructor", "Admin"

    public ICollection<User> Users { get; set; } = new List<User>();
}
