using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SyllabusAI.Data;
using SyllabusAI.Models;

namespace SyllabusAI.Service.Helpers;

/// <summary>
/// Development: seeds demo users when DB is empty (Seed:CreateDemoUsers).
/// Admin bootstrap uses Seed:AdminEmail / Seed:AdminPassword.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, IConfiguration config, CancellationToken ct = default)
    {
        var seedDemoUsers = config.GetValue<bool>("Seed:CreateDemoUsers");
        var seedDemoCourses = config.GetValue<bool>("Seed:CreateDemoCourses");

        var hasUsers = await db.Users.AnyAsync(ct);

        var studentRoleId = 1;
        var instructorRoleId = 2;
        var adminRoleId = 3;

        if (!hasUsers && seedDemoUsers)
        {
            await db.Users.AddRangeAsync(new[]
            {
                new User
                {
                    Email = "ogrenci@bahcesehir.edu.tr",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                    FullName = "Test Student",
                    RoleId = studentRoleId
                },
                new User
                {
                    Email = "egitmen@bau.edu.tr",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                    FullName = "Test Instructor",
                    RoleId = instructorRoleId
                },
                new User
                {
                    Email = "hoca@ou.bau.edu.tr",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
                    FullName = "Test Instructor (OU)",
                    RoleId = instructorRoleId
                }
            }, ct);

            await db.SaveChangesAsync(ct);
        }

        var adminEmail = config["Seed:AdminEmail"]?.Trim();
        var adminPassword = config["Seed:AdminPassword"];
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == adminEmail.ToLower(), ct);
            if (adminUser == null)
            {
                db.Users.Add(new User
                {
                    Email = adminEmail,
                    FullName = config["Seed:AdminFullName"]?.Trim() ?? "System Admin",
                    RoleId = adminRoleId,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync(ct);
            }
            else if (config.GetValue<bool>("Seed:ResetAdminPasswordFromConfig"))
            {
                adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword);
                await db.SaveChangesAsync(ct);
            }
        }

        var hasCourses = await db.Courses.AnyAsync(ct);
        if (!hasCourses && seedDemoCourses)
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
                    HighlightKeywords = "exam,attendance,grade",
                    SyllabusContent =
                        "Attendance: up to 4 absences allowed.\n" +
                        "Exam dates: Midterm week 7, Final week 15.\n" +
                        "Grading: Midterm 40%, Final 60%.\n" +
                        "Weekly outline: 1-2 Intro, 3-4 Charts, 5-6 Probability..."
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
