using System.Linq.Expressions;

namespace SyllabusAI.Core.Interfaces;

public interface IRepository<T> where T : class
{
    IQueryable<T> Query();
    T? GetById(int id);
    IEnumerable<T> GetAll();
    IEnumerable<T> Find(Expression<Func<T, bool>> predicate);
    void Add(T entity);
    void Update(T entity);
    void Delete(T entity);
    void DeleteById(int id);
}
