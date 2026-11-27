using System.Text.Json;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;

namespace Retrack.API.Services.Interfaces;

public interface IDepotSubscriptionService
{
    Task<bool> IsSubscriptionOnlyAsync();
    Task<PublicDepotPlanDto> PublicPlanAsync();
    Task<PlatformPaymentAccountDto> PaymentAccountAsync(Guid ownerId, Guid depotId);
    Task<DepotSubscriptionDto> GetAsync(Guid ownerId, Guid depotId);
    Task<DepotSubscriptionDto> StartTrialAsync(Guid ownerId, Guid depotId);
    Task RequireAccessAsync(Guid depotId);
    Task<PagedResult<SubscriptionOrderDto>> OrdersAsync(Guid userId, Guid? depotId, int page, int pageSize, string? status = null);
    Task<SubscriptionOrderDto> CreateOrderAsync(Guid ownerId, Guid depotId, Guid operationId);
    Task<PayOsLinkDto> CreateLinkAsync(Guid ownerId, Guid depotId, Guid orderId);
    Task<string> SyncAsync(Guid ownerId, Guid depotId, Guid orderId);
    Task SubmitProofAsync(Guid ownerId, Guid depotId, Guid orderId, SubscriptionProofDto dto);
    Task ApproveAsync(Guid adminId, Guid orderId, string reference);
    Task RejectProofAsync(Guid adminId, Guid orderId, string reason);
    Task<SubscriptionAdminSummaryDto> AdminSummaryAsync(Guid adminId);
    Task SetPriceAsync(Guid adminId, decimal price);
    Task<bool> WebhookAsync(JsonElement body);
    Task<List<Guid>> PendingPayOsAsync();
    Task ReconcileAsync(Guid orderId);
    // Hoàn tiền gói: depotId = null nghĩa là Admin thao tác.
    Task<SubscriptionRefundDto> RequestRefundAsync(Guid userId, Guid? depotId, SubscriptionRefundRequestDto dto);
    Task<PagedResult<SubscriptionRefundDto>> RefundsAsync(Guid userId, Guid? depotId, int page, int pageSize, string? status = null);
    Task<SubscriptionRefundDto> DecideRefundAsync(Guid adminId, Guid refundId, SubscriptionRefundDecisionDto dto);
    Task<SubscriptionRefundDto> MarkRefundTransferredAsync(Guid adminId, Guid refundId, SubscriptionRefundTransferDto dto);
}
