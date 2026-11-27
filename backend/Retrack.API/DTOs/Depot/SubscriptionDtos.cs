namespace Retrack.API.DTOs.Depot;

public record DepotSubscriptionDto(Guid DepotId, string Status, bool HasAccess, bool CanStartTrial,
    DateTime? TrialEndsAt, DateTime? PaidUntil, decimal MonthlyPrice, int TrialDays, bool PayOsAvailable);
public record SubscriptionOrderDto(Guid Id, Guid DepotId, string DepotName, decimal Amount, string Status,
    DateTime CreatedAt, DateTime? PaidAt, DateTime? PeriodStart, DateTime? PeriodEnd,
    string? CheckoutUrl, string? ProofUrl, string? PaymentReference, bool PayOsStarted = false, string? ReviewNote = null,
    long OrderCode = 0, DateTime? PeriodRevokedAt = null);
public record CreateSubscriptionOrderDto(Guid OperationId);
public record SubscriptionProofDto(string ProofUrl, string Reference);
public record SubscriptionApprovalDto(string Reference);
public record SubscriptionRejectionDto(string Reason);
public record SubscriptionPriceDto(decimal MonthlyPrice);
/// <summary>Thông tin gói công khai cho trang giới thiệu; không chứa dữ liệu kho hay người dùng.</summary>
public record PublicDepotPlanDto(bool SubscriptionOnly, decimal MonthlyPrice, int TrialDays);
// PaidRevenue là tiền gói đã thu; NetRevenue trừ phần đã thực chuyển hoàn. Tiền hàng không bao giờ được cộng vào đây.
public record SubscriptionAdminSummaryDto(decimal MonthlyPrice, decimal PaidRevenue, int PaidOrderCount, int PendingProofCount,
    decimal RefundedAmount = 0, decimal ApprovedAwaitingTransfer = 0, int OpenRefundCount = 0, decimal NetRevenue = 0,
    decimal RevenueThisMonth = 0, int ActiveDepotCount = 0, int TrialDepotCount = 0, int ExpiredDepotCount = 0);

// ── Hoàn tiền gói ──
public record SubscriptionRefundRequestDto(Guid OperationId, Guid OrderId, string Kind, decimal Amount, string Reason,
    string? EvidenceUrl = null, string? EvidenceReference = null);
public record SubscriptionRefundDecisionDto(bool Approve, decimal? Amount, string Note);
public record SubscriptionRefundTransferDto(string Reference, string? ProofUrl = null);
public record SubscriptionRefundDto(Guid Id, Guid OrderId, long OrderCode, Guid DepotId, string DepotName, decimal OrderAmount, string OrderStatus,
    DateTime? OrderPeriodStart, DateTime? OrderPeriodEnd, string Kind, string Status, string RequestedRole, DateTime RequestedAt,
    decimal RequestedAmount, string Reason, string? EvidenceUrl, string? EvidenceReference, decimal? ApprovedAmount, DateTime? DecidedAt,
    string? DecisionNote, DateTime? RefundedAt, string? RefundReference, string? RefundProofUrl, bool PeriodRevoked);
