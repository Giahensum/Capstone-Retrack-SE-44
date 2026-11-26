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
                o.CheckoutUrl, o.ProofUrl, o.PaymentReference, o.ProviderStartedAt != null, o.ReviewNote, o.OrderCode, o.PeriodRevokedAt)).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new() { Items = items, Page = page, PageSize = pageSize, TotalCount = count };
    }
    public async Task<SubscriptionAdminSummaryDto> SummaryAsync()
    {
        var settings = await SettingsAsync();
        var now = DateTime.UtcNow;
        // Đầu tháng theo giờ Việt Nam.
        var local = now.AddHours(7);
        var monthStart = DateTime.SpecifyKind(new DateTime(local.Year, local.Month, 1).AddHours(-7), DateTimeKind.Utc);
        var paid = await db.DepotSubscriptionOrders.Where(x => x.Status == "PAID").SumAsync(x => (decimal?)x.Amount) ?? 0;
        var refunded = await db.DepotSubscriptionRefunds.Where(x => x.Status == "REFUNDED").SumAsync(x => x.ApprovedAmount) ?? 0;
        return new(Services.Depot.SubscriptionRules.Price(settings), paid,
            await db.DepotSubscriptionOrders.CountAsync(x => x.Status == "PAID"), await db.DepotSubscriptionOrders.CountAsync(x => x.Status == "PENDING" && x.ProofUrl != null),
            refunded, await db.DepotSubscriptionRefunds.Where(x => x.Status == "APPROVED").SumAsync(x => x.ApprovedAmount) ?? 0,
            await db.DepotSubscriptionRefunds.CountAsync(x => x.Status == "REQUESTED"), paid - refunded,
            await db.DepotSubscriptionOrders.Where(x => x.Status == "PAID" && x.PaidAt >= monthStart).SumAsync(x => (decimal?)x.Amount) ?? 0,
            await db.DepotSubscriptions.CountAsync(x => x.PaidUntil > now),
            await db.DepotSubscriptions.CountAsync(x => !(x.PaidUntil > now) && x.TrialEndsAt > now),
            await db.DepotSubscriptions.CountAsync(x => !(x.PaidUntil > now) && !(x.TrialEndsAt > now) && (x.PaidUntil != null || x.TrialStartedAt != null)));
    }
    public async Task<DepotSubscriptionRefund?> RefundAsync(Guid id)
    {
        var refund = await db.DepotSubscriptionRefunds.SingleOrDefaultAsync(x => x.Id == id);
        if (refund != null) await db.Entry(refund).ReloadAsync();
        return refund;
    }
    public Task<List<DepotSubscriptionRefund>> OrderRefundsAsync(Guid orderId) => db.DepotSubscriptionRefunds.Where(x => x.OrderId == orderId).ToListAsync();
    // Hạn trả phí của các kỳ còn hiệu lực khác (bỏ kỳ đang hoàn) để rút đúng một kỳ, không xóa kỳ khác.
    public Task<DateTime?> PreviousPaidEndAsync(Guid depotId, Guid excludeOrderId) => db.DepotSubscriptionOrders
        .Where(x => x.DepotId == depotId && x.Id != excludeOrderId && x.Status == "PAID" && x.PeriodRevokedAt == null).MaxAsync(x => x.PeriodEnd);
    public async Task<PagedResult<SubscriptionRefundDto>> RefundsAsync(Guid? depotId, int page, int pageSize, string? status, Guid? refundId = null)
    {
        var q = db.DepotSubscriptionRefunds.AsNoTracking().Where(x => (depotId == null || x.DepotId == depotId) && (refundId == null || x.Id == refundId));
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);
        var count = await q.CountAsync();
        var items = await (from r in q join o in db.DepotSubscriptionOrders on r.OrderId equals o.Id join d in db.Depots on r.DepotId equals d.Id
            orderby r.Status == "REQUESTED" descending, r.Status == "APPROVED" descending, r.RequestedAt descending, r.Id
            select new SubscriptionRefundDto(r.Id, r.OrderId, o.OrderCode, r.DepotId, d.Name, o.Amount, o.Status, o.PeriodStart, o.PeriodEnd, r.Kind, r.Status,
                r.RequestedRole, r.RequestedAt, r.RequestedAmount, r.Reason, r.EvidenceUrl, r.EvidenceReference, r.ApprovedAmount, r.DecidedAt,
                r.DecisionNote, r.RefundedAt, r.RefundReference, r.RefundProofUrl, r.PeriodRevoked))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new() { Items = items, Page = page, PageSize = pageSize, TotalCount = count };
    }
    // Chống lách dùng thử bằng nhiều kho: cùng mã số thuế chỉ được dùng thử một lần.
    public Task<bool> TrialUsedByTaxCodeAsync(Guid depotId, string taxCode) => (from d in db.Depots join s in db.DepotSubscriptions on d.Id equals s.DepotId
        where d.Id != depotId && d.TaxCode != null && d.TaxCode.Trim().ToUpper() == taxCode && s.TrialStartedAt != null select d.Id).AnyAsync();
    public void Add(DepotSubscriptionRefund refund) => db.DepotSubscriptionRefunds.Add(refund);
    public void Notify(Guid depotId, Guid ownerId, string title, string message) =>
        db.Notifications.Add(Notification.ForDepot(ownerId, depotId, "SUBSCRIPTION", title, message, targetType: "SUBSCRIPTION", targetId: depotId));
    public void Add(DepotSubscription subscription) => db.DepotSubscriptions.Add(subscription);
    public void Add(DepotSubscriptionOrder order) => db.DepotSubscriptionOrders.Add(order);
    public void Audit(Guid actorId, string action, Guid id, string detail) => db.AuditLogs.Add(new() { UserId = actorId, Action = action, EntityName = "depot_subscriptions", EntityId = id, NewData = detail });
    public Task SaveAsync() => db.SaveChangesAsync();
}
