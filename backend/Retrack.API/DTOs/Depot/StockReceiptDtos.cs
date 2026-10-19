using System.ComponentModel.DataAnnotations;

namespace Retrack.API.DTOs.Depot;

public class CreateStockReceiptDto
{
    public Guid OperationId { get; set; }
    [Required, StringLength(40)] public string SourceType { get; set; } = "EXTERNAL_PURCHASE";
    [Required, StringLength(255)] public string Reference { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
    public decimal PurchaseCost { get; set; }
    public bool CostsConfirmed { get; set; }
    public List<StockLineInput> Lines { get; set; } = [];
    public List<StockExpenseInput> Expenses { get; set; } = [];
}
public class StockLineInput
{
    public string Kind { get; set; } = "MATERIAL";
    public string? MaterialType { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "kg";
    public decimal Quantity { get; set; }
    public decimal ReferencePrice { get; set; }
    public decimal DirectCost { get; set; }
    public string? ConditionNote { get; set; }
}
public record StockExpenseInput(string Category, string Description, decimal Amount);
public record StockLineDto(Guid Id, string Kind, string? MaterialType, string Name, string Unit,
    decimal Quantity, decimal ReferencePrice, decimal DirectCost, decimal? AllocatedCost, string? ConditionNote);
public record StockReceiptDto(Guid Id, string SourceType, string Reference, string Status, DateTime ReceivedAt,
    decimal PurchaseCost, bool CostsConfirmed, decimal TotalCost, string? ReviewNote,
    List<StockLineDto> Lines, List<StockExpenseInput> Expenses);
public record ReviewStockReceiptDto([Required, StringLength(1000)] string Reason);
public record StockLineCostInput(Guid Id, decimal ReferencePrice, decimal DirectCost);
public record UpdateStockCostsDto(decimal PurchaseCost, bool CostsConfirmed, List<StockLineCostInput> Lines,
    List<StockExpenseInput> Expenses);
public record ReusableStockDto(Guid Id, string Name, string Unit, string? ConditionNote, string Source,
    decimal ReceivedQuantity, decimal SoldQuantity, decimal AvailableQuantity, decimal? UnitCost);
public record CreateReuseSaleDto(Guid OperationId, decimal Quantity, decimal UnitPrice, decimal SellingCost,
    [Required, StringLength(1000)] string Note);
public record ReuseSaleDto(Guid Id, Guid LineId, string Name, decimal Quantity, decimal Revenue, decimal SellingCost,
    decimal? CostOfGoods, decimal? PretaxDifference, string Note, DateTime CreatedAt);
public record BatchCostSourceDto(string SourceType, string SourceLabel, decimal Quantity, decimal? UnitCost);
public record CreateBatchExpenseDto(Guid OperationId, [Required, StringLength(255)] string Description, decimal Amount);
public record BatchExpenseDto(Guid Id, string Description, decimal Amount, DateTime CreatedAt);
public record BatchCostDto(List<BatchCostSourceDto> Sources, List<BatchExpenseDto> Expenses, bool HasCompleteCost,
    decimal? CostOfGoods, decimal SellingCosts, decimal? Revenue, decimal? PlatformFee,
    decimal? PretaxDifference, bool IsSettled, string Note, bool NeedsCostReconciliation = false);

public record CreateStockAdjustmentDto(Guid OperationId, Guid LineId, string Kind, decimal Amount, string Reason);
public record StockAdjustmentDto(Guid Id, Guid LineId, string LineName, string Kind, decimal Amount, string Reason, Guid CreatedBy, DateTime CreatedAt);
public record StockLineCostBalanceDto(Guid LineId, string Name, decimal Quantity, decimal? OriginalCost, decimal? EffectiveCost);
