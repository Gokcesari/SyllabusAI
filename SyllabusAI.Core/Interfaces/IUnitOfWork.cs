using SyllabusAI.Models;

namespace SyllabusAI.Core.Interfaces;

public interface IUnitOfWork
{
    IRepository<User> Users { get; }
    IRepository<Role> Roles { get; }
    IRepository<Course> Courses { get; }
    IRepository<Enrollment> Enrollments { get; }
    IRepository<SyllabusPdfUpload> SyllabusPdfUploads { get; }
    IRepository<SyllabusChunk> SyllabusChunks { get; }
    IRepository<CourseFeedback> CourseFeedbacks { get; }
    IRepository<FeedbackQuestion> FeedbackQuestions { get; }
    IRepository<CourseFeedbackAnswer> CourseFeedbackAnswers { get; }
    int SaveChanges();
}
