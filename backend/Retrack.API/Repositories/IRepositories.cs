using Retrack.API.Models;
using Retrack.API.Models.Enums;

namespace Retrack.API.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByIdAsync(Guid id);
        Task<User> CreateAsync(User user);
        Task<User> UpdateAsync(User user);
        Task<List<User>> GetAllAsync();
        Task<(List<User> Items, int Total)> SearchAsync(string? keyword, string? role, bool? isActive, int page, int pageSize);
        Task<Dictionary<string, int>> CountByRoleAsync();
        Task DeleteAsync(User user);
    }

    public interface IMarketPriceRepository
    {
        Task<MarketPrice?> GetByIdAsync(Guid id);
        Task<List<MarketPrice>> GetAllAsync(MaterialType? materialType = null);
        Task<MarketPrice> CreateAsync(MarketPrice price);
        Task<MarketPrice> UpdateAsync(MarketPrice price);
        Task DeleteAsync(MarketPrice price);
    }

    public interface IAuditLogRepository
    {
        Task<AuditLog> CreateAsync(AuditLog log);
        Task<(List<AuditLog> Items, int Total)> SearchAsync(string? entityName, Guid? userId, int page, int pageSize);
    }

    public interface IPlatformInvoiceRepository
    {
        Task<PlatformInvoice?> GetByIdAsync(Guid id);
        Task<PlatformInvoice?> GetByPayerAndPeriodAsync(Guid payerId, int year, int month);
        Task<(List<PlatformInvoice> Items, int Total)> SearchAsync(string? status, int page, int pageSize);
        Task<PlatformInvoice> CreateAsync(PlatformInvoice invoice);
        Task<PlatformInvoice> UpdateAsync(PlatformInvoice invoice);
    }

    public interface IPickupRequestRepository
    {
        Task<PickupRequest?> GetByIdAsync(Guid id);
        Task<List<PickupRequest>> GetBySellerIdAsync(Guid sellerId);
        Task<List<PickupRequest>> GetByDepotIdAsync(Guid depotId);
        Task<List<PickupRequest>> GetPendingAsync();
        Task<PickupRequest> CreateAsync(PickupRequest request);
        Task<PickupRequest> UpdateAsync(PickupRequest request);
        Task<List<PickupRequest>> GetAllAsync(int page, int pageSize);
        Task<int> CountAsync();
    }
}

