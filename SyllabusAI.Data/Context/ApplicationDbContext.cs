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
    public DbSet<FeedbackQuestion> FeedbackQuestions { get; set; }
    public DbSet<CourseFeedbackAnswer> CourseFeedbackAnswers { get; set; }

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

        modelBuilder.Entity<FeedbackQuestion>(e =>
        {
            e.HasIndex(x => x.QuestionNo).IsUnique();
            e.Property(x => x.Text).HasMaxLength(1000);
        });

        modelBuilder.Entity<CourseFeedbackAnswer>(e =>
        {
            e.HasIndex(x => new { x.CourseFeedbackId, x.FeedbackQuestionId }).IsUnique();
            e.HasOne(x => x.CourseFeedback).WithMany(f => f.Answers).HasForeignKey(x => x.CourseFeedbackId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.FeedbackQuestion).WithMany(q => q.Answers).HasForeignKey(x => x.FeedbackQuestionId).OnDelete(DeleteBehavior.Restrict);
        });

        // Varsayılan roller (seed) - ilk çalıştırmada eklenebilir
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Student" },
            new Role { Id = 2, Name = "Instructor" },
            new Role { Id = 3, Name = "Admin" }
        );

        modelBuilder.Entity<FeedbackQuestion>().HasData(
            new FeedbackQuestion { Id = 1, QuestionNo = 1, Text = "Dersin hedefleri ve icerigi donem basinda acik sekilde paylasildi.", IsActive = true },
            new FeedbackQuestion { Id = 2, QuestionNo = 2, Text = "Ders plani (syllabus) ile islenen konular su ana kadar uyumluydu.", IsActive = true },
            new FeedbackQuestion { Id = 3, QuestionNo = 3, Text = "Kullanilan dijital platformlar ders takibi ve etkilesim icin yeterliydi.", IsActive = true },
            new FeedbackQuestion { Id = 4, QuestionNo = 4, Text = "Sunulan ders materyalleri (notlar, sunumlar vb.) yeterli ve faydaliydi.", IsActive = true },
            new FeedbackQuestion { Id = 5, QuestionNo = 5, Text = "Ders kapsaminda onerilen ek kaynaklara ve okumalara erisim kolaydi.", IsActive = true },
            new FeedbackQuestion { Id = 6, QuestionNo = 6, Text = "Islenen ders konularini su ana kadar teorik duzeyde iyi anlayabildim.", IsActive = true },
            new FeedbackQuestion { Id = 7, QuestionNo = 7, Text = "Konularin pekismesi icin sinifta daha fazla tekrar veya uygulama yapilmasi gerektigini dusunuyorum.", IsActive = true },
            new FeedbackQuestion { Id = 8, QuestionNo = 8, Text = "Dersin islenisi sirasinda farkli ogrenme hizlarina ve seviyelerine sahip ogrenciler gozetildi.", IsActive = true },
            new FeedbackQuestion { Id = 9, QuestionNo = 9, Text = "Ders kapsaminda yapilan ornekler ve uygulamalar, gercek hayattaki/mesleki senaryolarla iyi iliskilendirildi.", IsActive = true },
            new FeedbackQuestion { Id = 10, QuestionNo = 10, Text = "Dersin islenis temposu (ilerleyis hizi), konulari sindirmem ve not almam icin uygundur.", IsActive = true },
            new FeedbackQuestion { Id = 11, QuestionNo = 11, Text = "Anlatilan konularin zorluk seviyesi, sahip oldugum on bilgilerle ve yetkinligimle ortusmektedir.", IsActive = true },
            new FeedbackQuestion { Id = 12, QuestionNo = 12, Text = "Ders sirasinda soru sorma, tartismaya katilma ve fikir beyan etme konusunda kendimi tesvik edilmis hissediyorum.", IsActive = true },
            new FeedbackQuestion { Id = 13, QuestionNo = 13, Text = "Verilen odevler, projeler veya kisa sinavlar ogrenme surecime gercek anlamda katki saglamaktadir.", IsActive = true },
            new FeedbackQuestion { Id = 14, QuestionNo = 14, Text = "Dersin icerigi ve hocanin anlatim tarzi, konuya olan merakimi ve derse olan motivasyonumu canli tutmaktadir.", IsActive = true },
            new FeedbackQuestion { Id = 15, QuestionNo = 15, Text = "Egitmen, karmasik kavramlari aciklarken farkli yontemler (gorsel araclar, benzetmeler, vaka analizleri vb.) kullanarak anlasilirligi artirmaktadir.", IsActive = true }
        );
    }
}
