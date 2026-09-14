using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Retrack.API.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email)
        => await _dbSet.FirstOrDefaultAsync(u => u.Email == email);
}
