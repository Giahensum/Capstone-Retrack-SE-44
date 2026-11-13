using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;

namespace Retrack.API.Services.Depot;

public sealed partial class DepotStockService
{
    public async Task<StockReceiptDto> UpdateDraftAsync(Guid ownerId, Guid depotId, Guid id, UpdateStockReceiptDto dto)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (dto.ExpectedRevision?.Length != 64) throw new ArgumentException("Cần phiên bản phiếu đang sửa. Hãy tải lại phiếu.");
        await using var tx = await work.BeginAsync();
        await batches.LockDepotAsync(depotId);
        var receipt = await repository.ReloadReceiptAsync(id);
        if (receipt == null || receipt.DepotId != depotId) throw new KeyNotFoundException("Không tìm thấy phiếu trong kho.");
        if (receipt.Status != "DRAFT") throw new DepotConflictException("Chỉ sửa hàng thực nhận trên phiếu nháp. Phiếu đã gửi/ghi sổ phải giữ lịch sử.");
        if (Revision(receipt) != dto.ExpectedRevision) throw new DepotConflictException("Phiếu đã được cập nhật ở cửa sổ khác. Hãy đóng và mở lại phiếu để kiểm tra trước khi sửa.");
        var input = new CreateStockReceiptDto { OperationId = id, BulkAgreementId = receipt.BulkAgreementId,
            SourceType = receipt.SourceType, ReceivedAt = receipt.ReceivedAt, Reference = dto.Reference,
            PurchaseCost = dto.PurchaseCost, CostsConfirmed = dto.CostsConfirmed, Lines = dto.Lines, Expenses = dto.Expenses };
        ValidateReceipt(input, true);
        if (receipt.BulkAgreementId.HasValue &&
            dto.PurchaseCost != (await repository.BulkSourceAsync(receipt.BulkAgreementId.Value))?.Amount)
            throw new ArgumentException("Giá mua lô trúng phải giữ nguyên tổng tiền trong thỏa thuận.");
        var costs = dto.CostsConfirmed ? DepotCosting.Allocate(dto.PurchaseCost, dto.Expenses.Sum(x => x.Amount),
            dto.Lines.Select(x => new CostingLine(x.Quantity, x.ReferencePrice, x.DirectCost)).ToArray()) : null;
        var lines = dto.Lines.Select((x, i) => new DepotStockLine { ReceiptId = id, Kind = x.Kind, MaterialType = x.MaterialType,
            Name = x.Name.Trim(), Unit = x.Unit, Quantity = x.Quantity, ReferencePrice = x.ReferencePrice,
            DirectCost = x.DirectCost, ConditionNote = x.ConditionNote, Position = i, AllocatedCost = costs?[i] }).ToList();
        repository.ReplaceLines(receipt, lines);
        repository.ReplaceExpenses(receipt, dto.Expenses);
        receipt.Reference = dto.Reference.Trim(); receipt.PurchaseCost = dto.PurchaseCost; receipt.CostsConfirmed = dto.CostsConfirmed;
        await work.SaveAsync(); await tx.CommitAsync();
        return Map(receipt, true);
    }

    // Phiên bản nội dung phát hiện form cũ; không thêm cột hoặc sửa snapshot đã ghi sổ.
    private static string Revision(DepotStockReceipt receipt) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { receipt.Id, receipt.Status, receipt.Reference, receipt.SourceType, ReceivedAt = receipt.ReceivedAt.Ticks / 10,
            PurchaseCost = Canonical(receipt.PurchaseCost), receipt.CostsConfirmed, receipt.BulkAgreementId,
            Lines = receipt.Lines.OrderBy(x => x.Position).ThenBy(x => x.Id).Select(x => new { x.Id, x.Kind, x.MaterialType,
                x.Name, x.Unit, Quantity = Canonical(x.Quantity), ReferencePrice = Canonical(x.ReferencePrice),
                DirectCost = Canonical(x.DirectCost), AllocatedCost = x.AllocatedCost.HasValue ? Canonical(x.AllocatedCost.Value) : null, x.ConditionNote, x.Position }),
            Expenses = receipt.Expenses.OrderBy(x => x.Id).Select(x => new { x.Id, x.Category, x.Description, Amount = Canonical(x.Amount) }) }))));
    private static string Canonical(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
}
