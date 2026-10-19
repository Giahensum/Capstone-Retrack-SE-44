using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models;

[Table("depot_stock_receipts")]
public class DepotStockReceipt
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("depot_id")] public Guid DepotId { get; set; }
    [ForeignKey(nameof(DepotId))] public Depot Depot { get; set; } = null!;
    [Column("created_by")] public Guid CreatedBy { get; set; }
    [Column("source_type"), MaxLength(40)] public string SourceType { get; set; } = "EXTERNAL_PURCHASE";
    [Column("reference"), MaxLength(255)] public string Reference { get; set; } = "";
    [Column("status"), MaxLength(20)] public string Status { get; set; } = "DRAFT";
    [Column("received_at")] public DateTime ReceivedAt { get; set; }
    [Column("purchase_cost", TypeName = "numeric(18,0)")] public decimal PurchaseCost { get; set; }
    [Column("costs_confirmed")] public bool CostsConfirmed { get; set; }
    [Column("request_hash"), MaxLength(64)] public string RequestHash { get; set; } = "";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("reviewed_at")] public DateTime? ReviewedAt { get; set; }
    [Column("reviewed_by")] public Guid? ReviewedBy { get; set; }
    [Column("posted_at")] public DateTime? PostedAt { get; set; }
    [Column("posted_by")] public Guid? PostedBy { get; set; }
    [Column("review_note"), MaxLength(1000)] public string? ReviewNote { get; set; }
    public List<DepotStockLine> Lines { get; set; } = [];
    public List<DepotStockExpense> Expenses { get; set; } = [];
}

[Table("depot_stock_lines")]
public class DepotStockLine
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("receipt_id")] public Guid ReceiptId { get; set; }
    [ForeignKey(nameof(ReceiptId))] public DepotStockReceipt Receipt { get; set; } = null!;
    [Column("kind"), MaxLength(20)] public string Kind { get; set; } = "MATERIAL";
    [Column("material_type"), MaxLength(100)] public string? MaterialType { get; set; }
    [Column("name"), MaxLength(255)] public string Name { get; set; } = "";
    [Column("unit"), MaxLength(10)] public string Unit { get; set; } = "kg";
    [Column("quantity", TypeName = "numeric(18,3)")] public decimal Quantity { get; set; }
    [Column("reference_price", TypeName = "numeric(18,0)")] public decimal ReferencePrice { get; set; }
    [Column("direct_cost", TypeName = "numeric(18,0)")] public decimal DirectCost { get; set; }
    [Column("allocated_cost", TypeName = "numeric(18,0)")] public decimal? AllocatedCost { get; set; }
    [Column("condition_note"), MaxLength(1000)] public string? ConditionNote { get; set; }
    [Column("position")] public int Position { get; set; }
}

[Table("depot_stock_expenses")]
public class DepotStockExpense
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("receipt_id")] public Guid ReceiptId { get; set; }
    [ForeignKey(nameof(ReceiptId))] public DepotStockReceipt Receipt { get; set; } = null!;
    [Column("category"), MaxLength(30)] public string Category { get; set; } = "OTHER";
    [Column("description"), MaxLength(255)] public string Description { get; set; } = "";
    [Column("amount", TypeName = "numeric(18,0)")] public decimal Amount { get; set; }
}

[Table("depot_reuse_sales")]
public class DepotReuseSale
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("line_id")] public Guid LineId { get; set; }
    [ForeignKey(nameof(LineId))] public DepotStockLine Line { get; set; } = null!;
    [Column("quantity", TypeName = "numeric(18,3)")] public decimal Quantity { get; set; }
    [Column("unit_price", TypeName = "numeric(18,0)")] public decimal UnitPrice { get; set; }
    [Column("selling_cost", TypeName = "numeric(18,0)")] public decimal SellingCost { get; set; }
    [Column("unit_cost", TypeName = "numeric(30,9)")] public decimal? UnitCost { get; set; }
    [Column("note"), MaxLength(1000)] public string Note { get; set; } = "";
    [Column("created_by")] public Guid CreatedBy { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("depot_batch_cost_sources")]
public class DepotBatchCostSource
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("batch_id")] public Guid BatchId { get; set; }
    [ForeignKey(nameof(BatchId))] public InventoryBatch Batch { get; set; } = null!;
    [Column("source_id")] public Guid SourceId { get; set; }
    [Column("source_type"), MaxLength(20)] public string SourceType { get; set; } = "RECEIPT";
    [Column("source_label"), MaxLength(255)] public string SourceLabel { get; set; } = "";
    [Column("quantity", TypeName = "numeric(18,3)")] public decimal Quantity { get; set; }
    [Column("unit_cost", TypeName = "numeric(30,9)")] public decimal? UnitCost { get; set; }
}

[Table("depot_batch_expenses")]
public class DepotBatchExpense
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("batch_id")] public Guid BatchId { get; set; }
    [ForeignKey(nameof(BatchId))] public InventoryBatch Batch { get; set; } = null!;
    [Column("description"), MaxLength(255)] public string Description { get; set; } = "";
    [Column("amount", TypeName = "numeric(18,0)")] public decimal Amount { get; set; }
    [Column("created_by")] public Guid CreatedBy { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
