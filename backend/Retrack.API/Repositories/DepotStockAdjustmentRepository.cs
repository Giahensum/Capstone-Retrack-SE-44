using Microsoft.EntityFrameworkCore;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
namespace Retrack.API.Repositories;

public sealed partial class DepotStockRepository
{
    private sealed class CostedLine { public DepotStockLine Line { get; set; } = null!; public decimal? Cost { get; set; } }
    private IQueryable<CostedLine> CostLines() => db.DepotStockLines.AsNoTracking().Select(x => new CostedLine {
        Line = x,
        Cost = (x.AllocatedCost ?? db.DepotStockCostAdjustments.Where(a => a.LineId == x.Id && a.Kind == "CONFIRM_BASE_COST").Select(a => (decimal?)a.Amount).FirstOrDefault())
            + (db.DepotStockCostAdjustments.Where(a => a.LineId == x.Id && a.Kind == "ADDITIONAL_COST").Sum(a => (decimal?)a.Amount) ?? 0)
    });
    public Task<decimal?> CurrentLineCostAsync(Guid id) => CostLines().Where(x => x.Line.Id == id).Select(x => x.Cost).SingleOrDefaultAsync();
    public Task<List<StockLineCostBalanceDto>> CurrentReceiptCostsAsync(Guid id) => CostLines().Where(x => x.Line.ReceiptId == id).OrderBy(x => x.Line.Position)
        .Select(x => new StockLineCostBalanceDto(x.Line.Id, x.Line.Name, x.Line.Quantity, x.Line.AllocatedCost, x.Cost)).ToListAsync();
    public Task<DepotStockCostAdjustment?> FindAdjustmentAsync(Guid id) => db.DepotStockCostAdjustments.Include(x => x.Line).ThenInclude(x => x.Receipt).SingleOrDefaultAsync(x => x.Id == id);
    public void AddAdjustment(DepotStockCostAdjustment row) => db.DepotStockCostAdjustments.Add(row);
    public Task<PagedResult<StockAdjustmentDto>> AdjustmentsAsync(Guid id, DepotQuery query) => PageAsync(db.DepotStockCostAdjustments.AsNoTracking()
        .Where(x => x.Line.ReceiptId == id).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
        .Select(x => new StockAdjustmentDto(x.Id, x.LineId, x.Line.Name, x.Kind, x.Amount, x.Reason, x.CreatedBy, x.CreatedAt)), query);
    public Task<bool> HasLateCostsAsync(Guid id) => db.DepotBatchCostSources.AnyAsync(s => s.BatchId == id && s.SourceType == "RECEIPT" &&
        db.DepotStockCostAdjustments.Any(a => a.LineId == s.SourceId && a.CreatedAt > s.Batch.CreatedAt));
}
