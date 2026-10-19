using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Depot;

public sealed class DepotStockService(IDepotStockRepository repository, IDepotBatchRepository batches,
    IDepotUnitOfWork work, IDepotService scope) : IDepotStockService
{
    private static void ValidatePage(DepotQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || query.Search?.Length > 255)
            throw new ArgumentException("Phân trang hoặc từ khóa không hợp lệ.");
    }
    private async Task<bool> RequireAccessAsync(Guid userId, Guid depotId)
    {
        try { await scope.RequireOwnerAsync(userId, depotId); return true; }
        catch (DepotForbiddenException) { if (await repository.IsActiveStaffAsync(userId, depotId)) return false; throw; }
    }
    public async Task<PagedResult<StockReceiptDto>> ListAsync(Guid userId, Guid depotId, DepotQuery query)
    {
        var isOwner = await RequireAccessAsync(userId, depotId);
        ValidatePage(query);
        if (query.Status is not (null or "" or "DRAFT" or "SUBMITTED" or "POSTED" or "REJECTED" or "VOIDED")) throw new ArgumentException("Trạng thái phiếu không hợp lệ.");
        var page = await repository.ListAsync(depotId, query);
        return new() { Page = page.Page, PageSize = page.PageSize, TotalCount = page.TotalCount,
            Items = page.Items.Select(x => Map(x, isOwner)).ToList() };
    }
    public async Task<StockReceiptDto> CreateAsync(Guid userId, Guid depotId, CreateStockReceiptDto dto)
    {
        var isOwner = await RequireAccessAsync(userId, depotId);
        if (dto.OperationId == Guid.Empty || dto.SourceType is not ("EXTERNAL_PURCHASE" or "OPENING_BALANCE" or "BULK_PURCHASE") ||
            string.IsNullOrWhiteSpace(dto.Reference) || dto.Reference.Length > 255 || dto.Lines is null || dto.Expenses is null ||
            dto.Lines.Count is < 1 or > 50 || dto.Expenses.Count > 30 || dto.Lines.Any(x => x == null) || dto.Expenses.Any(x => x == null) || dto.ReceivedAt.Kind != DateTimeKind.Utc ||
            dto.ReceivedAt < new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc) || dto.ReceivedAt > DateTime.UtcNow.AddMinutes(5))
            throw new ArgumentException("Cần nguồn, ngày nhận thực tế, mã tham chiếu và từ 1 đến 50 dòng hàng.");
        // Nhân viên chỉ lập số nhận; chủ kho lập/chốt dữ liệu chi phí nội bộ.
        if (!isOwner && (dto.CostsConfirmed || dto.PurchaseCost != 0 || dto.Expenses.Count != 0 || dto.Lines.Any(x => x.DirectCost != 0 || x.ReferencePrice != 0)))
            throw new DepotForbiddenException();
        Money(dto.PurchaseCost);
        foreach (var x in dto.Lines)
        {
            if (x.Kind is not ("MATERIAL" or "REUSABLE") || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 255 ||
                x.Quantity <= 0 || x.Quantity > 1_000_000_000 || decimal.Round(x.Quantity, 3) != x.Quantity || x.ConditionNote?.Length > 1000)
                throw new ArgumentException("Tên, số lượng hoặc tình trạng dòng hàng không hợp lệ.");
            if (x.Kind == "MATERIAL") { x.MaterialType = MaterialCatalog.RequireCode(x.MaterialType ?? ""); x.Unit = "kg"; }
            else if (x.Unit is not ("cái" or "bộ") || decimal.Truncate(x.Quantity) != x.Quantity || x.MaterialType != null)
                throw new ArgumentException("Đồ tái sử dụng đếm nguyên cái/bộ và không đồng thời là dòng vật liệu.");
            Money(x.ReferencePrice); Money(x.DirectCost);
        }
        foreach (var x in dto.Expenses)
        {
            if (x.Category is not ("LABOR" or "FUEL" or "TRANSPORT" or "HANDLING" or "NON_RECOVERABLE_TAX" or "OTHER") ||
                string.IsNullOrWhiteSpace(x.Description) || x.Description.Length > 255) throw new ArgumentException("Chi phí cần nhóm và mô tả rõ.");
            Money(x.Amount);
        }
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto))));
        await using var tx = await work.BeginAsync();
        await batches.LockDepotAsync(depotId);
        var old = await repository.FindReceiptAsync(dto.OperationId);
        if (old != null)
        {
            if (old.DepotId != depotId || old.CreatedBy != userId || old.RequestHash != hash)
                throw new DepotConflictException("Mã thao tác đã dùng cho phiếu khác. Hãy tải lại dữ liệu.");
            return Map(old, isOwner);
        }
        var costs = dto.CostsConfirmed ? DepotCosting.Allocate(dto.PurchaseCost, dto.Expenses.Sum(x => x.Amount),
            dto.Lines.Select(x => new CostingLine(x.Quantity, x.ReferencePrice, x.DirectCost)).ToArray()) : null;
        var receipt = new DepotStockReceipt { Id = dto.OperationId, DepotId = depotId, CreatedBy = userId,
            SourceType = dto.SourceType, Reference = dto.Reference.Trim(), ReceivedAt = dto.ReceivedAt,
            PurchaseCost = dto.PurchaseCost, CostsConfirmed = dto.CostsConfirmed, RequestHash = hash,
            Lines = dto.Lines.Select((x, i) => new DepotStockLine { Kind = x.Kind, MaterialType = x.MaterialType,
                Name = x.Name.Trim(), Unit = x.Unit, Quantity = x.Quantity, ReferencePrice = x.ReferencePrice,
                DirectCost = x.DirectCost, ConditionNote = x.ConditionNote, AllocatedCost = costs?[i], Position = i }).ToList(),
            Expenses = dto.Expenses.Select(x => new DepotStockExpense { Category = x.Category, Description = x.Description.Trim(), Amount = x.Amount }).ToList() };
        repository.AddReceipt(receipt);
        await work.SaveAsync(); await tx.CommitAsync();
        return Map(receipt, isOwner);
    }
    public async Task<StockReceiptDto> UpdateCostsAsync(Guid ownerId, Guid depotId, Guid id, UpdateStockCostsDto dto)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        Money(dto.PurchaseCost);
        if (dto.Lines == null || dto.Expenses == null || dto.Expenses.Count > 30 || dto.Lines.Count is < 1 or > 50 ||
            dto.Lines.Any(x => x == null) || dto.Expenses.Any(x => x == null) || dto.Lines.Select(x => x.Id).Distinct().Count() != dto.Lines.Count)
            throw new ArgumentException("Dữ liệu chi phí không hợp lệ.");
        foreach (var x in dto.Lines) { Money(x.ReferencePrice); Money(x.DirectCost); }
        foreach (var x in dto.Expenses)
        {
            Money(x.Amount);
            if (x.Category is not ("LABOR" or "FUEL" or "TRANSPORT" or "HANDLING" or "NON_RECOVERABLE_TAX" or "OTHER") ||
                string.IsNullOrWhiteSpace(x.Description) || x.Description.Length > 255)
                throw new ArgumentException("Chi phí cần nhóm và mô tả rõ.");
        }
        await using var tx = await work.BeginAsync(); await batches.LockDepotAsync(depotId);
        var receipt = await repository.FindReceiptAsync(id);
        if (receipt == null || receipt.DepotId != depotId) throw new KeyNotFoundException("Không tìm thấy phiếu trong kho.");
        if (receipt.Status is not ("DRAFT" or "SUBMITTED")) throw new DepotConflictException("Chỉ cập nhật chi phí trước khi ghi sổ; không sửa ngầm giá vốn đã xuất.");
        if (dto.Lines.Count != receipt.Lines.Count || receipt.Lines.Any(x => !dto.Lines.Any(l => l.Id == x.Id)))
            throw new ArgumentException("Các dòng chi phí phải khớp đủ dòng hàng của phiếu.");
        var ordered = receipt.Lines.OrderBy(x => x.Position).ToList();
        var inputs = ordered.Select(x => dto.Lines.Single(l => l.Id == x.Id)).ToList();
        var costs = dto.CostsConfirmed ? DepotCosting.Allocate(dto.PurchaseCost, dto.Expenses.Sum(x => x.Amount),
            ordered.Select((x, i) => new CostingLine(x.Quantity, inputs[i].ReferencePrice, inputs[i].DirectCost)).ToArray()) : null;
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].ReferencePrice = inputs[i].ReferencePrice; ordered[i].DirectCost = inputs[i].DirectCost;
            ordered[i].AllocatedCost = costs?[i];
        }
        receipt.PurchaseCost = dto.PurchaseCost; receipt.CostsConfirmed = dto.CostsConfirmed;
        repository.ReplaceExpenses(receipt, dto.Expenses);
        await work.SaveAsync(); await tx.CommitAsync(); return Map(receipt, true);
    }
    public async Task<StockReceiptDto> ReviewAsync(Guid userId, Guid depotId, Guid id, string action, string? reason = null)
    {
        var isOwner = await RequireAccessAsync(userId, depotId);
        if (action != "SUBMITTED" && !isOwner) throw new DepotForbiddenException();
        if (action is not ("SUBMITTED" or "POSTED" or "REJECTED" or "VOIDED")) throw new ArgumentException("Thao tác không hợp lệ.");
        if (action is "REJECTED" or "VOIDED" && (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)) throw new ArgumentException("Cần lý do tối đa 1.000 ký tự.");
        await using var tx = await work.BeginAsync(); await batches.LockDepotAsync(depotId);
        var receipt = await repository.FindReceiptAsync(id);
        if (receipt == null || receipt.DepotId != depotId) throw new KeyNotFoundException("Không tìm thấy phiếu trong kho.");
        if (receipt.Status == action) return Map(receipt, isOwner);
        if (action == "VOIDED")
        {
            if (receipt.Status != "POSTED") throw new DepotConflictException("Chỉ đảo phiếu đã ghi sổ còn nguyên hàng chưa sử dụng.");
            foreach (var material in receipt.Lines.Where(x => x.Kind == "MATERIAL").Select(x => x.MaterialType!).Distinct())
            {
                var available = await repository.AvailableLayersAsync(depotId, material);
                if (receipt.Lines.Where(x => x.MaterialType == material).Any(x => available.SingleOrDefault(s => s.Id == x.Id)?.Quantity != x.Quantity))
                    throw new DepotConflictException("Hàng trong phiếu đã giữ/xuất, không được đảo phiếu nhập.");
            }
            foreach (var line in receipt.Lines.Where(x => x.Kind == "REUSABLE"))
                if (await repository.SoldAsync(line.Id) > 0) throw new DepotConflictException("Đồ trong phiếu đã bán một phần, không được đảo phiếu nhập.");
        }
        else if (receipt.Status is not ("DRAFT" or "SUBMITTED") || (action == "SUBMITTED" && receipt.Status != "DRAFT"))
            throw new DepotConflictException("Phiếu đã chốt, không được ghi sổ lại hoặc sửa lịch sử.");
        if (action == "POSTED") { receipt.PostedAt = DateTime.UtcNow; receipt.PostedBy = userId; }
        receipt.Status = action; receipt.ReviewedBy = userId; receipt.ReviewedAt = DateTime.UtcNow; receipt.ReviewNote = reason?.Trim();
        await work.SaveAsync(); await tx.CommitAsync();
        return Map(receipt, isOwner);
    }
    public async Task<PagedResult<ReusableStockDto>> ReusableAsync(Guid userId, Guid depotId, DepotQuery query)
    {
        await scope.RequireOwnerAsync(userId, depotId); ValidatePage(query);
        return await repository.ReusableAsync(depotId, query);
    }
    public async Task<ReuseSaleDto> SellAsync(Guid userId, Guid depotId, Guid lineId, CreateReuseSaleDto dto)
    {
        await scope.RequireOwnerAsync(userId, depotId);
        if (dto.OperationId == Guid.Empty || dto.Quantity <= 0 || dto.Quantity > 1_000_000_000 || decimal.Truncate(dto.Quantity) != dto.Quantity ||
            string.IsNullOrWhiteSpace(dto.Note) || dto.Note.Length > 1000) throw new ArgumentException("Cần lượng bán nguyên dương và ghi chú giao hàng thực tế.");
        Money(dto.UnitPrice); Money(dto.SellingCost);
        await using var tx = await work.BeginAsync(); await batches.LockDepotAsync(depotId);
        var old = await repository.FindSaleAsync(dto.OperationId);
        if (old != null)
        {
            if (old.Line.Receipt.DepotId != depotId || old.LineId != lineId || old.Quantity != dto.Quantity ||
                old.UnitPrice != dto.UnitPrice || old.SellingCost != dto.SellingCost || old.Note != dto.Note.Trim())
                throw new DepotConflictException("Mã thao tác đã dùng cho lần bán khác.");
            return Sale(old);
        }
        await scope.RequireTradingAsync(userId, depotId);
        var line = await repository.FindLineAsync(depotId, lineId) ?? throw new KeyNotFoundException("Không tìm thấy món đồ trong kho.");
        if (line.Kind != "REUSABLE" || line.Receipt.Status != "POSTED" || dto.Quantity > line.Quantity - await repository.SoldAsync(lineId))
            throw new DepotConflictException("Đồ chưa nhập hoặc số lượng bán vượt lượng còn lại.");
        var sale = new DepotReuseSale { Id = dto.OperationId, Line = line, LineId = line.Id, Quantity = dto.Quantity,
            UnitPrice = dto.UnitPrice, SellingCost = dto.SellingCost, Note = dto.Note.Trim(), CreatedBy = userId };
        repository.AddSale(sale); await work.SaveAsync(); await tx.CommitAsync(); return Sale(sale);
    }
    public async Task<PagedResult<ReuseSaleDto>> SalesAsync(Guid userId, Guid depotId, DepotQuery query)
    {
        await scope.RequireOwnerAsync(userId, depotId); ValidatePage(query); return await repository.SalesAsync(depotId, query);
    }
    // Chỉ gọi bên trong transaction và khóa depot của BatchService.
    public async Task ReserveCostsAsync(Guid depotId, Guid batchId, string material, decimal quantity)
    {
        var layers = await repository.AvailableLayersAsync(depotId, material);
        var take = DepotCosting.Take(quantity, layers.Select(x => new CostingSource(x.Id, x.Quantity, x.UnitCost)).ToArray());
        repository.AddAllocations(take.Select(x => { var source = layers.Single(l => l.Id == x.Id); return new DepotBatchCostSource {
            BatchId = batchId, SourceId = x.Id, SourceType = source.SourceType, SourceLabel = source.Label,
            Quantity = x.Quantity, UnitCost = x.UnitCost == null ? null : decimal.Round(x.UnitCost.Value, 9) }; }));
    }
    public async Task<BatchCostDto> BatchCostsAsync(Guid ownerId, Guid depotId, Guid batchId)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var batch = await repository.FindBatchAsync(depotId, batchId) ?? throw new KeyNotFoundException("Không tìm thấy lô trong kho.");
        var sources = await repository.BatchSourcesAsync(batchId);
        var expenses = await repository.BatchExpensesAsync(batchId);
        var complete = sources.Count > 0 && sources.All(x => x.UnitCost != null) && sources.Sum(x => x.Quantity) == batch.DeclaredWeightKg;
        decimal? cost = complete ? decimal.Round(sources.Sum(x => x.Quantity * x.UnitCost!.Value), 0) : null;
        var selling = expenses.Sum(x => x.Amount);
        var settled = batch.SettledAt != null;
        return new(sources, expenses, complete, cost, selling, settled ? batch.GrossAmount : null,
            settled ? batch.PlatformFeeAmount : null, settled ? batch.NetAmount - cost - selling : null, settled,
            "Phân bổ giá vốn FIFO, không chứng minh nguồn vật lý khi hàng đã trộn. Chênh lệch chỉ gồm chi phí đã ghi, trước thuế thu nhập; chưa đối soát hàng trả/hao hụt. Nguồn pickup cũ chỉ có giá mua, chưa gồm chi phí đầu vào khác.");
    }
    public async Task AddBatchExpenseAsync(Guid ownerId, Guid depotId, Guid batchId, CreateBatchExpenseDto dto)
    {
        await scope.RequireOwnerAsync(ownerId, depotId); Money(dto.Amount);
        if (dto.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Length > 255)
            throw new ArgumentException("Cần mã thao tác và mô tả chi phí.");
        await using var tx = await work.BeginAsync(); await batches.LockDepotAsync(depotId);
        if (await repository.FindBatchAsync(depotId, batchId) == null) throw new KeyNotFoundException("Không tìm thấy lô.");
        var old = await repository.FindExpenseAsync(dto.OperationId);
        if (old != null)
        {
            if (old.BatchId != batchId || old.Amount != dto.Amount || old.Description != dto.Description.Trim())
                throw new DepotConflictException("Mã thao tác đã dùng cho khoản chi khác.");
            return;
        }
        repository.AddExpense(new() { Id = dto.OperationId, BatchId = batchId, Description = dto.Description.Trim(), Amount = dto.Amount, CreatedBy = ownerId });
        await work.SaveAsync(); await tx.CommitAsync();
    }
    private static void Money(decimal value)
    {
        if (value < 0 || value > 1_000_000_000_000 || decimal.Truncate(value) != value)
            throw new ArgumentException("Số tiền phải là số nguyên VND từ 0 đến 1.000 tỷ.");
    }
    private static StockReceiptDto Map(DepotStockReceipt x, bool includeCost) => new(x.Id, x.SourceType, x.Reference,
        x.Status, x.ReceivedAt, includeCost ? x.PurchaseCost : 0, includeCost && x.CostsConfirmed,
        includeCost ? x.PurchaseCost + x.Expenses.Sum(e => e.Amount) + x.Lines.Sum(l => l.DirectCost) : 0, x.ReviewNote,
        x.Lines.OrderBy(l => l.Position).Select(l => new StockLineDto(l.Id, l.Kind, l.MaterialType, l.Name, l.Unit,
            l.Quantity, includeCost ? l.ReferencePrice : 0, includeCost ? l.DirectCost : 0, includeCost ? l.AllocatedCost : null, l.ConditionNote)).ToList(),
        includeCost ? x.Expenses.Select(e => new StockExpenseInput(e.Category, e.Description, e.Amount)).ToList() : []);
    private static ReuseSaleDto Sale(DepotReuseSale x)
    {
        var cost = x.Line.AllocatedCost / x.Line.Quantity * x.Quantity;
        return new(x.Id, x.LineId, x.Line.Name, x.Quantity, x.Quantity * x.UnitPrice, x.SellingCost, cost,
            x.Quantity * x.UnitPrice - x.SellingCost - cost, x.Note, x.CreatedAt);
    }
}
