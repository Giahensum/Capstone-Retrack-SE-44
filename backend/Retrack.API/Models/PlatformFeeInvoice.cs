using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models;

// Admin phát hành/đối soát; Chủ kho vựa chỉ được gửi xác nhận thanh toán.
[Table("platform_fee_invoices")]
public sealed class PlatformFeeInvoice
{
    [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("owner_id")] public Guid OwnerId { get; set; }
    [Column("period_start")] public DateOnly PeriodStart { get; set; }
    [Column("amount")] public decimal Amount { get; set; }
    [Column("status")] public string Status { get; set; } = "UNPAID";
    [Column("payment_proof_url")] public string? PaymentProofUrl { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("submitted_at")] public DateTime? SubmittedAt { get; set; }
    public User Owner { get; set; } = null!;
}
