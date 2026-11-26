using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models;

[Table("depot_subscriptions")]
public sealed class DepotSubscription
{
    [Key, Column("depot_id")] public Guid DepotId { get; set; }
    [Column("trial_started_at")] public DateTime? TrialStartedAt { get; set; }
    [Column("trial_ends_at")] public DateTime? TrialEndsAt { get; set; }
    [Column("paid_until")] public DateTime? PaidUntil { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("depot_subscription_orders")]
public sealed class DepotSubscriptionOrder
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("depot_id")] public Guid DepotId { get; set; }
    [Column("payer_id")] public Guid PayerId { get; set; }
    [Column("amount")] public decimal Amount { get; set; }
    [Column("status"), MaxLength(20)] public string Status { get; set; } = "PENDING";
    [Column("order_code")] public long OrderCode { get; set; }
    [Column("checkout_url")] public string? CheckoutUrl { get; set; }
    [Column("provider_started_at")] public DateTime? ProviderStartedAt { get; set; }
    [Column("last_checked_at")] public DateTime? LastCheckedAt { get; set; }
    [Column("proof_url")] public string? ProofUrl { get; set; }
    [Column("payment_reference"), MaxLength(255)] public string? PaymentReference { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("paid_at")] public DateTime? PaidAt { get; set; }
    [Column("period_start")] public DateTime? PeriodStart { get; set; }
    [Column("period_end")] public DateTime? PeriodEnd { get; set; }
    [Column("approved_by")] public Guid? ApprovedBy { get; set; }
    [Column("review_note"), MaxLength(1000)] public string? ReviewNote { get; set; }
    // Kỳ đã được hoàn tiền (kỳ chưa bắt đầu): không còn tính vào hạn dùng; đơn vẫn PAID vì tiền đã thu thật.
    [Column("period_revoked_at")] public DateTime? PeriodRevokedAt { get; set; }
}

/// <summary>Yêu cầu hoàn tiền gói. Duyệt (APPROVED) khác với đã chuyển tiền hoàn thật (REFUNDED).</summary>
[Table("depot_subscription_refunds")]
public sealed class DepotSubscriptionRefund
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("order_id")] public Guid OrderId { get; set; }
    [Column("depot_id")] public Guid DepotId { get; set; }
    [Column("kind"), MaxLength(30)] public string Kind { get; set; } = "";
    [Column("status"), MaxLength(20)] public string Status { get; set; } = "REQUESTED";
    [Column("requested_by")] public Guid RequestedBy { get; set; }
    [Column("requested_role"), MaxLength(10)] public string RequestedRole { get; set; } = "OWNER";
    [Column("requested_at")] public DateTime RequestedAt { get; set; }
    [Column("requested_amount")] public decimal RequestedAmount { get; set; }
    [Column("reason"), MaxLength(1000)] public string Reason { get; set; } = "";
    [Column("evidence_url")] public string? EvidenceUrl { get; set; }
    [Column("evidence_reference"), MaxLength(255)] public string? EvidenceReference { get; set; }
    [Column("approved_amount")] public decimal? ApprovedAmount { get; set; }
    [Column("decided_by")] public Guid? DecidedBy { get; set; }
    [Column("decided_at")] public DateTime? DecidedAt { get; set; }
    [Column("decision_note"), MaxLength(1000)] public string? DecisionNote { get; set; }
    [Column("refunded_by")] public Guid? RefundedBy { get; set; }
    [Column("refunded_at")] public DateTime? RefundedAt { get; set; }
    [Column("refund_reference"), MaxLength(255)] public string? RefundReference { get; set; }
    [Column("refund_proof_url")] public string? RefundProofUrl { get; set; }
    [Column("period_revoked")] public bool PeriodRevoked { get; set; }
}
