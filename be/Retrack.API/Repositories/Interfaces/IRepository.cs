namespace Retrack.API.Repositories.Interfaces;

/// <summary>
/// Generic repository interface — Tất cả repository đều kế thừa từ đây
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
    void Update(T entity);
    void Delete(T entity);
    Task<int> SaveChangesAsync();
}
