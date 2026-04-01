using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SyllabusAI.Core.Interfaces;

namespace SyllabusAI.Data.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    private readonly ApplicationDbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = _context.Set<T>();
    }

    public IQueryable<T> Query() => _dbSet.AsQueryable();

    public T? GetById(int id) => _dbSet.Find(id);

    public IEnumerable<T> GetAll() => _dbSet.ToList();

    public IEnumerable<T> Find(Expression<Func<T, bool>> predicate) => _dbSet.Where(predicate).ToList();

    public void Add(T entity) => _dbSet.Add(entity);

    public void Update(T entity) => _dbSet.Update(entity);

    public void Delete(T entity) => _dbSet.Remove(entity);

    public void DeleteById(int id)
    {
        var entity = GetById(id);
        if (entity is not null)
            _dbSet.Remove(entity);
    }
}
