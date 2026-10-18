using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Shared;

namespace Retrack.API.Repositories;

public sealed class DepotStockRepository(AppDbContext db) : IDepotStockRepository
{
    public Task<bool> IsActiveStaffAsync(Guid userId, Guid depotId) => db.DepotStaffs.AnyAsync(x =>
        x.DepotId == depotId && x.UserId == userId && x.IsActive && x.StaffType == "DEPOT_EMPLOYEE" &&
        x.User.IsActive && x.User.Role == "DEPOT_EMPLOYEE");
    public Task<DepotStockReceipt?> FindReceiptAsync(Guid id) => db.DepotStockReceipts
        .Include(x => x.Lines).Include(x => x.Expenses).AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id);
    public Task<PagedResult<DepotStockReceipt>> ListAsync(Guid depotId, DepotQuery query)
    {
        var source = db.DepotStockReceipts.AsNoTracking().Where(x => x.DepotId == depotId);
        if (!string.IsNullOrWhiteSpace(query.Status)) source = source.Where(x => x.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(x => x.Reference.Contains(query.Search));
        return PageAsync(source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Include(x => x.Lines).Include(x => x.Expenses).AsSplitQuery(), query);
    }
    public void AddReceipt(DepotStockReceipt receipt) => db.DepotStockReceipts.Add(receipt);
    public void ReplaceExpenses(DepotStockReceipt receipt, List<StockExpenseInput> expenses)
    {
        db.DepotStockExpenses.RemoveRange(receipt.Expenses);
        receipt.Expenses = expenses.Select(x => new DepotStockExpense {
            ReceiptId = receipt.Id, Category = x.Category, Description = x.Description.Trim(), Amount = x.Amount }).ToList();
    }
    public Task<PagedResult<ReusableStockDto>> ReusableAsync(Guid depotId, DepotQuery query)
    {
        var source = db.DepotStockLines.AsNoTracking().Where(x => x.Receipt.DepotId == depotId &&
            x.Receipt.Status == "POSTED" && x.Kind == "REUSABLE");
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(x => x.Name.Contains(query.Search));
        return PageAsync(source.OrderByDescending(x => x.Receipt.ReceivedAt).ThenBy(x => x.Id).Select(x =>
            new ReusableStockDto(x.Id, x.Name, x.Unit, x.ConditionNote, x.Receipt.Reference, x.Quantity,
                db.DepotReuseSales.Where(s => s.LineId == x.Id).Sum(s => (decimal?)s.Quantity) ?? 0,
                x.Quantity - (db.DepotReuseSales.Where(s => s.LineId == x.Id).Sum(s => (decimal?)s.Quantity) ?? 0),
                x.AllocatedCost / x.Quantity)), query);
    }
    public Task<DepotStockLine?> FindLineAsync(Guid depotId, Guid lineId) => db.DepotStockLines.Include(x => x.Receipt)
        .SingleOrDefaultAsync(x => x.Id == lineId && x.Receipt.DepotId == depotId);
    public async Task<decimal> SoldAsync(Guid lineId) => await db.DepotReuseSales.Where(x => x.LineId == lineId)
        .SumAsync(x => (decimal?)x.Quantity) ?? 0;
    public Task<DepotReuseSale?> FindSaleAsync(Guid id) => db.DepotReuseSales.Include(x => x.Line).ThenInclude(x => x.Receipt).SingleOrDefaultAsync(x => x.Id == id);
    public void AddSale(DepotReuseSale sale) => db.DepotReuseSales.Add(sale);
    public Task<PagedResult<ReuseSaleDto>> SalesAsync(Guid depotId, DepotQuery query) => PageAsync(db.DepotReuseSales.AsNoTracking()
        .Where(x => x.Line.Receipt.DepotId == depotId).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
        .Select(x => new ReuseSaleDto(x.Id, x.LineId, x.Line.Name, x.Quantity, x.Quantity * x.UnitPrice, x.SellingCost,
            x.Line.AllocatedCost / x.Line.Quantity * x.Quantity,
            x.Quantity * x.UnitPrice - x.SellingCost - x.Line.AllocatedCost / x.Line.Quantity * x.Quantity,
            x.Note, x.CreatedAt)), query);

    private IQueryable<InventoryBatch> ActiveBatches(Guid depotId, string[] names) => db.InventoryBatches.Where(b =>
        b.DepotId == depotId && names.Contains(b.MaterialType) && b.Status != "CANCELLED" &&
        (b.Status != "REJECTED" || b.FactoryReceivedAt != null || b.QualityCheck != null ||
            (b.TransportJob != null && (b.TransportJob.Status == "PICKED_UP" || b.TransportJob.Status == "DELIVERED"))));

    public async Task<List<DepotCostLayer>> AvailableLayersAsync(Guid depotId, string material)
    {
        var names = MaterialCatalog.Values(material);
        var layers = await db.PickupRequestItems.AsNoTracking().Where(x => x.PickupRequest.TargetDepotId == depotId &&
            x.PickupRequest.Status == "DONE" && names.Contains(x.MaterialType))
            .Select(x => new DepotCostLayer(x.Id, "PICKUP", "Thu gom " + x.PickupRequestId, x.WeightKg,
                (decimal?)x.PricePerKg, x.PickupRequest.CreatedAt)).ToListAsync();
        layers.AddRange(await db.DepotStockLines.AsNoTracking().Where(x => x.Receipt.DepotId == depotId &&
            x.Receipt.Status == "POSTED" && x.Kind == "MATERIAL" && names.Contains(x.MaterialType!))
            .Select(x => new DepotCostLayer(x.Id, "RECEIPT", x.Receipt.Reference, x.Quantity,
                x.AllocatedCost / x.Quantity, x.Receipt.ReceivedAt)).ToListAsync());
        var active = ActiveBatches(depotId, names);
        var used = await db.DepotBatchCostSources.AsNoTracking().Where(x => active.Any(b => b.Id == x.BatchId))
            .GroupBy(x => new { x.SourceId, x.SourceType }).Select(g => new { g.Key.SourceId, g.Key.SourceType, Quantity = g.Sum(x => x.Quantity) }).ToListAsync();
        var legacy = await active.Where(b => !db.DepotBatchCostSources.Any(x => x.BatchId == b.Id))
            .SumAsync(b => (decimal?)b.DeclaredWeightKg) ?? 0;
        var result = new List<DepotCostLayer>();
        // Lô cũ chưa có snapshot vẫn chiếm lượng tồn; không cộng lại DONE thành một phiếu nhận mới.
        foreach (var layer in layers.OrderBy(x => x.ReceivedAt).ThenBy(x => x.Id))
        {
            var left = Math.Max(0, layer.Quantity - used.Where(x => x.SourceId == layer.Id && x.SourceType == layer.SourceType).Sum(x => x.Quantity));
            var legacyUsed = Math.Min(left, legacy);
            legacy -= legacyUsed;
            left -= legacyUsed;
            if (left > 0) result.Add(layer with { Quantity = left });
        }
        return result;
    }
    public void AddAllocations(IEnumerable<DepotBatchCostSource> sources) => db.DepotBatchCostSources.AddRange(sources);
    public Task<InventoryBatch?> FindBatchAsync(Guid depotId, Guid batchId) => db.InventoryBatches
        .SingleOrDefaultAsync(x => x.Id == batchId && x.DepotId == depotId);
    public Task<List<BatchCostSourceDto>> BatchSourcesAsync(Guid batchId) => db.DepotBatchCostSources.AsNoTracking()
        .Where(x => x.BatchId == batchId).OrderBy(x => x.SourceLabel).ThenBy(x => x.Id)
        .Select(x => new BatchCostSourceDto(x.SourceType, x.SourceLabel, x.Quantity, x.UnitCost)).ToListAsync();
    public Task<List<BatchExpenseDto>> BatchExpensesAsync(Guid batchId) => db.DepotBatchExpenses.AsNoTracking()
        .Where(x => x.BatchId == batchId).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
        .Select(x => new BatchExpenseDto(x.Id, x.Description, x.Amount, x.CreatedAt)).ToListAsync();
    public Task<DepotBatchExpense?> FindExpenseAsync(Guid id) => db.DepotBatchExpenses.SingleOrDefaultAsync(x => x.Id == id);
    public void AddExpense(DepotBatchExpense expense) => db.DepotBatchExpenses.Add(expense);
    private static async Task<PagedResult<T>> PageAsync<T>(IQueryable<T> source, DepotQuery query)
    {
        var total = await source.CountAsync();
        var offset = (long)(query.Page - 1) * query.PageSize;
        return new() { Page = query.Page, PageSize = query.PageSize, TotalCount = total,
            Items = offset > int.MaxValue ? [] : await source.Skip((int)offset).Take(query.PageSize).ToListAsync() };
    }
}
