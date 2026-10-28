using System.Security.Cryptography;
using System.Text;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Seller;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Liquidation;

namespace Retrack.API.Services.Seller;

public sealed class SellerLiquidationService(ISellerLiquidationRepository repository, IDepotUnitOfWork work,
    IDepotBatchRepository depots, TimeProvider clock) : ISellerLiquidationService
{
    private const int MaxOpenLots = 20;
    private static readonly TimeSpan MinBiddingWindow = TimeSpan.FromMinutes(10), MaxBiddingWindow = TimeSpan.FromDays(30);
    // PostgreSQL lưu microsecond; phản hồi đầu và retry dùng cùng độ chính xác.
    private DateTime Now { get { var value = clock.GetUtcNow().UtcDateTime; return new(value.Ticks / 10 * 10, DateTimeKind.Utc); } }

    private static DateTime ToUtc(DateTime value, string name) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => throw new ArgumentException($"{name} cần kèm múi giờ (UTC).")
    };
    private static string Hash(Guid seller, CreateLiquidationLotDto x, DateTime[] times) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f',
        [seller, x.Title, x.Description, x.ScopeDetails, x.CollectionTerms, x.PublicArea, x.Address, x.ContactPhone,
         string.Join('\n', x.ImageUrls), .. times.Select(t => t.Ticks), x.StartingPrice, x.BidIncrement, x.DepositPercent])))).ToLowerInvariant();

    public async Task<SellerLiquidationDetailDto> CreateAsync(Guid sellerId, CreateLiquidationLotDto dto)
    {
        if (dto.OperationId == Guid.Empty) throw new ArgumentException("Cần mã thao tác tạo lô.");
        var title = dto.Title.Trim();
        // Độ dài tối thiểu tính sau khi cắt khoảng trắng; DataAnnotations ở controller chưa bắt được chuỗi toàn dấu cách xen kẽ.
        if (title.Length < 5 || dto.Description.Trim().Length < 10 || dto.ScopeDetails.Trim().Length < 10 || dto.CollectionTerms.Trim().Length < 10 ||
            dto.PublicArea.Trim().Length < 2 || dto.Address.Trim().Length < 5 || dto.ContactPhone.Trim().Length < 8)
            throw new ArgumentException("Thông tin lô quá ngắn hoặc để trống.");
        var urls = dto.ImageUrls.Select(x => x.Trim()).ToList();
        if (urls.Any(x => x.Length > 1000 || !Uri.TryCreate(x, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")))
            throw new ArgumentException("Ảnh phải là đường dẫn http(s) hợp lệ, tối đa 10 ảnh.");
        LiquidationRules.CheckMoney(dto.StartingPrice); LiquidationRules.CheckMoney(dto.BidIncrement);
        LiquidationRules.CheckDepositPercent(dto.DepositPercent);
        var times = new[] { ToUtc(dto.SurveyStart, "Khảo sát bắt đầu"), ToUtc(dto.SurveyEnd, "Khảo sát kết thúc"),
            ToUtc(dto.BiddingStart, "Mở trả giá"), ToUtc(dto.BiddingEnd, "Đóng trả giá"), ToUtc(dto.CollectionDeadline, "Hạn thu gom") };
        LiquidationRules.CheckSchedule(times[0], times[1], times[2], times[3], times[4]);
        var now = Now;
        if (times[0] < now.AddMinutes(-5)) throw new ArgumentException("Lịch khảo sát không được bắt đầu trong quá khứ.");
        if (times[3] - times[2] < MinBiddingWindow || times[3] - times[2] > MaxBiddingWindow)
            throw new ArgumentException("Thời gian trả giá phải từ 10 phút đến 30 ngày.");
        var hash = Hash(sellerId, dto, times);

        await using var tx = await work.BeginAsync();
        await repository.LockCreateAsync(dto.OperationId);
        var existing = await repository.FindLotAsync(dto.OperationId);
        if (existing != null)
        {
            if (existing.SellerId != sellerId || existing.RequestHash != hash) throw new DepotConflictException("Mã thao tác đã dùng cho lô khác.");
            return await BuildDetailAsync(existing, now);
        }
        if (!await repository.IsActiveSellerAsync(sellerId)) throw new DepotForbiddenException("Tài khoản Seller không còn hoạt động.");
        if (await repository.CountOpenLotsAsync(sellerId) >= MaxOpenLots) throw new DepotConflictException($"Tối đa {MaxOpenLots} lô đang mở cùng lúc.");
        var lot = new LiquidationLot
        {
            Id = dto.OperationId, SellerId = sellerId, Title = title, Description = dto.Description.Trim(), ScopeDetails = dto.ScopeDetails.Trim(),
            CollectionTerms = dto.CollectionTerms.Trim(), PublicArea = dto.PublicArea.Trim(), Address = dto.Address.Trim(), ContactPhone = dto.ContactPhone.Trim(),
            ImageUrls = [.. urls], SurveyStart = times[0], SurveyEnd = times[1], BiddingStart = times[2], OriginalEnd = times[3], BiddingEnd = times[3],
            CollectionDeadline = times[4], StartingPrice = dto.StartingPrice, BidIncrement = dto.BidIncrement, DepositPercent = dto.DepositPercent, Status = "PUBLISHED", RequestHash = hash, CreatedAt = now
        };
        repository.AddLot(lot);
        await work.SaveAsync(); await tx.CommitAsync();
        return await BuildDetailAsync(lot, now);
    }

    public async Task<PagedResult<SellerLiquidationSummaryDto>> ListAsync(Guid sellerId, SellerLiquidationQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || query.Search?.Length > 255 ||
            query.Status is not (null or "" or "SURVEY" or "BIDDING" or "UPCOMING" or "CLOSING" or "AWARDED" or "NO_SALE" or "CANCELLED"))
            throw new ArgumentException("Bộ lọc hoặc phân trang không hợp lệ.");
        var now = Now;
        var page = await repository.ListAsync(sellerId, query, now);
        return new() { Page = page.Page, PageSize = page.PageSize, TotalCount = page.TotalCount, Items = page.Items.Select(x => Summary(x.Lot, x.ParticipantCount, now)).ToList() };
    }

    private async Task<LiquidationLot> OwnedLotAsync(Guid sellerId, Guid lotId)
    {
        var lot = await repository.FindLotAsync(lotId);
        // Không phân biệt "không tồn tại" và "của Seller khác" để không lộ ID.
        if (lot == null || lot.SellerId != sellerId) throw new KeyNotFoundException("Không tìm thấy lô thanh lý.");
        return lot;
    }

    public async Task<SellerLiquidationDetailDto> DetailAsync(Guid sellerId, Guid lotId) =>
        await BuildDetailAsync(await OwnedLotAsync(sellerId, lotId), Now);

    public async Task<PagedResult<SellerLiquidationBidDto>> BidsAsync(Guid sellerId, Guid lotId, int page)
    {
        if (page < 1) throw new ArgumentException("Trang không hợp lệ.");
        var lot = await OwnedLotAsync(sellerId, lotId);
        return await repository.BidsAsync(lot.Id, lot.HighestBidId, page);
    }

    public async Task CancelAsync(Guid sellerId, Guid lotId, string reason)
    {
        reason = reason?.Trim() ?? "";
        if (reason.Length is < 5 or > 1000) throw new ArgumentException("Cần lý do hủy từ 5 đến 1000 ký tự.");
        await OwnedLotAsync(sellerId, lotId);
        await using var tx = await work.BeginAsync();
        // Chỉ khóa dòng lô: luồng trả giá cũng khóa lô nên bid đồng thời sẽ thấy trạng thái đã hủy.
        var lot = await repository.LockLotAsync(lotId) ?? throw new KeyNotFoundException("Không tìm thấy lô thanh lý.");
        if (lot.SellerId != sellerId) throw new KeyNotFoundException("Không tìm thấy lô thanh lý.");
        if (lot.Status == "CANCELLED" && lot.CancelReason == reason) return;
        var now = Now;
        if (lot.Status != "PUBLISHED" || now >= lot.BiddingEnd) throw new DepotConflictException("Phiên đã kết thúc hoặc không còn ở trạng thái mở.");
        if (lot.BidCount > 0) throw new DepotConflictException("Phiên đã có lượt trả giá nên không thể tự hủy; cần Admin xử lý để không bỏ rơi người trả giá.");
        lot.Status = "CANCELLED"; lot.CancelReason = reason;
        foreach (var participant in await repository.ParticipantsAsync(lotId))
            repository.AddNotice(Notification.ForDepot(participant.OwnerId, participant.DepotId, "LIQUIDATION", "Lô thanh lý đã bị hủy",
                $"Seller đã hủy lô {lot.Title}. Lý do: {reason}", targetType: "LIQUIDATION", targetId: lot.Id));
        await work.SaveAsync(); await tx.CommitAsync();
    }

    private async Task<LiquidationAgreement> OwnedAgreementAsync(Guid sellerId, Guid agreementId)
    {
        var agreement = await repository.FindAgreementAsync(agreementId);
        if (agreement == null || agreement.Lot.SellerId != sellerId) throw new KeyNotFoundException("Không tìm thấy thỏa thuận.");
        return agreement;
    }

    private async Task<SellerLiquidationAgreementDto> AgreementStepAsync(Guid sellerId, Guid agreementId, string step)
    {
        var initial = await OwnedAgreementAsync(sellerId, agreementId);
        await using var tx = await work.BeginAsync();
        // Cùng thứ tự khóa depot → lô với thao tác của Chủ kho để tránh deadlock/race.
        await depots.LockDepotAsync(initial.DepotId);
        await repository.LockLotAsync(initial.LotId);
        var agreement = await OwnedAgreementAsync(sellerId, agreementId);
        if (!await repository.IsActiveSellerAsync(sellerId)) throw new DepotForbiddenException("Tài khoản Seller không còn hoạt động.");
        var now = Now;
        if (step == "SIGN")
        {
            if (agreement.DepositAmount <= 0) throw new DepotConflictException("Thỏa thuận này không có hợp đồng đặt cọc.");
            if (agreement.SellerSignedAt == null)
            {
                agreement.SellerSignedAt = now;
                repository.AddNotice(Notification.ForDepot(agreement.BuyerId, agreement.DepotId, "LIQUIDATION", "Seller đã ký hợp đồng đặt cọc",
                    $"Seller đã ký hợp đồng đặt cọc lô {agreement.Lot.Title}. Hãy ký nếu bạn chưa ký, rồi chuyển cọc ngoài hệ thống.", targetType: "LIQUIDATION", targetId: agreement.LotId));
            }
        }
        else if (step == "DEPOSIT")
        {
            if (agreement.DepositReportedAt == null) throw new DepotConflictException("Chủ kho chưa báo chuyển cọc nên chưa thể xác nhận đã nhận.");
            if (agreement.DepositConfirmedAt == null)
            {
                agreement.DepositConfirmedAt = now;
                repository.AddNotice(Notification.ForDepot(agreement.BuyerId, agreement.DepotId, "LIQUIDATION", "Seller đã xác nhận nhận tiền cọc",
                    $"Seller xác nhận đã nhận cọc lô {agreement.Lot.Title}. Bạn có thể chuyển phần còn lại.", targetType: "LIQUIDATION", targetId: agreement.LotId));
            }
        }
        else if (step == "PAYMENT")
        {
            if (agreement.PaymentReportedAt == null) throw new DepotConflictException("Chủ kho chưa báo chuyển tiền nên chưa thể xác nhận đã nhận.");
            if (agreement.DepositAmount > 0 && agreement.DepositConfirmedAt == null) throw new DepotConflictException("Cần xác nhận nhận tiền cọc trước.");
            if (agreement.PaymentConfirmedAt == null)
            {
                agreement.PaymentConfirmedAt = now;
                repository.AddNotice(Notification.ForDepot(agreement.BuyerId, agreement.DepotId, "LIQUIDATION", "Seller đã xác nhận nhận tiền",
                    $"Seller xác nhận đã nhận đủ tiền lô {agreement.Lot.Title}. Hai bên có thể tiến hành giao nhận.", targetType: "LIQUIDATION", targetId: agreement.LotId));
            }
        }
        else
        {
            if (agreement.PaymentConfirmedAt == null) throw new DepotConflictException("Cần xác nhận đã nhận đủ tiền trước khi ghi nhận bàn giao.");
            if (agreement.SellerHandedOverAt == null)
            {
                agreement.SellerHandedOverAt = now;
                repository.AddNotice(Notification.ForDepot(agreement.BuyerId, agreement.DepotId, "LIQUIDATION", "Seller đã xác nhận bàn giao",
                    $"Seller xác nhận đã bàn giao lô {agreement.Lot.Title}. Chủ kho kiểm tra và xác nhận thu gom đủ.", targetType: "LIQUIDATION", targetId: agreement.LotId));
            }
        }
        await work.SaveAsync(); await tx.CommitAsync();
        return await AgreementDtoAsync(agreement);
    }
    public Task<SellerLiquidationAgreementDto> ConfirmPaymentAsync(Guid sellerId, Guid agreementId) => AgreementStepAsync(sellerId, agreementId, "PAYMENT");
    public Task<SellerLiquidationAgreementDto> ConfirmHandoverAsync(Guid sellerId, Guid agreementId) => AgreementStepAsync(sellerId, agreementId, "HANDOVER");
    public Task<SellerLiquidationAgreementDto> SignAsync(Guid sellerId, Guid agreementId) => AgreementStepAsync(sellerId, agreementId, "SIGN");
    public Task<SellerLiquidationAgreementDto> ConfirmDepositAsync(Guid sellerId, Guid agreementId) => AgreementStepAsync(sellerId, agreementId, "DEPOSIT");

    private async Task<SellerLiquidationAgreementDto> AgreementDtoAsync(LiquidationAgreement x) => new(x.Id, x.LotId, x.Lot.Title,
        await repository.DepotNameAsync(x.DepotId), x.Amount, Stage(x), x.PaymentReference, x.PaymentReportedAt, x.PaymentConfirmedAt,
        x.OwnerCollectedAt, x.SellerHandedOverAt, x.Lot.CollectionDeadline, x.Lot.DepositPercent, x.DepositAmount, x.Amount - x.DepositAmount,
        x.SellerSignedAt, x.BuyerSignedAt, x.DepositReference, x.DepositReportedAt, x.DepositConfirmedAt);

    // Giai đoạn thỏa thuận theo góc nhìn Seller; tiền đi ngoài hệ thống, nền tảng chỉ ghi nhận xác nhận.
    private static string Stage(LiquidationAgreement x) =>
        x.DepositAmount > 0 && (x.SellerSignedAt == null || x.BuyerSignedAt == null) ? "WAITING_SIGNATURES" :
        x.DepositAmount > 0 && x.DepositReportedAt == null ? "WAITING_DEPOSIT" :
        x.DepositAmount > 0 && x.DepositConfirmedAt == null ? "WAITING_DEPOSIT_CONFIRM" :
        x.PaymentConfirmedAt == null ? (x.PaymentReportedAt == null ? "WAITING_BUYER_PAYMENT" : "WAITING_SELLER_CONFIRM") :
        x.SellerHandedOverAt == null ? "WAITING_HANDOVER" : x.OwnerCollectedAt == null ? "WAITING_OWNER_COLLECT" : "COMPLETED";

    private async Task<SellerLiquidationDetailDto> BuildDetailAsync(LiquidationLot lot, DateTime now)
    {
        var agreement = lot.Status == "AWARDED" ? await repository.LotAgreementAsync(lot.Id) : null;
        SellerLiquidationAgreementDto? agreementDto = null;
        if (agreement != null)
        {
            var full = await repository.FindAgreementAsync(agreement.Id);
            if (full != null) agreementDto = await AgreementDtoAsync(full);
        }
        return new(Summary(lot, await repository.ParticipantCountAsync(lot.Id), now), lot.Description, lot.ScopeDetails, lot.CollectionTerms,
            lot.Address, lot.ContactPhone, lot.CancelReason, lot.Status == "PUBLISHED" && lot.BidCount == 0 && now < lot.BiddingEnd, agreementDto);
    }

    private static SellerLiquidationSummaryDto Summary(LiquidationLot x, int participants, DateTime now) => new(x.Id, x.Title, x.PublicArea,
        x.StartingPrice, x.BidIncrement, x.HighestPrice, x.BidCount, participants,
        LiquidationRules.Phase(x.Status, now, x.SurveyStart, x.SurveyEnd, x.BiddingStart, x.BiddingEnd), x.Status,
        x.SurveyStart, x.SurveyEnd, x.BiddingStart, x.BiddingEnd, x.OriginalEnd.AddMinutes(10), x.CollectionDeadline, x.ImageUrls, now, x.DepositPercent);
}
