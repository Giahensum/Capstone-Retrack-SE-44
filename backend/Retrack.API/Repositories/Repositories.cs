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

        public async Task<(List<User> Items, int Total)> SearchAsync(string? keyword, string? role, bool? isActive, int page, int pageSize)
        {
            var query = _db.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var k = keyword.Trim().ToLower();
                query = query.Where(u => u.FullName.ToLower().Contains(k) || u.Email.ToLower().Contains(k) || u.Phone.Contains(k));
            }
            if (!string.IsNullOrWhiteSpace(role))
                query = query.Where(u => u.Role == role);
            if (isActive.HasValue)
                query = query.Where(u => u.IsActive == isActive.Value);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<Dictionary<string, int>> CountByRoleAsync()
            => await _db.Users.GroupBy(u => u.Role).ToDictionaryAsync(g => g.Key, g => g.Count());

        public async Task DeleteAsync(User user)
        {
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
        }
    }

    public class MarketPriceRepository : IMarketPriceRepository
    {
        private readonly AppDbContext _db;

        public MarketPriceRepository(AppDbContext db) => _db = db;

        public async Task<MarketPrice?> GetByIdAsync(Guid id)
            => await _db.MarketPrices.FindAsync(id);

        public async Task<List<MarketPrice>> GetAllAsync(string? materialType = null)
        {
            var query = _db.MarketPrices.AsQueryable();
            if (!string.IsNullOrWhiteSpace(materialType))
                query = query.Where(m => m.MaterialType == materialType);
            return await query.OrderByDescending(m => m.EffectiveDate).ToListAsync();
        }

        public async Task<MarketPrice> CreateAsync(MarketPrice price)
        {
            _db.MarketPrices.Add(price);
            await _db.SaveChangesAsync();
            return price;
        }

        public async Task<MarketPrice> UpdateAsync(MarketPrice price)
        {
            _db.MarketPrices.Update(price);
            await _db.SaveChangesAsync();
            return price;
        }

        public async Task DeleteAsync(MarketPrice price)
        {
            _db.MarketPrices.Remove(price);
            await _db.SaveChangesAsync();
        }
    }

    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly AppDbContext _db;

        public AuditLogRepository(AppDbContext db) => _db = db;

        public async Task<AuditLog> CreateAsync(AuditLog log)
        {
            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync();
            return log;
        }

        public async Task<(List<AuditLog> Items, int Total)> SearchAsync(string? entityName, Guid? userId, int page, int pageSize)
        {
            var query = _db.AuditLogs.Include(a => a.User).AsQueryable();
            if (!string.IsNullOrWhiteSpace(entityName))
                query = query.Where(a => a.EntityName == entityName);
            if (userId.HasValue)
                query = query.Where(a => a.UserId == userId.Value);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }
    }

    public class PlatformInvoiceRepository : IPlatformInvoiceRepository
    {
        private readonly AppDbContext _db;

        public PlatformInvoiceRepository(AppDbContext db) => _db = db;

        public async Task<PlatformInvoice?> GetByIdAsync(Guid id)
            => await _db.PlatformInvoices.Include(i => i.Payer).FirstOrDefaultAsync(i => i.Id == id);

        public async Task<PlatformInvoice?> GetByPayerAndPeriodAsync(Guid payerId, int year, int month)
            => await _db.PlatformInvoices.FirstOrDefaultAsync(i => i.PayerId == payerId && i.PeriodYear == year && i.PeriodMonth == month);

        public async Task<(List<PlatformInvoice> Items, int Total)> SearchAsync(string? status, int page, int pageSize)
        {
            var query = _db.PlatformInvoices.Include(i => i.Payer).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(i => i.Status == status);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(i => i.PeriodYear).ThenByDescending(i => i.PeriodMonth)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<PlatformInvoice> CreateAsync(PlatformInvoice invoice)
        {
            _db.PlatformInvoices.Add(invoice);
            await _db.SaveChangesAsync();
            return invoice;
        }

        public async Task<PlatformInvoice> UpdateAsync(PlatformInvoice invoice)
        {
            _db.PlatformInvoices.Update(invoice);
            await _db.SaveChangesAsync();
            return invoice;
        }
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

