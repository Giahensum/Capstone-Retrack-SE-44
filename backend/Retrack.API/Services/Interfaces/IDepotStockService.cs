using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;

namespace Retrack.API.Services.Interfaces;

public interface IDepotStockService
{
    Task<StockCostPreviewDto> PreviewCostsAsync(Guid ownerId, Guid depotId, StockCostPreviewInput dto);
    Task<PagedResult<StockReceiptDto>> ListAsync(Guid userId, Guid depotId, DepotQuery query);
    Task<StockReceiptDto> GetAsync(Guid userId, Guid depotId, Guid receiptId);
    Task<StockReceiptDto> CreateAsync(Guid userId, Guid depotId, CreateStockReceiptDto dto);
    Task<StockReceiptDto> UpdateDraftAsync(Guid ownerId, Guid depotId, Guid id, UpdateStockReceiptDto dto);
    Task<StockReceiptDto> ReviewAsync(Guid userId, Guid depotId, Guid id, string action, string? reason = null);
    Task<StockReceiptDto> UpdateCostsAsync(Guid ownerId, Guid depotId, Guid id, UpdateStockCostsDto dto);
    Task<PagedResult<ReusableStockDto>> ReusableAsync(Guid userId, Guid depotId, DepotQuery query);
    Task<ReuseSaleDto> SellAsync(Guid userId, Guid depotId, Guid lineId, CreateReuseSaleDto dto);
    Task<PagedResult<ReuseSaleDto>> SalesAsync(Guid userId, Guid depotId, DepotQuery query);
    Task ReserveCostsAsync(Guid depotId, Guid batchId, string material, decimal quantity);
    Task<BatchCostDto> BatchCostsAsync(Guid ownerId, Guid depotId, Guid batchId, int? sourcePage = null, int? expensePage = null);
    Task AddBatchExpenseAsync(Guid ownerId, Guid depotId, Guid batchId, CreateBatchExpenseDto dto);
    Task AddAdjustmentAsync(Guid ownerId, Guid depotId, Guid receiptId, CreateStockAdjustmentDto dto);
    Task<PagedResult<StockAdjustmentDto>> AdjustmentsAsync(Guid ownerId, Guid depotId, Guid receiptId, DepotQuery query);
    Task<List<StockLineCostBalanceDto>> CurrentReceiptCostsAsync(Guid ownerId, Guid depotId, Guid receiptId);
}
