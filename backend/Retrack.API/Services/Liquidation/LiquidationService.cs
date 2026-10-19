using Retrack.API.DTOs;
using Retrack.API.DTOs.Liquidation;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Liquidation;

public sealed class LiquidationService(ILiquidationRepository repository, IDepotService scope,
    IDepotUnitOfWork work, IDepotBatchRepository depots, TimeProvider clock) : ILiquidationService
{
    // PostgreSQL lưu microsecond; phản hồi đầu và retry dùng cùng độ chính xác.
    private DateTime Now { get { var value = clock.GetUtcNow().UtcDateTime; return new(value.Ticks / 10 * 10, DateTimeKind.Utc); } }
    private async Task RequireViewerAsync(Guid userId, Guid? depotId)
    {
        if (!depotId.HasValue) throw new DepotForbiddenException();
        await scope.RequireOwnerAsync(userId, depotId.Value);
    }
    public async Task<PagedResult<LiquidationSummaryDto>> ListAsync(Guid userId, Guid? depotId, LiquidationQuery query)
    {
        await RequireViewerAsync(userId, depotId);
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || query.Search?.Length > 255 ||
            query.Status is not (null or "" or "SURVEY" or "BIDDING" or "UPCOMING" or "CLOSING" or "AWARDED" or "NO_SALE" or "CANCELLED"))
            throw new ArgumentException("Bộ lọc hoặc phân trang không hợp lệ.");
        var now = Now;
        var page = await repository.ListAsync(userId, depotId!.Value, query, now);
        return new() { Page = page.Page, PageSize = page.PageSize, TotalCount = page.TotalCount, Items = page.Items.Select(x => Summary(x, now)).ToList() };
    }
    public async Task<LiquidationDetailDto> DetailAsync(Guid userId, Guid? depotId, Guid lotId)
    {
        await RequireViewerAsync(userId, depotId);
        var lot = await repository.FindAsync(lotId) ?? throw new KeyNotFoundException("Không tìm thấy lô thanh lý.");
        var participation = await repository.ParticipationAsync(lotId, userId);
        var highest = lot.HighestBidId.HasValue ? await repository.FindBidAsync(lot.HighestBidId.Value) : null;
        var agreement = await repository.LotAgreementAsync(lotId);
        var canReadAddress = participation?.DepotId == depotId;
        var canSeeAgreement = agreement != null && agreement.BuyerId == userId && agreement.DepotId == depotId;
        var now = Now;
        return new(Summary(lot, now), lot.Description, lot.ScopeDetails, lot.CollectionTerms,
            canReadAddress ? lot.Address : null, canReadAddress ? lot.ContactPhone : null,
            participation?.DepotId, participation?.SurveyDeclaredAt != null, highest?.OwnerId == userId,
            depotId.HasValue && participation?.DepotId == depotId && participation.SurveyDeclaredAt != null &&
                lot.Status == "PUBLISHED" && now >= lot.BiddingStart && now < lot.BiddingEnd && await repository.IsActiveSellerAsync(lot.SellerId),
            lot.CancelReason, canSeeAgreement ? agreement!.Id : null);
    }
    public async Task ParticipateAsync(Guid ownerId, Guid depotId, Guid lotId, bool consent, bool declareSurvey)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (!consent) throw new ArgumentException("Cần xác nhận điều kiện của phiên.");
        await using var tx = await work.BeginAsync();
        await depots.LockDepotAsync(depotId);
        var lot = await repository.LockAsync(lotId) ?? throw new KeyNotFoundException("Không tìm thấy lô.");
        var now = Now;
        if (lot.SellerId == ownerId || lot.Status != "PUBLISHED" || !await repository.IsActiveSellerAsync(lot.SellerId)) throw new DepotConflictException("Phiên không còn cho tham gia.");
        var participation = await repository.ParticipationAsync(lotId, ownerId);
        if (participation != null && participation.DepotId != depotId) throw new DepotConflictException("Bạn đã tham gia bằng kho khác. Một chủ kho chỉ dùng một kho trong mỗi phiên.");
        if (participation == null || declareSurvey && participation.SurveyDeclaredAt == null) await scope.RequireTradingAsync(ownerId, depotId);
        if (declareSurvey)
        {
            if (participation == null || now < lot.SurveyStart || now >= lot.BiddingEnd)
                throw new DepotConflictException("Cần đăng ký trước và xác nhận hiện trạng từ lúc bắt đầu khảo sát đến trước hết phiên.");
            participation.SurveyDeclaredAt ??= now;
        }
        else if (participation == null)
        {
            if (now >= lot.SurveyEnd) throw new DepotConflictException("Đã hết thời gian đăng ký khảo sát.");
            repository.AddParticipation(new() { LotId = lotId, DepotId = depotId, OwnerId = ownerId, RegisteredAt = now });
        }
        await work.SaveAsync(); await tx.CommitAsync();
    }
    public async Task<MyLiquidationBidDto> BidAsync(Guid ownerId, Guid depotId, Guid lotId, LiquidationBidInput dto)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (dto.OperationId == Guid.Empty) throw new ArgumentException("Cần mã thao tác trả giá.");
        LiquidationRules.CheckMoney(dto.Amount);
        await using var tx = await work.BeginAsync();
        await depots.LockDepotAsync(depotId);
        await repository.LockCreateAsync(dto.OperationId);
        var lot = await repository.LockAsync(lotId) ?? throw new KeyNotFoundException("Không tìm thấy lô.");
        var old = await repository.FindBidAsync(dto.OperationId);
        if (old != null)
        {
            if (old.OwnerId != ownerId || old.DepotId != depotId || old.LotId != lotId || old.Amount != dto.Amount)
                throw new DepotConflictException("Mã thao tác đã dùng cho lượt trả giá khác.");
            return new(old.Id, old.Amount, old.CreatedAt);
        }
        await scope.RequireTradingAsync(ownerId, depotId);
        var now = Now;
        var participant = await repository.ParticipationAsync(lotId, ownerId);
        if (participant?.DepotId != depotId || participant.SurveyDeclaredAt == null || lot.SellerId == ownerId)
            throw new DepotForbiddenException("Kho cần đăng ký và tự xác nhận đã khảo sát/hiểu hiện trạng trước khi trả giá.");
        if (lot.Status != "PUBLISHED" || now < lot.BiddingStart || now >= lot.BiddingEnd || !await repository.IsActiveSellerAsync(lot.SellerId))
            throw new DepotConflictException("Phiên chưa mở, đã hết hạn hoặc không còn hoạt động.");
        LiquidationRules.CheckBid(lot.StartingPrice, lot.HighestPrice, lot.BidIncrement, dto.Amount);
        var bid = new LiquidationBid { Id = dto.OperationId, LotId = lotId, DepotId = depotId, OwnerId = ownerId, Amount = dto.Amount, CreatedAt = now };
        repository.AddBid(bid); lot.HighestPrice = dto.Amount; lot.HighestBidId = bid.Id; lot.BidCount++;
        lot.BiddingEnd = LiquidationRules.Extend(now, lot.BiddingEnd, lot.OriginalEnd.AddMinutes(10));
        await work.SaveAsync(); await tx.CommitAsync(); return new(bid.Id, bid.Amount, bid.CreatedAt);
    }
    public async Task<PagedResult<MyLiquidationBidDto>> MyBidsAsync(Guid ownerId, Guid depotId, Guid lotId, int page)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (page < 1) throw new ArgumentException("Trang không hợp lệ.");
        return await repository.MyBidsAsync(lotId, ownerId, depotId, page);
    }
    public Task<List<Guid>> ExpiredAsync() => repository.ExpiredAsync(Now);
    public async Task FinalizeAsync(Guid lotId)
    {
        await using var tx = await work.BeginAsync();
        var lot = await repository.LockAsync(lotId);
        if (lot == null || lot.Status != "PUBLISHED" || Now < lot.BiddingEnd) return;
        if (lot.HighestBidId == null) lot.Status = "NO_SALE";
        else
        {
            var winning = await repository.FindBidAsync(lot.HighestBidId.Value) ?? throw new InvalidOperationException("Thiếu lượt giá thắng; cần kiểm tra dữ liệu phiên.");
            if (winning.LotId != lot.Id || winning.Amount != lot.HighestPrice) throw new InvalidOperationException("Giá thắng không khớp phiên.");
            lot.Status = "AWARDED";
            repository.AddAgreement(new() { LotId = lot.Id, WinningBidId = winning.Id, DepotId = winning.DepotId, BuyerId = winning.OwnerId, Amount = winning.Amount, CreatedAt = Now });
        }
        await work.SaveAsync(); await tx.CommitAsync();
    }
    private async Task<LiquidationAgreement> RequireAgreementAsync(Guid userId, Guid? depotId, Guid id)
    {
        await RequireViewerAsync(userId, depotId);
        var agreement = await repository.FindAgreementAsync(id) ?? throw new KeyNotFoundException("Không tìm thấy thỏa thuận.");
        if (agreement.BuyerId != userId || agreement.DepotId != depotId)
            throw new DepotForbiddenException();
        return agreement;
    }
    public async Task<LiquidationAgreementDto> AgreementAsync(Guid userId, Guid? depotId, Guid agreementId)
    {
        var x = await RequireAgreementAsync(userId, depotId, agreementId);
        var receipt = await repository.AgreementReceiptAsync(x.Id);
        return new(x.Id, x.LotId, x.Lot.Title, x.DepotId, x.Amount, x.Lot.ScopeDetails, x.Lot.CollectionTerms,
            x.Lot.Address, x.Lot.ContactPhone, x.Lot.CollectionDeadline, x.PaymentReference, x.PaymentReportedAt,
            x.PaymentConfirmedAt, x.OwnerCollectedAt, x.SellerHandedOverAt, receipt?.Id, receipt?.Status);
    }
    public async Task AgreementActionAsync(Guid userId, Guid? depotId, Guid agreementId, string action, string? reference = null)
    {
        var initial = await RequireAgreementAsync(userId, depotId, agreementId);
        await using var tx = await work.BeginAsync();
        // Cùng thứ tự depot → phiên với thao tác lập/duyệt phiếu nhập liên kết.
        await depots.LockDepotAsync(initial.DepotId);
        await repository.LockAsync(initial.LotId);
        var agreement = await RequireAgreementAsync(userId, depotId, agreementId);
        var now = Now;
        if (action == "REPORT_PAYMENT")
        {
            if (string.IsNullOrWhiteSpace(reference) || reference.Length > 255) throw new ArgumentException("Cần tham chiếu chuyển tiền tối đa 255 ký tự.");
            if (agreement.PaymentReportedAt != null && agreement.PaymentReference != reference.Trim()) throw new DepotConflictException("Tham chiếu đã ghi, không sửa ngầm bằng retry.");
            agreement.PaymentReference = reference.Trim(); agreement.PaymentReportedAt ??= now;
        }
        else if (action == "CONFIRM_COLLECTED")
        {
            if (agreement.PaymentConfirmedAt == null) throw new DepotConflictException("Cần Seller xác nhận đã nhận đủ tiền trước giao nhận.");
            agreement.OwnerCollectedAt ??= now;
        }
        else throw new DepotForbiddenException();
        await work.SaveAsync(); await tx.CommitAsync();
    }
    private static LiquidationSummaryDto Summary(LiquidationLot x, DateTime now) => new(x.Id, x.Title, x.PublicArea,
        x.StartingPrice, x.BidIncrement, x.HighestPrice, x.BidCount, LiquidationRules.Phase(x.Status, now, x.SurveyStart, x.SurveyEnd, x.BiddingStart, x.BiddingEnd),
        x.Status, x.SurveyStart, x.SurveyEnd, x.BiddingStart, x.BiddingEnd, x.OriginalEnd.AddMinutes(10), x.CollectionDeadline, x.ImageUrls, now);
}
