using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Retrack.API.Models;

[Table("depot_cost_reconciliations")]
public sealed class DepotCostReconciliation
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("sequence_no"), DatabaseGenerated(DatabaseGeneratedOption.Identity)] public long SequenceNo { get; set; }
    [Column("adjustment_id")] public Guid AdjustmentId { get; set; }
    public DepotStockCostAdjustment Adjustment { get; set; } = null!;
    [Column("batch_source_id")] public Guid? BatchSourceId { get; set; }
    public DepotBatchCostSource? BatchSource { get; set; }
    [Column("reuse_sale_id")] public Guid? ReuseSaleId { get; set; }
    public DepotReuseSale? ReuseSale { get; set; }
    [Column("unit_cost", TypeName = "numeric(30,9)")] public decimal? UnitCost { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
