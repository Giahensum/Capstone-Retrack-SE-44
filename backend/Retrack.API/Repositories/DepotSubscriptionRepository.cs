using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class DepotSubscriptionRepository(AppDbContext db) : IDepotSubscriptionRepository
{
    public Task<IDbContextTransaction> BeginAsync() => db.Database.BeginTransactionAsync();
    public async Task<Depot> LockDepotAsync(Guid depotId)
    {
        var depot = await db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId} FOR UPDATE").AsTracking().SingleOrDefaultAsync()
            ?? throw new KeyNotFoundException("Không tìm thấy kho.");
        // Sau khi đợi khóa phải đọc lại các entity đã có trong change tracker.
        await db.Entry(depot).ReloadAsync();
        foreach (var entry in db.ChangeTracker.Entries<DepotSubscription>().Where(x => x.Entity.DepotId == depotId).ToArray()) await entry.ReloadAsync();
        foreach (var entry in db.ChangeTracker.Entries<DepotSubscriptionOrder>().Where(x => x.Entity.DepotId == depotId).ToArray()) await entry.ReloadAsync();
        return depot;
    }
    public Task<bool> IsActiveOwnerAsync(Guid ownerId, Guid depotId) => db.Depots.AnyAsync(x => x.Id == depotId && x.OwnerId == ownerId && x.Owner.IsActive && x.Owner.Role == "DEPOT_OWNER");
    public Task<bool> IsAdminAsync(Guid userId) => db.Users.AnyAsync(x => x.Id == userId && x.IsActive && x.Role == "ADMIN");
    public Task<DepotSubscription?> FindAsync(Guid depotId) => db.DepotSubscriptions.SingleOrDefaultAsync(x => x.DepotId == depotId);
    public Task<DepotSubscriptionOrder?> OrderAsync(Guid id) => db.DepotSubscriptionOrders.SingleOrDefaultAsync(x => x.Id == id);
    public Task<DepotSubscriptionOrder?> OrderByCodeAsync(long code) => db.DepotSubscriptionOrders.SingleOrDefaultAsync(x => x.OrderCode == code);
    public Task<DepotSubscriptionOrder?> PendingAsync(Guid depotId) => db.DepotSubscriptionOrders.SingleOrDefaultAsync(x => x.DepotId == depotId && x.Status == "PENDING");
    public Task<long> NextCodeAsync() => db.Database.SqlQueryRaw<long>("SELECT nextval('depot_subscription_order_code_seq') AS \"Value\"").SingleAsync();
    public Task<List<Guid>> PendingPayOsAsync() => db.DepotSubscriptionOrders.AsNoTracking().Where(x => x.Status == "PENDING" && x.ProviderStartedAt != null)
        .OrderBy(x => x.LastCheckedAt != null).ThenBy(x => x.LastCheckedAt).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => x.Id).Take(20).ToListAsync();
    public Task MarkCheckedAsync(Guid id, DateTime now) => db.DepotSubscriptionOrders.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.LastCheckedAt, now));
    public Task<Dictionary<string, string>> SettingsAsync() => db.SystemConfigs.AsNoTracking()
        .Where(x => x.ConfigKey == "BUSINESS_MODEL" || x.ConfigKey == "SUBSCRIPTION_MONTHLY_PRICE" ||
            x.ConfigKey == "PLATFORM_FEE_BANK_NAME" || x.ConfigKey == "PLATFORM_FEE_ACCOUNT_NUMBER" || x.ConfigKey == "PLATFORM_FEE_ACCOUNT_HOLDER")
        .ToDictionaryAsync(x => x.ConfigKey, x => x.ConfigValue);
    public async Task SetPriceAsync(decimal price, Guid actorId)
    {
        var item = await db.SystemConfigs.FromSqlRaw("SELECT * FROM system_configs WHERE config_key = 'SUBSCRIPTION_MONTHLY_PRICE' FOR UPDATE").AsTracking().SingleAsync();
        await db.Entry(item).ReloadAsync();
        Audit(actorId, "SUBSCRIPTION_PRICE_UPDATED", actorId, $"Giá cũ {item.ConfigValue}; giá mới {price.ToString(CultureInfo.InvariantCulture)}");
        item.ConfigValue = price.ToString(CultureInfo.InvariantCulture); item.UpdatedAt = DateTime.UtcNow;
    }
    public async Task<PagedResult<SubscriptionOrderDto>> ListAsync(Guid? depotId, int page, int pageSize, string? status)
    {
        var q = db.DepotSubscriptionOrders.AsNoTracking().Where(x => depotId == null || x.DepotId == depotId);
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);
        var count = await q.CountAsync();
        var items = await (from o in q join d in db.Depots on o.DepotId equals d.Id
            orderby o.CreatedAt descending, o.Id
            select new SubscriptionOrderDto(o.Id, o.DepotId, d.Name, o.Amount, o.Status, o.CreatedAt, o.PaidAt, o.PeriodStart, o.PeriodEnd,
                o.CheckoutUrl, o.ProofUrl, o.PaymentReference, o.ProviderStartedAt != null, o.ReviewNote)).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new() { Items = items, Page = page, PageSize = pageSize, TotalCount = count };
    }
    public async Task<SubscriptionAdminSummaryDto> SummaryAsync()
    {
        var settings = await SettingsAsync();
        return new(Services.Depot.SubscriptionRules.Price(settings), await db.DepotSubscriptionOrders.Where(x => x.Status == "PAID").SumAsync(x => (decimal?)x.Amount) ?? 0,
            await db.DepotSubscriptionOrders.CountAsync(x => x.Status == "PAID"), await db.DepotSubscriptionOrders.CountAsync(x => x.Status == "PENDING" && x.ProofUrl != null));
    }
    public void Add(DepotSubscription subscription) => db.DepotSubscriptions.Add(subscription);
    public void Add(DepotSubscriptionOrder order) => db.DepotSubscriptionOrders.Add(order);
    public void Audit(Guid actorId, string action, Guid id, string detail) => db.AuditLogs.Add(new() { UserId = actorId, Action = action, EntityName = "depot_subscriptions", EntityId = id, NewData = detail });
    public Task SaveAsync() => db.SaveChangesAsync();
}
