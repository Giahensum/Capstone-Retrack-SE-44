using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public record DepotCostLayer(Guid Id, string SourceType, string Label, decimal Quantity, decimal? UnitCost, DateTime ReceivedAt);
public interface IDepotStockRepository
{
    Task<bool> IsActiveStaffAsync(Guid userId, Guid depotId);
    Task<DepotStockReceipt?> FindReceiptAsync(Guid id);
    Task<PagedResult<DepotStockReceipt>> ListAsync(Guid depotId, DepotQuery query);
    void AddReceipt(DepotStockReceipt receipt);
    void ReplaceExpenses(DepotStockReceipt receipt, List<StockExpenseInput> expenses);
    Task<PagedResult<ReusableStockDto>> ReusableAsync(Guid depotId, DepotQuery query);
    Task<DepotStockLine?> FindLineAsync(Guid depotId, Guid lineId);
    Task<decimal> SoldAsync(Guid lineId);
    Task<DepotReuseSale?> FindSaleAsync(Guid id);
    void AddSale(DepotReuseSale sale);
    Task<PagedResult<ReuseSaleDto>> SalesAsync(Guid depotId, DepotQuery query);
    Task<List<DepotCostLayer>> AvailableLayersAsync(Guid depotId, string material);
    void AddAllocations(IEnumerable<DepotBatchCostSource> sources);
    Task<InventoryBatch?> FindBatchAsync(Guid depotId, Guid batchId);
    Task<List<BatchCostSourceDto>> BatchSourcesAsync(Guid batchId);
    Task<List<BatchExpenseDto>> BatchExpensesAsync(Guid batchId);
    Task<DepotBatchExpense?> FindExpenseAsync(Guid id);
    void AddExpense(DepotBatchExpense expense);
    Task<decimal?> CurrentLineCostAsync(Guid lineId);
    Task<DepotStockCostAdjustment?> FindAdjustmentAsync(Guid id);
    void AddAdjustment(DepotStockCostAdjustment adjustment);
    Task<PagedResult<StockAdjustmentDto>> AdjustmentsAsync(Guid receiptId, DepotQuery query);
    Task<bool> HasLateCostsAsync(Guid batchId);
    Task<List<StockLineCostBalanceDto>> CurrentReceiptCostsAsync(Guid receiptId);
    Task ForwardCostAsync(Guid lineId, Guid adjustmentId, decimal? unitCost);
}
