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
}
