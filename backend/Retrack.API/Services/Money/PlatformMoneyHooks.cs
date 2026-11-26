using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;

namespace Retrack.API.Services.Money;

/// <summary>
/// Các điểm móc nhỏ để luồng cũ (PickupService, DepotPaymentService, LiquidationService...) hỏi chế độ thu chi và ghi dòng sổ cái.
/// Dùng cùng DbContext/transaction với nơi gọi (đã khóa dòng nguồn). Ở LEGACY mọi hàm đều không làm gì.
/// Các dịch vụ nhận hooks qua tham số tùy chọn (mặc định null = LEGACY) nên không phá vỡ nơi khởi tạo hiện có.
/// </summary>
public sealed class PlatformMoneyHooks(AppDbContext db, MoneyConfigReader config, PayableLedgerService ledger)
{
    public async Task<bool> IsPlatformAsync(CancellationToken ct = default) => (await config.GetAsync(ct)).IsPlatformMode;
    public Task<bool> IsPlatformAsync(string flow, Guid source, CancellationToken ct = default) => config.UsesPlatformAsync(flow, source, ct);

    /// <summary>Đơn thu gom DONE: tạo dòng SALE cho Seller (gross = số khoản thu PAID, hoa hồng chụp theo tỷ lệ của Seller). Cần transaction hiện hành.</summary>
    public async Task OnPickupDoneAsync(PickupRequest request, DateTime doneAtUtc, CancellationToken ct = default)
    {
        if (!await IsPlatformAsync("PICKUP", request.Id, ct)) return;
        var c = await PaidAsync(MoneyFlows.Pickup, request.Id, ct);
        if (c == null) return; // đơn thanh toán theo luồng cũ trước khi bật PLATFORM
        await ledger.CreateSaleLineAsync(new(c.Id, c.PayeeId, MoneyFlows.Pickup, MoneySources.PickupRequest, request.Id, c.Amount, doneAtUtc), ct);
    }

    /// <summary>Chủ kho xác nhận thu gom đủ (owner_collected_at): MỘT dòng SALE cho Seller, gross = toàn bộ giá trúng, gắn khoản thu phần còn lại.</summary>
    public async Task OnLiquidationCollectedAsync(LiquidationAgreement agreement, DateTime collectedAtUtc, CancellationToken ct = default)
    {
        if (!await IsPlatformAsync("LIQUIDATION_BALANCE", agreement.Id, ct)) return;
        var c = await PaidAsync(MoneyFlows.LiquidationBalance, agreement.Id, ct);
        if (c == null) return;
        await ledger.CreateSaleLineAsync(new(c.Id, c.PayeeId, MoneyFlows.Liquidation, MoneySources.LiquidationAgreement, agreement.Id, agreement.Amount, collectedAtUtc), ct);
    }

    private Task<MoneyCollection?> PaidAsync(string flow, Guid sourceId, CancellationToken ct) =>
        db.MoneyCollections.AsNoTracking().SingleOrDefaultAsync(x => x.Flow == flow && x.SourceId == sourceId && x.Status == CollectionStates.Paid, ct);
}
