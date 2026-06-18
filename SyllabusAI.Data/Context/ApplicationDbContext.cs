using Microsoft.EntityFrameworkCore;
using SyllabusAI.Models;

namespace SyllabusAI.Data;

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
    public DbSet<CourseWeeklyFeedback> CourseWeeklyFeedbacks { get; set; }
    public DbSet<WeeklyFeedbackQuestion> WeeklyFeedbackQuestions { get; set; }
    public DbSet<CourseWeeklyFeedbackAnswer> CourseWeeklyFeedbackAnswers { get; set; }
    public DbSet<ChatSession> ChatSessions { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }

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
            e.HasIndex(x => new { x.CourseId, x.NormalizedCategory });
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

        modelBuilder.Entity<CourseWeeklyFeedback>(e =>
        {
            e.HasIndex(x => new { x.CourseId, x.StudentUserId }).IsUnique();
            e.HasOne(x => x.Course).WithMany(c => c.WeeklyFeedbacks).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WeeklyFeedbackQuestion>(e =>
        {
            e.HasIndex(x => x.QuestionNo).IsUnique();
            e.Property(x => x.Text).HasMaxLength(1000);
        });

        modelBuilder.Entity<CourseWeeklyFeedbackAnswer>(e =>
        {
            e.HasIndex(x => new { x.CourseWeeklyFeedbackId, x.WeeklyFeedbackQuestionId }).IsUnique();
            e.HasOne(x => x.CourseWeeklyFeedback).WithMany(f => f.Answers).HasForeignKey(x => x.CourseWeeklyFeedbackId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.WeeklyFeedbackQuestion).WithMany(q => q.Answers).HasForeignKey(x => x.WeeklyFeedbackQuestionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatSession>(e =>
        {
            e.HasIndex(x => new { x.StudentUserId, x.CourseId, x.CreatedAtUtc });
            e.HasOne(x => x.StudentUser).WithMany().HasForeignKey(x => x.StudentUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Course).WithMany(c => c.ChatSessions).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasIndex(x => new { x.ChatSessionId, x.CreatedAtUtc });
            e.Property(x => x.Role).HasMaxLength(32);
            e.HasOne(x => x.ChatSession).WithMany(s => s.Messages).HasForeignKey(x => x.ChatSessionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Student" },
            new Role { Id = 2, Name = "Instructor" },
            new Role { Id = 3, Name = "Admin" }
        );

        modelBuilder.Entity<FeedbackQuestion>().HasData(
            new FeedbackQuestion { Id = 1, QuestionNo = 1, Text = "Course objectives and content were clearly communicated at the start of the term.", IsActive = true },
            new FeedbackQuestion { Id = 2, QuestionNo = 2, Text = "Topics covered so far align with the syllabus.", IsActive = true },
            new FeedbackQuestion { Id = 3, QuestionNo = 3, Text = "Digital platforms used were adequate for following the course and interaction.", IsActive = true },
            new FeedbackQuestion { Id = 4, QuestionNo = 4, Text = "Course materials (notes, slides, etc.) were sufficient and useful.", IsActive = true },
            new FeedbackQuestion { Id = 5, QuestionNo = 5, Text = "Additional resources and readings suggested in the course were easy to access.", IsActive = true },
            new FeedbackQuestion { Id = 6, QuestionNo = 6, Text = "So far I understand the topics covered at a theoretical level.", IsActive = true },
            new FeedbackQuestion { Id = 7, QuestionNo = 7, Text = "I think more in-class review or practice is needed to reinforce topics.", IsActive = true },
            new FeedbackQuestion { Id = 8, QuestionNo = 8, Text = "During instruction, students with different learning speeds and levels were considered.", IsActive = true },
            new FeedbackQuestion { Id = 9, QuestionNo = 9, Text = "Examples and activities in the course were well linked to real-world or professional scenarios.", IsActive = true },
            new FeedbackQuestion { Id = 10, QuestionNo = 10, Text = "The pace of the course suits my ability to digest topics and take notes.", IsActive = true },
            new FeedbackQuestion { Id = 11, QuestionNo = 11, Text = "The difficulty of topics matches my prior knowledge and skills.", IsActive = true },
            new FeedbackQuestion { Id = 12, QuestionNo = 12, Text = "I feel encouraged to ask questions, join discussions, and share ideas in class.", IsActive = true },
            new FeedbackQuestion { Id = 13, QuestionNo = 13, Text = "Assignments, projects, or quizzes meaningfully support my learning.", IsActive = true },
            new FeedbackQuestion { Id = 14, QuestionNo = 14, Text = "The content and the instructor's style keep my interest and motivation for the course.", IsActive = true },
            new FeedbackQuestion { Id = 15, QuestionNo = 15, Text = "The instructor explains complex concepts using different methods to improve clarity.", IsActive = true }
        );

        modelBuilder.Entity<WeeklyFeedbackQuestion>().HasData(
            new WeeklyFeedbackQuestion { Id = 1, QuestionNo = 1, Text = "This week's lesson was productive overall.", IsActive = true },
            new WeeklyFeedbackQuestion { Id = 2, QuestionNo = 2, Text = "The lesson content was clear and understandable.", IsActive = true },
            new WeeklyFeedbackQuestion { Id = 3, QuestionNo = 3, Text = "The instructor's explanation was effective.", IsActive = true },
            new WeeklyFeedbackQuestion { Id = 4, QuestionNo = 4, Text = "The examples given in the lesson helped me understand the topic.", IsActive = true },
            new WeeklyFeedbackQuestion { Id = 5, QuestionNo = 5, Text = "There was sufficient interaction and participation during the lesson.", IsActive = true },
            new WeeklyFeedbackQuestion { Id = 6, QuestionNo = 6, Text = "I believe I can apply what I learned in this lesson.", IsActive = true },
            new WeeklyFeedbackQuestion { Id = 7, QuestionNo = 7, Text = "This week's lesson met my expectations.", IsActive = true }
        );
    }
}
