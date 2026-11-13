using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Shared;

namespace Retrack.API.Repositories;

public sealed partial class DepotStockRepository(AppDbContext db) : IDepotStockRepository
{
    public Task<bool> IsActiveStaffAsync(Guid userId, Guid depotId) => db.DepotStaffs.AnyAsync(x =>
        x.DepotId == depotId && x.UserId == userId && x.IsActive && x.StaffType == "DEPOT_EMPLOYEE" &&
        x.User.IsActive && x.User.Role == "DEPOT_EMPLOYEE" && x.Depot.Owner.IsActive && x.Depot.Owner.Role == "DEPOT_OWNER");
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
    public Task<DepotStockReceipt?> ReloadReceiptAsync(Guid id)
    {
        // Đọc lại sau khi lấy khóa depot, tránh dùng entity cũ trước khi chờ giao dịch khác.
        foreach (var entry in db.ChangeTracker.Entries().Where(x =>
            x.Entity is DepotStockLine line && line.ReceiptId == id ||
            x.Entity is DepotStockExpense expense && expense.ReceiptId == id ||
            x.Entity is DepotStockReceipt receipt && receipt.Id == id).ToList())
            entry.State = EntityState.Detached;
        return FindReceiptAsync(id);
    }
    public void ReplaceLines(DepotStockReceipt receipt, List<DepotStockLine> lines)
    {
        db.DepotStockLines.RemoveRange(receipt.Lines);
        receipt.Lines = lines;
        db.DepotStockLines.AddRange(lines);
    }
    public void AddNotice(Notification notice) => db.Notifications.Add(notice);
    public Task<Guid> OwnerIdAsync(Guid depotId) => db.Depots.Where(x => x.Id == depotId).Select(x => x.OwnerId).SingleAsync();
    public Task<DepotBulkStockSource?> BulkSourceAsync(Guid agreementId) => db.LiquidationAgreements.AsNoTracking()
        .Where(x => x.Id == agreementId).Select(x => new DepotBulkStockSource(x.DepotId, x.Amount,
            x.PaymentConfirmedAt != null && x.OwnerCollectedAt != null && x.SellerHandedOverAt != null)).SingleOrDefaultAsync();
    public Task<bool> HasBulkReceiptAsync(Guid agreementId) => db.DepotStockReceipts.AnyAsync(x => x.BulkAgreementId == agreementId && x.Status != "VOIDED" && x.Status != "REJECTED");
    public void ReplaceExpenses(DepotStockReceipt receipt, List<StockExpenseInput> expenses)
    {
        db.DepotStockExpenses.RemoveRange(receipt.Expenses);
        receipt.Expenses = expenses.Select(x => new DepotStockExpense {
            ReceiptId = receipt.Id, Category = x.Category, Description = x.Description.Trim(), Amount = x.Amount }).ToList();
        db.DepotStockExpenses.AddRange(receipt.Expenses);
    }
    public Task<PagedResult<ReusableStockDto>> ReusableAsync(Guid depotId, DepotQuery query)
    {
        var source = CostLines().Where(x => x.Line.Receipt.DepotId == depotId && x.Line.Receipt.Status == "POSTED" && x.Line.Kind == "REUSABLE");
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(x => x.Line.Name.Contains(query.Search));
        return PageAsync(source.OrderByDescending(x => x.Line.Receipt.ReceivedAt).ThenBy(x => x.Line.Id).Select(x =>
            new ReusableStockDto(x.Line.Id, x.Line.Name, x.Line.Unit, x.Line.ConditionNote, x.Line.Receipt.Reference, x.Line.Quantity,
                db.DepotReuseSales.Where(s => s.LineId == x.Line.Id).Sum(s => (decimal?)s.Quantity) ?? 0,
                x.Line.Quantity - (db.DepotReuseSales.Where(s => s.LineId == x.Line.Id).Sum(s => (decimal?)s.Quantity) ?? 0),
                x.Cost / x.Line.Quantity)), query);
    }
    public async Task<DepotStockLine?> FindLineAsync(Guid depotId, Guid lineId)
    {
        var line = await db.DepotStockLines.Include(x => x.Receipt).SingleOrDefaultAsync(x => x.Id == lineId && x.Receipt.DepotId == depotId);
        if (line != null)
        {
            await db.Entry(line).ReloadAsync();
            await db.Entry(line.Receipt).ReloadAsync();
        }
        return line;
    }
    public async Task<decimal> SoldAsync(Guid lineId) => await db.DepotReuseSales.Where(x => x.LineId == lineId)
        .SumAsync(x => (decimal?)x.Quantity) ?? 0;
    public Task<DepotReuseSale?> FindSaleAsync(Guid id) => db.DepotReuseSales.Include(x => x.Line).ThenInclude(x => x.Receipt).SingleOrDefaultAsync(x => x.Id == id);
    public void AddSale(DepotReuseSale sale) => db.DepotReuseSales.Add(sale);
    public Task<PagedResult<ReuseSaleDto>> SalesAsync(Guid depotId, DepotQuery query)
    {
        var rows = db.DepotReuseSales.AsNoTracking().Where(x => x.Line.Receipt.DepotId == depotId).Select(x => new {
            Sale = x, EffectiveCost = db.DepotCostReconciliations.Where(r => r.ReuseSaleId == x.Id).OrderByDescending(r => r.SequenceNo).Select(r => r.UnitCost).FirstOrDefault() ?? x.UnitCost,
            Reconciled = db.DepotCostReconciliations.Any(r => r.ReuseSaleId == x.Id)
        });
        return PageAsync(rows.OrderByDescending(x => x.Sale.CreatedAt).ThenBy(x => x.Sale.Id).Select(x => new ReuseSaleDto(
            x.Sale.Id, x.Sale.LineId, x.Sale.Line.Name, x.Sale.Quantity, x.Sale.Quantity * x.Sale.UnitPrice, x.Sale.SellingCost,
            x.Sale.UnitCost * x.Sale.Quantity, x.Sale.Quantity * x.Sale.UnitPrice - x.Sale.SellingCost - x.EffectiveCost * x.Sale.Quantity,
            x.Sale.Note, x.Sale.CreatedAt, x.EffectiveCost * x.Sale.Quantity, x.Reconciled)), query);
    }

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
                (decimal?)x.PricePerKg, x.PickupRequest.StockReceivedAt ?? x.PickupRequest.UpdatedAt)).ToListAsync();
        layers.AddRange(await CostLines().Where(x => x.Line.Receipt.DepotId == depotId &&
            x.Line.Receipt.Status == "POSTED" && x.Line.Kind == "MATERIAL" && names.Contains(x.Line.MaterialType!))
            .Select(x => new DepotCostLayer(x.Line.Id, "RECEIPT", x.Line.Receipt.Reference, x.Line.Quantity,
                x.Cost / x.Line.Quantity, x.Line.Receipt.ReceivedAt)).ToListAsync());
        var active = ActiveBatches(depotId, names);
        var used = await db.DepotBatchCostSources.AsNoTracking().Where(x => active.Any(b => b.Id == x.BatchId))
            .GroupBy(x => new { x.SourceId, x.SourceType }).Select(g => new { g.Key.SourceId, g.Key.SourceType, Quantity = g.Sum(x => x.Quantity) }).ToListAsync();
        var usedBySource = used.ToDictionary(x => (x.SourceType, x.SourceId), x => x.Quantity);
        var legacy = await active.Where(b => !db.DepotBatchCostSources.Any(x => x.BatchId == b.Id))
            .SumAsync(b => (decimal?)b.DeclaredWeightKg) ?? 0;
        var result = new List<DepotCostLayer>();
        // Lô cũ chưa có snapshot vẫn chiếm lượng tồn; không cộng lại DONE thành một phiếu nhận mới.
        foreach (var layer in layers.OrderBy(x => x.ReceivedAt).ThenBy(x => x.Id))
        {
            var left = Math.Max(0, layer.Quantity - usedBySource.GetValueOrDefault((layer.SourceType, layer.Id)));
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
    public Task<List<BatchCostSourceDto>> BatchSourcesAsync(Guid batchId, DepotQuery? query = null) => ReadRowsAsync(db.DepotBatchCostSources.AsNoTracking()
        .Where(x => x.BatchId == batchId).OrderBy(x => x.SourceLabel).ThenBy(x => x.Id)
        .Select(x => new BatchCostSourceDto(x.SourceType, x.SourceLabel, x.Quantity, x.UnitCost,
            db.DepotCostReconciliations.Where(r => r.BatchSourceId == x.Id).OrderByDescending(r => r.SequenceNo).Select(r => r.UnitCost).FirstOrDefault(),
            db.DepotCostReconciliations.Any(r => r.BatchSourceId == x.Id))), query);
    public Task<List<BatchExpenseDto>> BatchExpensesAsync(Guid batchId, DepotQuery? query = null) => ReadRowsAsync(db.DepotBatchExpenses.AsNoTracking()
        .Where(x => x.BatchId == batchId).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
        .Select(x => new BatchExpenseDto(x.Id, x.Description, x.Amount, x.CreatedAt)), query);

    // Tính trên toàn lô, độc lập với trang nguồn và trang chi phí đang xem.
    public Task<BatchCostTotals> BatchCostTotalsAsync(Guid batchId) => db.Database.SqlQuery<BatchCostTotals>($"""
        WITH sources AS (
            SELECT s.quantity, s.unit_cost,
                   CASE WHEN r.found THEN r.unit_cost ELSE s.unit_cost END AS effective_cost,
                   r.found
            FROM depot_batch_cost_sources s
            LEFT JOIN LATERAL (
                SELECT unit_cost, TRUE AS found FROM depot_cost_reconciliations
                WHERE batch_source_id = s.id ORDER BY sequence_no DESC LIMIT 1
            ) r ON TRUE
            WHERE s.batch_id = {batchId}
        ), expenses AS (
            SELECT COUNT(*)::int AS count, COALESCE(SUM(amount), 0) AS amount
            FROM depot_batch_expenses WHERE batch_id = {batchId}
        )
        SELECT COUNT(*)::int AS "SourceCount", COALESCE(SUM(quantity), 0) AS "Quantity",
               (COUNT(*) FILTER (WHERE unit_cost IS NULL))::int AS "MissingOriginal",
               (COUNT(*) FILTER (WHERE effective_cost IS NULL))::int AS "MissingEffective",
               COALESCE(SUM(quantity * unit_cost), 0) AS "OriginalCost",
               COALESCE(SUM(quantity * effective_cost), 0) AS "EffectiveCost",
               (SELECT count FROM expenses) AS "ExpenseCount",
               (SELECT amount FROM expenses) AS "SellingCosts",
               COALESCE(BOOL_OR(found), FALSE) AS "HasReconciliation"
        FROM sources
        """).SingleAsync();

    private static Task<List<T>> ReadRowsAsync<T>(IQueryable<T> source, DepotQuery? query)
    {
        if (query is null) return source.ToListAsync();
        var offset = (long)(query.Page - 1) * query.PageSize;
        return offset > int.MaxValue ? Task.FromResult(new List<T>())
            : source.Skip((int)offset).Take(query.PageSize).ToListAsync();
    }
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
