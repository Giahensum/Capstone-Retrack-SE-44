using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
namespace Retrack.API.Services.Depot;

public sealed partial class DepotStockService
{
    public async Task<PagedResult<StockAdjustmentDto>> AdjustmentsAsync(Guid ownerId, Guid depotId, Guid receiptId, DepotQuery query)
    {
        await RequireReceiptOwner(ownerId, depotId, receiptId); ValidatePage(query);
        return await repository.AdjustmentsAsync(receiptId, query);
    }
    private async Task RequireReceiptOwner(Guid ownerId, Guid depotId, Guid id)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var receipt = await repository.FindReceiptAsync(id);
        if (receipt == null || receipt.DepotId != depotId) throw new KeyNotFoundException("Không tìm thấy phiếu nhập trong kho.");
    }
    public async Task<List<StockLineCostBalanceDto>> CurrentReceiptCostsAsync(Guid ownerId, Guid depotId, Guid receiptId)
    {
        await RequireReceiptOwner(ownerId, depotId, receiptId); return await repository.CurrentReceiptCostsAsync(receiptId);
    }
    public async Task AddAdjustmentAsync(Guid ownerId, Guid depotId, Guid receiptId, CreateStockAdjustmentDto dto)
    {
        await RequireReceiptOwner(ownerId, depotId, receiptId);
        if (dto.OperationId == Guid.Empty || dto.LineId == Guid.Empty || dto.Kind is not ("ADDITIONAL_COST" or "CONFIRM_BASE_COST") ||
            string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > 1000 || decimal.Round(dto.Amount, 0) != dto.Amount || Math.Abs(dto.Amount) > 1_000_000_000_000m)
            throw new ArgumentException("Khoản điều chỉnh cần dòng hàng, số tiền nguyên và lý do tối đa 1000 ký tự.");
        await using var tx = await work.BeginAsync(); await batches.LockDepotAsync(depotId);
        var old = await repository.FindAdjustmentAsync(dto.OperationId);
        if (old != null)
        {
            if (old.Line.Receipt.DepotId != depotId) throw new DepotForbiddenException();
            if (old.LineId != dto.LineId || old.Line.ReceiptId != receiptId || old.Kind != dto.Kind || old.Amount != dto.Amount || old.Reason != dto.Reason.Trim())
                throw new DepotConflictException("Mã thao tác đã dùng cho khoản điều chỉnh khác.");
            await tx.CommitAsync(); return;
        }
        var line = await repository.FindLineAsync(depotId, dto.LineId) ?? throw new KeyNotFoundException("Không tìm thấy dòng hàng.");
        if (line.ReceiptId != receiptId || line.Receipt.Status != "POSTED") throw new DepotConflictException("Chỉ ghi điều chỉnh cho dòng hàng của phiếu đã ghi sổ.");
        var cost = await repository.CurrentLineCostAsync(line.Id);
        if (dto.Kind == "CONFIRM_BASE_COST" && (cost != null || dto.Amount < 0)) throw new DepotConflictException("Chỉ xác nhận vốn gốc một lần cho dòng chưa biết vốn, số tiền không âm.");
        if (dto.Kind == "ADDITIONAL_COST" && (dto.Amount == 0 || dto.Amount < 0 && (cost == null || cost + dto.Amount < 0)))
            throw new ArgumentException("Điều chỉnh phải khác 0 và không được làm tổng giá vốn âm hoặc giảm vốn chưa xác định.");
        repository.AddAdjustment(new() { Id = dto.OperationId, LineId = line.Id, Kind = dto.Kind, Amount = dto.Amount, Reason = dto.Reason.Trim(), CreatedBy = ownerId });
        await work.SaveAsync();
        var effective = await repository.CurrentLineCostAsync(line.Id);
        await repository.ForwardCostAsync(line.Id, dto.OperationId, effective == null ? null : decimal.Round(effective.Value / line.Quantity, 9));
        await work.SaveAsync(); await tx.CommitAsync();
    }
}
