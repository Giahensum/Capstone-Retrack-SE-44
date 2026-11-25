namespace Retrack.API.DTOs.Depot;

public record DepotSubscriptionDto(Guid DepotId, string Status, bool HasAccess, bool CanStartTrial,
    DateTime? TrialEndsAt, DateTime? PaidUntil, decimal MonthlyPrice, int TrialDays, bool PayOsAvailable);
public record SubscriptionOrderDto(Guid Id, Guid DepotId, string DepotName, decimal Amount, string Status,
    DateTime CreatedAt, DateTime? PaidAt, DateTime? PeriodStart, DateTime? PeriodEnd,
    string? CheckoutUrl, string? ProofUrl, string? PaymentReference, bool PayOsStarted = false, string? ReviewNote = null);
public record CreateSubscriptionOrderDto(Guid OperationId);
public record SubscriptionProofDto(string ProofUrl, string Reference);
public record SubscriptionApprovalDto(string Reference);
public record SubscriptionRejectionDto(string Reason);
public record SubscriptionPriceDto(decimal MonthlyPrice);
public record SubscriptionAdminSummaryDto(decimal MonthlyPrice, decimal PaidRevenue, int PaidOrderCount, int PendingProofCount);
