using Retrack.API.Data;

namespace Retrack.API.Repositories;

public class Repository<T> : Interfaces.IRepository<T> where T : class
{
    protected readonly AppDbContext _dbSet;
    public Repository(AppDbContext context) { _dbSet = context; }
}
