using Microsoft.EntityFrameworkCore.Storage;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public interface IDepotSubscriptionRepository
{
    Task<IDbContextTransaction> BeginAsync();
    Task<Depot> LockDepotAsync(Guid depotId);
    Task<bool> IsActiveOwnerAsync(Guid ownerId, Guid depotId);
    Task<bool> IsAdminAsync(Guid userId);
    Task<DepotSubscription?> FindAsync(Guid depotId);
    Task<DepotSubscriptionOrder?> OrderAsync(Guid id);
    Task<DepotSubscriptionOrder?> OrderByCodeAsync(long code);
    Task<DepotSubscriptionOrder?> PendingAsync(Guid depotId);
    Task<long> NextCodeAsync();
    Task<List<Guid>> PendingPayOsAsync();
    Task MarkCheckedAsync(Guid id, DateTime now);
    Task<Dictionary<string, string>> SettingsAsync();
    Task SetPriceAsync(decimal price, Guid actorId);
    Task<PagedResult<SubscriptionOrderDto>> ListAsync(Guid? depotId, int page, int pageSize, string? status);
    Task<SubscriptionAdminSummaryDto> SummaryAsync();
    void Add(DepotSubscription subscription);
    void Add(DepotSubscriptionOrder order);
    void Audit(Guid actorId, string action, Guid id, string detail);
    Task<DepotSubscriptionRefund?> RefundAsync(Guid id);
    Task<List<DepotSubscriptionRefund>> OrderRefundsAsync(Guid orderId);
    Task<DateTime?> PreviousPaidEndAsync(Guid depotId, Guid excludeOrderId);
    Task<PagedResult<SubscriptionRefundDto>> RefundsAsync(Guid? depotId, int page, int pageSize, string? status, Guid? refundId = null);
    Task<bool> TrialUsedByTaxCodeAsync(Guid depotId, string taxCode);
    void Add(DepotSubscriptionRefund refund);
    void Notify(Guid depotId, Guid ownerId, string title, string message);
    Task SaveAsync();
}
