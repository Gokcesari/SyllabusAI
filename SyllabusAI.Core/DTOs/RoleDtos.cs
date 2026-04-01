namespace SyllabusAI.DTOs;

public class RoleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Örnek: Student (öğrenci), Instructor (eğitmen/hoca), Admin. JWT [Authorize(Roles=...)] ile aynı isim olmalı.
/// </summary>
public class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
}
