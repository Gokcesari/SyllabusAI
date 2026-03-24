using Microsoft.EntityFrameworkCore;
using SyllabusAI.Models;

namespace SyllabusAI.Data;

/// <summary>
/// Geliştirme ortamında veritabanı boşsa örnek kullanıcı ekler.
/// GitHub'tan indirilen DB kullanıldığında bu seed atlanabilir (zaten kullanıcılar var).
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        // Kullanıcıları varsa tekrar ekleme; ama ders seed'i için devam edebiliriz.
        var hasUsers = await db.Users.AnyAsync(ct);

        var studentRoleId = 1;
        var instructorRoleId = 2;

        if (!hasUsers)
        {
            await db.Users.AddRangeAsync(new[]
            {
                new User
                {
                    Email = "ogrenci@bahcesehir.edu.tr",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                    FullName = "Test Öğrenci",
                    RoleId = studentRoleId
                },
                new User
                {
                    Email = "egitmen@bau.edu.tr",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                    FullName = "Test Eğitmen",
                    RoleId = instructorRoleId
                },
                new User
                {
                    Email = "hoca@ou.bau.edu.tr",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                    FullName = "Test Eğitmen (OU)",
                    RoleId = instructorRoleId
                }
            }, ct);

            await db.SaveChangesAsync(ct);
        }

        // Ekranların “boş” görünmemesi için örnek ders + kayıt (isteğe bağlı)
        var hasCourses = await db.Courses.AnyAsync(ct);
        if (!hasCourses)
        {
            var instructor = await db.Users.FirstOrDefaultAsync(u => u.Email == "egitmen@bau.edu.tr", ct);
            var student = await db.Users.FirstOrDefaultAsync(u => u.Email == "ogrenci@bahcesehir.edu.tr", ct);
            if (instructor != null)
            {
                var course = new Course
                {
                    CourseCode = "INE2001",
                    Title = "Statistics",
                    InstructorId = instructor.Id,
                    HighlightKeywords = "sınav,devam,not",
                    SyllabusContent =
                        "Devam kuralı: 4 devamsızlık hakkı.\n" +
                        "Sınav tarihleri: Vize 7. hafta, Final 15. hafta.\n" +
                        "Notlandırma: Vize %40, Final %60.\n" +
                        "Haftalık plan: 1-2 Giriş, 3-4 Grafikler, 5-6 Olasılık..."
                };
                db.Courses.Add(course);
                await db.SaveChangesAsync(ct);

                if (student != null)
                {
                    db.Enrollments.Add(new Enrollment { UserId = student.Id, CourseId = course.Id });
                    await db.SaveChangesAsync(ct);
                }
            }
        }
    }
}
