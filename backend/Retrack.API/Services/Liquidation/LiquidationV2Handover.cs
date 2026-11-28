using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Retrack.API.DTOs.Liquidation;
using Retrack.API.Models;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.Liquidation;

public sealed partial class LiquidationV2Service
{
    public const string ConfirmedQuantityPricing = "CONFIRMED_HANDOVER_QUANTITY";
    private static void RequireQuantityConfirmed(LiquidationAgreement a)
    {
        if (a.PricingBasis == ConfirmedQuantityPricing && a.HandoverStatus != "CONFIRMED")
            throw new DepotConflictException("Cần Seller xác nhận số cân/kiểm đếm bàn giao trước khi thanh toán.");
    }
    private static void RequireQuantityEditable(LiquidationAgreement a)
    {
        if (a.PricingBasis != ConfirmedQuantityPricing)
            throw new DepotConflictException("Phiên cũ giữ cách tính tiền theo lượng đã đăng.");
        if (a.PaymentReportedAt != null || a.PaymentConfirmedAt != null || a.SellerHandedOverAt != null ||
            a.OwnerCollectedAt != null || a.HandoverStatus == "CONFIRMED")
            throw new DepotConflictException("Số lượng và tiền mua đã chốt, không sửa lại ở bước này.");
    }
    private static LiquidationHandoverDto? HandoverDto(LiquidationHandover? h) => h == null ? null :
        new(h.Id, h.Revision, h.Status, h.Amount, h.Note, h.CreatedAt, h.DecisionNote, h.DecidedAt,
            h.Lines.OrderBy(x => x.AgreementItemId).Select(x => new LiquidationHandoverLineDto(x.AgreementItemId, x.Quantity, x.UnitPrice, x.Amount)).ToList());
    private static string HandoverHash(Guid agreement, ProposeLiquidationHandoverDto input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|",
            agreement, input.ExpectedRevision, input.Note?.Trim() ?? "",
            string.Join(";", input.Lines.OrderBy(x => x.AgreementItemId).Select(x =>
                x.AgreementItemId + ":" + x.Quantity.ToString("G29", CultureInfo.InvariantCulture)))))));

    public async Task<LiquidationV2AgreementDto> ProposeHandoverAsync(Guid owner, Guid depot, Guid id, ProposeLiquidationHandoverDto input)
    {
        if (input == null || input.OperationId == Guid.Empty || input.ExpectedRevision < 0 || input.Note?.Length > 1000 ||
            input.Lines == null || input.Lines.Count is < 1 or > LiquidationV2Rules.MaxItems || input.Lines.Any(x => x == null) ||
            input.Lines.Select(x => x.AgreementItemId).Distinct().Count() != input.Lines.Count)
            throw new ArgumentException("Cần mã thao tác, phiên bản và đủ các dòng cân/kiểm đếm hợp lệ.");
        var initial = await BuyerAgreementAsync(owner, depot, id);
        await using var tx = await work.BeginAsync();
        await depots.LockDepotAsync(initial.DepotId); await repository.LockOperationAsync(input.OperationId); await repository.LockLotAsync(initial.LotId);
        var a = await BuyerAgreementAsync(owner, depot, id); RequireActive(a);
        var hash = HandoverHash(id, input);
        var old = await repository.HandoverOperationAsync(input.OperationId);
        if (old != null)
        {
            if (old.AgreementId != id || old.ProposedBy != owner || old.RequestHash != hash)
                throw new DepotConflictException("Mã thao tác đã được dùng cho bảng bàn giao khác.");
            await tx.CommitAsync(); return await AgreementDtoAsync(a, false);
        }
        RequireQuantityEditable(a);
        if (a.HandoverRevision != input.ExpectedRevision || a.HandoverRevision == int.MaxValue)
            throw new DepotConflictException("Bảng cân đã thay đổi. Hãy tải lại trước khi gửi.");
        if (input.Lines.Count != a.Items.Count || input.Lines.Any(x => !a.Items.Any(i => i.Id == x.AgreementItemId)))
            throw new ArgumentException("Bảng cân phải khớp đủ hạng mục kho đã thắng, không thêm hoặc bỏ hạng mục.");
        var lines = new List<LiquidationHandoverLine>();
        foreach (var line in input.Lines)
        {
            var item = a.Items.Single(x => x.Id == line.AgreementItemId);
            if (line.Quantity <= 0 || line.Quantity > (item.Unit == "kg" ? LiquidationV2Rules.MaxKg : LiquidationV2Rules.MaxPieces) ||
                decimal.Round(line.Quantity, item.Unit == "kg" ? 3 : 0) != line.Quantity)
                throw new ArgumentException("Số cân phải dương, tối đa 3 số lẻ; số cái/bộ phải là số nguyên dương.");
            var amount = LiquidationV2Rules.Amount(line.Quantity, item.UnitPrice);
            if (amount <= 0 || amount > LiquidationV2Rules.MaxLineAmount)
                throw new ArgumentException("Thành tiền bàn giao không hợp lệ.");
            lines.Add(new() { AgreementItemId = item.Id, Quantity = line.Quantity, UnitPrice = item.UnitPrice, Amount = amount });
        }
        var total = lines.Sum(x => x.Amount);
        if (total > LiquidationV2Rules.MaxLineAmount) throw new ArgumentException("Tổng tiền bàn giao vượt giới hạn 1.000 tỷ.");
        if (a.HandoverRevision > 0)
        {
            var previous = await repository.HandoverAsync(id, a.HandoverRevision);
            if (previous?.Status == "PENDING") previous.Status = "SUPERSEDED";
        }
        a.HandoverRevision++; a.HandoverStatus = "PENDING";
        repository.AddHandover(new()
        {
            Id = input.OperationId,
            AgreementId = id,
            Revision = a.HandoverRevision,
            RequestHash = hash,
            Amount = total,
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
            ProposedBy = owner,
            CreatedAt = Now,
            Lines = lines
        });
        repository.AddNotice(SellerNotice(a.Lot.SellerId, a.LotId, "Kho gửi số cân/kiểm đếm bàn giao",
            $"Kiểm tra {lines.Count} hạng mục, tạm tính {total:N0} đ. Chỉ thanh toán sau khi bạn xác nhận bảng bàn giao."));
        await work.SaveAsync(); await tx.CommitAsync();
        return await AgreementDtoAsync(a, false);
    }

    public async Task<LiquidationV2AgreementDto> DecideHandoverAsync(Guid seller, Guid id, DecideLiquidationHandoverDto input)
    {
        if (input == null || input.Revision < 1 || input.Note?.Length > 1000 || !input.Accept && (input.Note?.Trim().Length ?? 0) < 5)
            throw new ArgumentException("Cần phiên bản bàn giao; nếu yêu cầu sửa, ghi lý do từ 5 đến 1000 ký tự.");
        var initial = await repository.FindAgreementAsync(id);
        if (initial == null || initial.Lot.SellerId != seller) throw new KeyNotFoundException("Không tìm thấy thỏa thuận.");
        await using var tx = await work.BeginAsync();
        await depots.LockDepotAsync(initial.DepotId); await repository.LockLotAsync(initial.LotId);
        var a = await repository.FindAgreementAsync(id) ?? throw new KeyNotFoundException("Không tìm thấy thỏa thuận.");
        if (a.Lot.SellerId != seller || !await repository.IsActiveSellerAsync(seller)) throw new DepotForbiddenException();
        RequireActive(a);
        if (a.PricingBasis != ConfirmedQuantityPricing || a.HandoverRevision != input.Revision)
            throw new DepotConflictException("Bảng bàn giao đã thay đổi hoặc phiên không áp dụng chốt cân. Hãy tải lại.");
        var h = await repository.HandoverAsync(id, input.Revision) ?? throw new DepotConflictException("Không tìm thấy bảng bàn giao hiện tại.");
        var status = input.Accept ? "CONFIRMED" : "REJECTED";
        var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        if (h.Status == status && h.DecisionNote == note) { await tx.CommitAsync(); return await AgreementDtoAsync(a, true); }
        if (h.Status != "PENDING" || a.PaymentReportedAt != null || a.PaymentConfirmedAt != null || a.OwnerCollectedAt != null)
            throw new DepotConflictException("Bảng bàn giao đã được xử lý, không thay đổi xác nhận cũ.");
        h.Status = status; h.DecisionNote = note; h.DecidedBy = seller; h.DecidedAt = Now; a.HandoverStatus = status;
        if (input.Accept) a.Amount = h.Amount;
        repository.AddNotice(Notification.ForDepot(a.BuyerId, a.DepotId, "LIQUIDATION",
            input.Accept ? "Seller đã xác nhận số cân/kiểm đếm" : "Seller yêu cầu sửa bảng bàn giao",
            input.Accept ? $"Đã chốt tiền mua {a.Amount:N0} đ. Kho có thể thanh toán trực tiếp cho Seller." : $"Lý do: {note}",
            targetType: "LIQUIDATION_V2", targetId: a.LotId));
        await work.SaveAsync(); await tx.CommitAsync(); return await AgreementDtoAsync(a, true);
    }
}
