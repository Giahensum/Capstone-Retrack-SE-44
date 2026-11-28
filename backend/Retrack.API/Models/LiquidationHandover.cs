using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models;

// Mỗi lần đề xuất giữ một bản riêng; không ghi đè số lượng/đơn giá thắng đã chụp.
[Table("liquidation_handovers")]
public sealed class LiquidationHandover
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("agreement_id")] public Guid AgreementId { get; set; }
    [ForeignKey(nameof(AgreementId))] public LiquidationAgreement Agreement { get; set; } = null!;
    [Column("revision")] public int Revision { get; set; }
    [Column("status"), MaxLength(20)] public string Status { get; set; } = "PENDING";
    [Column("request_hash"), MaxLength(64)] public string RequestHash { get; set; } = "";
    [Column("amount", TypeName = "numeric(18,0)")] public decimal Amount { get; set; }
    [Column("note"), MaxLength(1000)] public string? Note { get; set; }
    [Column("proposed_by")] public Guid ProposedBy { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("decision_note"), MaxLength(1000)] public string? DecisionNote { get; set; }
    [Column("decided_by")] public Guid? DecidedBy { get; set; }
    [Column("decided_at")] public DateTime? DecidedAt { get; set; }
    public List<LiquidationHandoverLine> Lines { get; set; } = [];
}
[Table("liquidation_handover_lines")]
public sealed class LiquidationHandoverLine
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("handover_id")] public Guid HandoverId { get; set; }
    [ForeignKey(nameof(HandoverId))] public LiquidationHandover Handover { get; set; } = null!;
    [Column("agreement_item_id")] public Guid AgreementItemId { get; set; }
    [ForeignKey(nameof(AgreementItemId))] public LiquidationAgreementItem AgreementItem { get; set; } = null!;
    [Column("quantity", TypeName = "numeric(18,3)")] public decimal Quantity { get; set; }
    [Column("unit_price", TypeName = "numeric(18,0)")] public decimal UnitPrice { get; set; }
    [Column("amount", TypeName = "numeric(18,0)")] public decimal Amount { get; set; }
}
