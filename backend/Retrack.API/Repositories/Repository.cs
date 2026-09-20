using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

/// <summary>
/// Generic repository implementation — Mọi repository đều kế thừa từ đây
/// Chỉ cần tạo class mới kế thừa Repository<T> là có đầy đủ CRUD
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id) => await _dbSet.FindAsync(id);
    public async Task<IEnumerable<T>> GetAllAsync() => await _dbSet.ToListAsync();
    public async Task AddAsync(T entity) => await _dbSet.AddAsync(entity);
    public void Update(T entity) => _dbSet.Update(entity);
    public void Delete(T entity) => _dbSet.Remove(entity);
    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
}
