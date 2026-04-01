using SyllabusAI.Core.Interfaces;
using SyllabusAI.Models;

namespace SyllabusAI.Data.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
        Users = new Repository<User>(_context);
        Roles = new Repository<Role>(_context);
        Courses = new Repository<Course>(_context);
        Enrollments = new Repository<Enrollment>(_context);
        SyllabusPdfUploads = new Repository<SyllabusPdfUpload>(_context);
        SyllabusChunks = new Repository<SyllabusChunk>(_context);
        CourseFeedbacks = new Repository<CourseFeedback>(_context);
    }

    public IRepository<User> Users { get; }
    public IRepository<Role> Roles { get; }
    public IRepository<Course> Courses { get; }
    public IRepository<Enrollment> Enrollments { get; }
    public IRepository<SyllabusPdfUpload> SyllabusPdfUploads { get; }
    public IRepository<SyllabusChunk> SyllabusChunks { get; }
    public IRepository<CourseFeedback> CourseFeedbacks { get; }

    public int SaveChanges() => _context.SaveChanges();
}
