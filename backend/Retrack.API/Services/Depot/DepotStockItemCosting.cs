using Retrack.API.DTOs.Depot;
using Retrack.API.Helpers;
using Retrack.API.Services.Liquidation;

namespace Retrack.API.Services.Depot;

public sealed partial class DepotStockService
{
    private async Task<decimal[]?> AllocateReceiptCostsAsync(Guid depotId, Guid? agreementId, decimal purchase,
        string? sourceRevision, IReadOnlyList<StockLineInput> lines, decimal common, bool confirmed)
    {
        var linked = lines.Any(x => x.AgreementItemId.HasValue);
        if (!agreementId.HasValue)
        {
            if (linked) throw new ArgumentException("Hạng mục nguồn chỉ được dùng trong phiếu liên kết lô đã mua.");
            return confirmed ? DepotCosting.Allocate(purchase, common, lines.Select(x => new CostingLine(x.Quantity, x.ReferencePrice, x.DirectCost)).ToArray()) : null;
        }
        var source = await repository.LockBulkSourceAsync(agreementId.Value);
        if (source == null || source.DepotId != depotId) throw new DepotForbiddenException();
        if (source.PricingBasis == LiquidationV2Service.ConfirmedQuantityPricing && source.HandoverStatus != "CONFIRMED")
            throw new DepotConflictException("Cần chốt cân/kiểm đếm hai phía trước khi nhập kho.");
        if (!linked)
        {
            if (source.PricingBasis == LiquidationV2Service.ConfirmedQuantityPricing)
                throw new ArgumentException("Phiếu phải giữ liên kết tới từng hạng mục đã mua.");
            return confirmed ? DepotCosting.Allocate(purchase, common, lines.Select(x => new CostingLine(x.Quantity, x.ReferencePrice, x.DirectCost)).ToArray()) : null;
        }
        if (source.FormatVersion != 2 || lines.Any(x => !x.AgreementItemId.HasValue) || purchase != source.Amount ||
            source.PaymentConfirmedAt == null || source.SellerHandedOverAt == null || source.Resolution == "CANCELLED" ||
            source.AbandonReportedAt != null && source.Resolution == null)
            throw new DepotConflictException("Nguồn mua chưa hợp lệ hoặc tiền mua không khớp số đã chốt.");
        if (sourceRevision != LiquidationSourceRevision.For(source))
            throw new DepotConflictException("Thông tin bàn giao đã thay đổi. Hãy tải lại nguồn trước khi nhập kho.");
        var items = await repository.BulkCostItemsAsync(agreementId.Value);
        if (items.Any(x => !x.Amount.HasValue || x.Amount <= 0) || items.Sum(x => x.Amount ?? 0) != purchase ||
            items.Count == 0 || items.Any(x => !lines.Any(l => l.AgreementItemId == x.Id)) || lines.Any(l => !items.Any(x => x.Id == l.AgreementItemId)))
            throw new ArgumentException("Dòng nhập phải khớp đủ hạng mục đã mua, không thêm nguồn khác hoặc bỏ tiền mua.");
        return confirmed ? DepotCosting.AllocateItems(common, items.Select(x => new CostingItem(x.Id, x.Amount!.Value)).ToArray(),
            lines.Select(x => new ItemCostingLine(x.AgreementItemId!.Value, new(x.Quantity, x.ReferencePrice, x.DirectCost))).ToArray()) : null;
    }
}
