using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;

namespace Retrack.API.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _db;

        public UserRepository(AppDbContext db) => _db = db;

        public async Task<User?> GetByEmailAsync(string email)
            => await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        public async Task<User?> GetByIdAsync(Guid id)
            => await _db.Users.FindAsync(id);

        public async Task<User> CreateAsync(User user)
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }

        public async Task<User> UpdateAsync(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
            return user;
        }

        public async Task<List<User>> GetAllAsync()
            => await _db.Users.ToListAsync();
    }

    public class PickupRequestRepository : IPickupRequestRepository
    {
        private readonly AppDbContext _db;

        public PickupRequestRepository(AppDbContext db) => _db = db;

        public async Task<PickupRequest?> GetByIdAsync(Guid id)
            => await _db.PickupRequests
                .Include(p => p.Seller)
                .Include(p => p.TargetDepot)
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id);

        public async Task<List<PickupRequest>> GetBySellerIdAsync(Guid sellerId)
            => await _db.PickupRequests
                .Include(p => p.Items)
                .Include(p => p.TargetDepot)
                .Where(p => p.SellerId == sellerId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

        public async Task<List<PickupRequest>> GetByDepotIdAsync(Guid depotId)
            => await _db.PickupRequests
                .Include(p => p.Seller)
                .Include(p => p.Items)
                .Where(p => p.TargetDepotId == depotId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

        public async Task<List<PickupRequest>> GetPendingAsync()
            => await _db.PickupRequests
                .Include(p => p.Seller)
                .Where(p => p.Status == "PENDING")
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

        public async Task<PickupRequest> CreateAsync(PickupRequest request)
        {
            _db.PickupRequests.Add(request);
            await _db.SaveChangesAsync();
            return request;
        }

        public async Task<PickupRequest> UpdateAsync(PickupRequest request)
        {
            request.UpdatedAt = DateTime.UtcNow;
            _db.PickupRequests.Update(request);
            await _db.SaveChangesAsync();
            return request;
        }

        public async Task<List<PickupRequest>> GetAllAsync(int page, int pageSize)
            => await _db.PickupRequests
                .Include(p => p.Seller)
                .Include(p => p.TargetDepot)
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

        public async Task<int> CountAsync()
            => await _db.PickupRequests.CountAsync();
    }
}

