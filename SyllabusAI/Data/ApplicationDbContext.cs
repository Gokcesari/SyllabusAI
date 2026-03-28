using Microsoft.EntityFrameworkCore;
using SyllabusAI.Models;

namespace SyllabusAI.Data;

/// <summary>
/// Veritabanı bağlamı. Connection string appsettings.json içinden okunur.
/// GitHub'tan indirilen DB'ye bağlanmak için ConnectionStrings:DefaultConnection değerini güncelleyin.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<Enrollment> Enrollments { get; set; }
    public DbSet<SyllabusPdfUpload> SyllabusPdfUploads { get; set; }
    public DbSet<SyllabusChunk> SyllabusChunks { get; set; }
    public DbSet<CourseFeedback> CourseFeedbacks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.HasOne(u => u.Role).WithMany(r => r.Users).HasForeignKey(u => u.RoleId);
        });

        modelBuilder.Entity<Course>(e =>
        {
            e.HasIndex(c => new { c.CourseCode, c.InstructorId }).IsUnique();
            e.HasOne(c => c.Instructor).WithMany().HasForeignKey(c => c.InstructorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Enrollment>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.CourseId }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Course).WithMany(c => c.Enrollments).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SyllabusPdfUpload>(e =>
        {
            e.HasIndex(x => x.CourseId);
            e.HasOne(x => x.Course).WithMany(c => c.SyllabusPdfUploads).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SyllabusChunk>(e =>
        {
            e.HasIndex(x => new { x.CourseId, x.ChunkIndex });
            e.HasOne(x => x.Course).WithMany(c => c.SyllabusChunks).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CourseFeedback>(e =>
        {
            e.HasIndex(x => new { x.CourseId, x.StudentUserId }).IsUnique();
            e.HasOne(x => x.Course).WithMany(c => c.Feedbacks).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // Varsayılan roller (seed) - ilk çalıştırmada eklenebilir
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Student" },
            new Role { Id = 2, Name = "Instructor" },
            new Role { Id = 3, Name = "Admin" }
        );
    }
}
