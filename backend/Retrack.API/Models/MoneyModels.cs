using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models;

/// <summary>Hằng số dòng tiền của sổ cái thu/chi qua sàn (khớp CHECK trong khối RETRACK_MONEY_V1).</summary>
public static class MoneyFlows
{
    public const string Pickup = "PICKUP";
    public const string Batch = "BATCH";
    public const string Liquidation = "LIQUIDATION";
    public const string LiquidationDeposit = "LIQUIDATION_DEPOSIT";
    public const string LiquidationBalance = "LIQUIDATION_BALANCE";
    /// <summary>Dòng hoa hồng/sổ cái: PICKUP, BATCH, LIQUIDATION.</summary>
    public static readonly string[] CommissionFlows = [Pickup, Batch, Liquidation];
}

public static class MoneySources
{
    public const string PickupRequest = "PICKUP_REQUEST";
    public const string InventoryBatch = "INVENTORY_BATCH";
    public const string LiquidationAgreement = "LIQUIDATION_AGREEMENT";
}

public static class PayableKinds { public const string Sale = "SALE"; public const string Refund = "REFUND"; }

public static class PayableStates
{
    public const string Held = "HELD";
    public const string Available = "AVAILABLE";
    public const string InPayout = "IN_PAYOUT";
    public const string Paid = "PAID";
    public const string Reversed = "REVERSED";
}

[Table("money_collections")]
public class MoneyCollection
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("flow"), MaxLength(30)] public string Flow { get; set; } = "";
    [Column("source_type"), MaxLength(40)] public string SourceType { get; set; } = "";
    [Column("source_id")] public Guid SourceId { get; set; }
    [Column("payer_id")] public Guid PayerId { get; set; }
    [Column("payee_id")] public Guid PayeeId { get; set; }
    [Column("amount")] public decimal Amount { get; set; }
    [Column("status"), MaxLength(20)] public string Status { get; set; } = "PENDING";
    [Column("payos_order_code")] public long? PayosOrderCode { get; set; }
    [Column("payos_checkout_url")] public string? PayosCheckoutUrl { get; set; }
    [Column("payos_link_id")] public string? PayosLinkId { get; set; }
    [Column("paid_at")] public DateTime? PaidAt { get; set; }
    [Column("received_amount")] public decimal? ReceivedAmount { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("payable_lines")]
public class PayableLine
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("collection_id")] public Guid CollectionId { get; set; }
    [Column("payee_id")] public Guid PayeeId { get; set; }
    [Column("flow"), MaxLength(30)] public string Flow { get; set; } = "";
    [Column("source_type"), MaxLength(40)] public string SourceType { get; set; } = "";
    [Column("source_id")] public Guid SourceId { get; set; }
    [Column("kind"), MaxLength(10)] public string Kind { get; set; } = PayableKinds.Sale;
    /// <summary>NULL cho SALE; bắt buộc cho REFUND (mỗi lần hoàn một phần một khóa).</summary>
    [Column("refund_key"), MaxLength(100)] public string? RefundKey { get; set; }
    [Column("gross")] public decimal Gross { get; set; }
    [Column("commission_rate")] public decimal CommissionRate { get; set; }
    [Column("commission")] public decimal Commission { get; set; }
    [Column("state"), MaxLength(20)] public string State { get; set; } = PayableStates.Held;
    [Column("available_at")] public DateTime AvailableAt { get; set; }
    [Column("payout_id")] public Guid? PayoutId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("payout_runs")]
public class PayoutRun
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("code"), MaxLength(40)] public string Code { get; set; } = "";
    [Column("payee_id")] public Guid PayeeId { get; set; }
    [Column("period_start")] public DateTime PeriodStart { get; set; }
    [Column("period_end")] public DateTime PeriodEnd { get; set; }
    [Column("gross")] public decimal Gross { get; set; }
    [Column("commission")] public decimal Commission { get; set; }
    [Column("refund_amount")] public decimal RefundAmount { get; set; }
    [Column("carried_debt")] public decimal CarriedDebt { get; set; }
    [Column("net")] public decimal Net { get; set; }
    [Column("status"), MaxLength(20)] public string Status { get; set; } = "PENDING";
    [Column("provider"), MaxLength(20)] public string Provider { get; set; } = "SIMULATED";
    [Column("provider_ref"), MaxLength(100)] public string? ProviderRef { get; set; }
    [Column("gateway_status"), MaxLength(40)] public string? GatewayStatus { get; set; }
    [Column("fail_reason")] public string? FailReason { get; set; }
    [Column("bank_snapshot", TypeName = "jsonb")] public string? BankSnapshot { get; set; }
    [Column("approved_by")] public Guid? ApprovedBy { get; set; }
    [Column("approved_at")] public DateTime? ApprovedAt { get; set; }
    [Column("created_by")] public Guid? CreatedBy { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("paid_at")] public DateTime? PaidAt { get; set; }
    [Column("version")] public int Version { get; set; }
}

[Table("payee_commissions")]
public class PayeeCommission
{
    [Column("user_id")] public Guid UserId { get; set; }
    [Column("flow"), MaxLength(30)] public string Flow { get; set; } = "";
    [Column("rate")] public decimal Rate { get; set; }
    [Column("updated_by")] public Guid? UpdatedBy { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("payee_payout_settings")]
public class PayeePayoutSetting
{
    [Key, Column("user_id")] public Guid UserId { get; set; }
    [Column("auto_enabled")] public bool AutoEnabled { get; set; } = true;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
