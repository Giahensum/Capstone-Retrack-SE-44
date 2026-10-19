using System.ComponentModel.DataAnnotations.Schema;
namespace Retrack.API.Models;
[Table("depot_stock_cost_adjustments")]
public sealed class DepotStockCostAdjustment
{
    [Column("id")] public Guid Id { get; set; }
    [Column("line_id")] public Guid LineId { get; set; }
    public DepotStockLine Line { get; set; } = null!;
    [Column("kind")] public string Kind { get; set; } = "ADDITIONAL_COST";
    [Column("amount", TypeName = "numeric(18,0)")] public decimal Amount { get; set; }
    [Column("reason")] public string Reason { get; set; } = "";
    [Column("created_by")] public Guid CreatedBy { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
