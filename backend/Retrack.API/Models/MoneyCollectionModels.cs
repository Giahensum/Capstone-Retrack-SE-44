using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models;

/// <summary>Trạng thái khoản thu (khớp CHECK ck_money_collections_status của RETRACK_MONEY_V1).</summary>
public static class CollectionStates
{
    public const string Pending = "PENDING";
    public const string Paid = "PAID";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";
    public const string Refunding = "REFUNDING";
    public const string Refunded = "REFUNDED";
}

/// <summary>Loại bất thường khi thu tiền (khớp CHECK ck_money_anomalies_kind của RETRACK_MONEY_V3).</summary>
public static class CollectionAnomalyKinds
{
    public const string Underpaid = "UNDERPAID";
    public const string Overpaid = "OVERPAID";
    public const string LatePayment = "LATE_PAYMENT";
    public const string DuplicatePayment = "DUPLICATE_PAYMENT";
    public const string SourceMismatch = "SOURCE_MISMATCH";
}

/// <summary>Mã đơn PayOS đã cấp cho một khoản thu (kể cả link đã hết hạn).</summary>
[Table("money_collection_orders")]
public class MoneyCollectionOrder
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None), Column("order_code")] public long OrderCode { get; set; }
    [Column("collection_id")] public Guid CollectionId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Tiền về bất thường cần Admin xử lý tay (hoàn tiền cho người trả).</summary>
[Table("money_collection_anomalies")]
public class MoneyCollectionAnomaly
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("collection_id")] public Guid CollectionId { get; set; }
    [Column("order_code")] public long? OrderCode { get; set; }
    [Column("kind"), MaxLength(30)] public string Kind { get; set; } = "";
    [Column("received_amount")] public decimal ReceivedAmount { get; set; }
    [Column("expected_amount")] public decimal ExpectedAmount { get; set; }
    /// <summary>Số tiền cần hoàn tay cho người trả.</summary>
    [Column("refund_amount")] public decimal RefundAmount { get; set; }
    [Column("reference"), MaxLength(100)] public string Reference { get; set; } = "";
    [Column("status"), MaxLength(20)] public string Status { get; set; } = "OPEN";
    [Column("note")] public string? Note { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("resolved_at")] public DateTime? ResolvedAt { get; set; }
    [Column("resolved_by")] public Guid? ResolvedBy { get; set; }
}
