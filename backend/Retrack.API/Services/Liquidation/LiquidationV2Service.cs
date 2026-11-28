using System.Security.Cryptography;
using System.Text;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Liquidation;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Liquidation;

/// <summary>
/// Phiên thanh lý nhiều hạng mục (V2). Thứ tự khóa: kho → mã thao tác → phiên (giống V1 và luồng nhập kho).
/// Đóng phiên chỉ khóa phiên. Tiền mua = đơn giá trúng × số lượng đã đăng; RETRACK không thu hộ, không cọc.
/// </summary>
public sealed partial class LiquidationV2Service(ILiquidationV2Repository repository, IDepotService scope, IDepotUnitOfWork work,
    IDepotBatchRepository depots, TimeProvider clock) : ILiquidationV2Service, ISellerLiquidationV2Service, IAdminLiquidationService
{
    private const int MaxOpenLots = 20;
    private static readonly TimeSpan MinBiddingWindow = TimeSpan.FromMinutes(1), MaxBiddingWindow = TimeSpan.FromDays(30);
    // PostgreSQL lưu microsecond; phản hồi đầu và retry dùng cùng độ chính xác.
    private DateTime Now { get { var value = clock.GetUtcNow().UtcDateTime; return new(value.Ticks / 10 * 10, DateTimeKind.Utc); } }

    // ───────────────────────── Seller ─────────────────────────
    private static DateTime ToUtc(DateTime value, string name) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => throw new ArgumentException($"{name} cần kèm múi giờ (UTC).")
    };

    private static string Hash(Guid seller, CreateLiquidationV2Dto x, DateTime[] times) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f',
        [seller, x.Title, x.Description, x.CollectionTerms, x.PublicArea, x.Address, x.ContactPhone, string.Join('\n', x.ImageUrls),
         .. times.Select(t => t.Ticks), x.CollectionWindowHours,
         .. x.Items.Select(i => string.Join('\u001e', i.MaterialCode, i.Name, i.Description, i.Unit, i.Quantity, i.StartingUnitPrice, i.BidIncrement))])))).ToLowerInvariant();

    public async Task<SellerLiquidationV2DetailDto> CreateAsync(Guid sellerId, CreateLiquidationV2Dto dto)
    {
        if (dto.OperationId == Guid.Empty) throw new ArgumentException("Cần mã thao tác tạo phiên.");
        var title = dto.Title?.Trim() ?? "";
        if (title.Length < 5 || (dto.Description?.Trim().Length ?? 0) < 10 || (dto.CollectionTerms?.Trim().Length ?? 0) < 10 ||
            (dto.PublicArea?.Trim().Length ?? 0) < 2 || (dto.Address?.Trim().Length ?? 0) < 5 || (dto.ContactPhone?.Trim().Length ?? 0) < 8)
            throw new ArgumentException("Thông tin phiên quá ngắn hoặc để trống.");
        if (dto.Items == null || dto.Items.Count is < 1 or > LiquidationV2Rules.MaxItems || dto.Items.Any(x => x == null))
            throw new ArgumentException($"Phiên cần từ 1 đến {LiquidationV2Rules.MaxItems} hạng mục.");
        foreach (var item in dto.Items)
        {
            item.Name = item.Name?.Trim() ?? ""; item.Description = item.Description?.Trim() ?? ""; item.Unit = item.Unit?.Trim() ?? "";
            // Mã vật liệu chỉ cho hạng mục kg, theo đúng danh mục dùng chung để kho nhập đúng loại khi phân loại.
            item.MaterialCode = string.IsNullOrWhiteSpace(item.MaterialCode) || item.Unit != "kg" ? null : Retrack.API.Services.Shared.MaterialCatalog.RequireCode(item.MaterialCode);
            LiquidationV2Rules.CheckItem(item.Name, item.Description, item.Unit, item.Quantity, item.StartingUnitPrice, item.BidIncrement);
        }
        if (dto.Items.Select(x => x.Name.ToLowerInvariant()).Distinct().Count() != dto.Items.Count)
            throw new ArgumentException("Tên các hạng mục trong một phiên không được trùng nhau.");
        var urls = (dto.ImageUrls ?? []).Select(x => x.Trim()).ToList();
        if (urls.Count > 10 || urls.Any(x => x.Length > 1000 || !Uri.TryCreate(x, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")))
            throw new ArgumentException("Ảnh phải là đường dẫn http(s) hợp lệ, tối đa 10 ảnh.");
        if (dto.CollectionWindowHours is < LiquidationV2Rules.MinCollectionWindowHours or > LiquidationV2Rules.MaxCollectionWindowHours)
            throw new ArgumentException("Thời hạn thu gom sau khi đóng phiên phải từ 24 giờ đến 60 ngày.");
        var times = new[] { ToUtc(dto.SurveyStart, "Khảo sát bắt đầu"), ToUtc(dto.SurveyEnd, "Khảo sát kết thúc"),
            ToUtc(dto.BiddingStart, "Mở trả giá"), ToUtc(dto.BiddingEnd, "Đóng trả giá") };
        var deadline = times[3].AddMinutes(LiquidationV2Rules.MaxExtensionMinutes).AddHours(dto.CollectionWindowHours);
        LiquidationRules.CheckSchedule(times[0], times[1], times[2], times[3], deadline);
        var now = Now;
        if (times[0] < now.AddMinutes(-5)) throw new ArgumentException("Lịch khảo sát không được bắt đầu trong quá khứ.");
        if (times[3] - times[2] < MinBiddingWindow || times[3] - times[2] > MaxBiddingWindow)
            throw new ArgumentException("Thời gian trả giá phải từ 1 phút đến 30 ngày.");
        var hash = Hash(sellerId, dto, times);

        await using var tx = await work.BeginAsync();
        await repository.LockOperationAsync(dto.OperationId);
        var existing = await repository.FindLotAsync(dto.OperationId);
        if (existing != null)
        {
            if (existing.SellerId != sellerId || existing.RequestHash != hash) throw new DepotConflictException("Mã thao tác đã dùng cho phiên khác.");
            return await SellerDetailAsync(existing, now);
        }
        if (!await repository.IsActiveSellerAsync(sellerId)) throw new DepotForbiddenException("Tài khoản Seller không còn hoạt động.");
        if (await repository.CountOpenLotsAsync(sellerId) >= MaxOpenLots) throw new DepotConflictException($"Tối đa {MaxOpenLots} phiên đang mở cùng lúc.");
        // Cột trọn lô cũ được điền tổng khởi điểm/bước nhỏ nhất để giữ ràng buộc V1; luật V2 nằm ở từng hạng mục.
        var scope = string.Join("; ", dto.Items.Select(i => $"{i.Name}: {i.Quantity:0.###} {LiquidationV2Rules.UnitLabel(i.Unit)}"));
        var lot = new LiquidationLot
        {
            Id = dto.OperationId, SellerId = sellerId, Title = title, Description = dto.Description!.Trim(),
            ScopeDetails = scope.Length > 3000 ? scope[..3000] : scope, CollectionTerms = dto.CollectionTerms!.Trim(),
            PublicArea = dto.PublicArea!.Trim(), Address = dto.Address!.Trim(), ContactPhone = dto.ContactPhone!.Trim(), ImageUrls = [.. urls],
            SurveyStart = times[0], SurveyEnd = times[1], BiddingStart = times[2], OriginalEnd = times[3], BiddingEnd = times[3],
            CollectionDeadline = deadline, StartingPrice = Math.Max(1, dto.Items.Sum(i => LiquidationV2Rules.Amount(i.Quantity, i.StartingUnitPrice))),
            BidIncrement = dto.Items.Min(i => i.BidIncrement), DepositPercent = 0, Status = "PUBLISHED", RequestHash = hash, CreatedAt = now,
            FormatVersion = 2, PricingBasis = ConfirmedQuantityPricing, CollectionWindowHours = dto.CollectionWindowHours,
        };
        repository.AddLot(lot);
        short position = 1;
        foreach (var i in dto.Items)
            repository.AddItem(new()
            {
                LotId = lot.Id, Position = position++, Kind = LiquidationV2Rules.KindOf(i.Unit), MaterialCode = i.MaterialCode, Name = i.Name,
                Description = i.Description, Unit = i.Unit, Quantity = i.Quantity, StartingUnitPrice = i.StartingUnitPrice, BidIncrement = i.BidIncrement,
            });
        repository.AddNotice(SellerNotice(sellerId, lot.Id, "Đã đăng phiên thanh lý nhiều hạng mục",
            $"Phiên {lot.Title} gồm {dto.Items.Count} hạng mục đã được đăng. Bạn theo dõi đăng ký khảo sát và giá từng hạng mục trong chi tiết phiên."));
        await work.SaveAsync(); await tx.CommitAsync();
        return await SellerDetailAsync(lot, now);
    }

    public async Task<PagedResult<SellerLiquidationV2SummaryDto>> ListAsync(Guid sellerId, string? search, int page, int pageSize)
    {
        CheckPage(page, pageSize, search);
        var now = Now;
        var rows = await repository.SellerListAsync(sellerId, search?.Trim(), page, pageSize);
        return new() { Page = rows.Page, PageSize = rows.PageSize, TotalCount = rows.TotalCount,
            Items = rows.Items.Select(r => new SellerLiquidationV2SummaryDto(Summary(r.Lot, now, r.ItemCount, r.ItemNames) with { WonItemCount = r.WonItemCount }, r.ParticipantCount)).ToList() };
    }

    private async Task<LiquidationLot> OwnedLotAsync(Guid sellerId, Guid lotId)
    {
        var lot = await repository.FindLotAsync(lotId);
        // Không phân biệt "không tồn tại" và "của Seller khác" để không lộ ID.
        if (lot == null || lot.SellerId != sellerId) throw new KeyNotFoundException("Không tìm thấy phiên thanh lý.");
        return lot;
    }

    public async Task<SellerLiquidationV2DetailDto> DetailAsync(Guid sellerId, Guid lotId) => await SellerDetailAsync(await OwnedLotAsync(sellerId, lotId), Now);

    public async Task<PagedResult<SellerLiquidationV2BidDto>> ItemBidsAsync(Guid sellerId, Guid lotId, Guid itemId, int page)
    {
        if (page < 1) throw new ArgumentException("Trang không hợp lệ.");
        var lot = await OwnedLotAsync(sellerId, lotId);
        var item = (await repository.ItemsAsync(lot.Id)).SingleOrDefault(x => x.Id == itemId) ?? throw new KeyNotFoundException("Không tìm thấy hạng mục.");
        var rows = await repository.ItemBidsAsync(item.Id, page);
        var names = await repository.DepotNamesAsync(rows.Items.Select(x => x.DepotId));
        var offset = (page - 1) * rows.PageSize;
        // Seller là chủ hàng nên được xem kho trả giá; Chủ kho đối thủ thì không.
        return new() { Page = rows.Page, PageSize = rows.PageSize, TotalCount = rows.TotalCount,
            Items = rows.Items.Select((x, i) => new SellerLiquidationV2BidDto(offset + i + 1, names.GetValueOrDefault(x.DepotId, "Kho"), x.UnitPrice, x.CreatedAt, x.Id == item.HighestBidId)).ToList() };
    }

    public async Task CancelAsync(Guid sellerId, Guid lotId, string reason)
    {
        reason = reason?.Trim() ?? "";
        if (reason.Length is < 5 or > 1000) throw new ArgumentException("Cần lý do hủy từ 5 đến 1000 ký tự.");
        await OwnedLotAsync(sellerId, lotId);
        await using var tx = await work.BeginAsync();
        var lot = await repository.LockLotAsync(lotId);
        if (lot == null || lot.SellerId != sellerId) throw new KeyNotFoundException("Không tìm thấy phiên thanh lý.");
        if (lot.Status == "CANCELLED" && lot.CancelReason == reason) return;
        if (lot.Status != "PUBLISHED" || Now >= lot.BiddingEnd) throw new DepotConflictException("Phiên đã kết thúc hoặc không còn ở trạng thái mở.");
        if (lot.BidCount > 0) throw new DepotConflictException("Phiên đã có lượt trả giá nên không thể tự hủy; cần Admin xử lý để không bỏ rơi người trả giá.");
        lot.Status = "CANCELLED"; lot.CancelReason = reason;
        repository.AddNotice(SellerNotice(sellerId, lot.Id, "Đã hủy phiên thanh lý", $"Phiên {lot.Title} đã bị hủy. Lý do: {reason}"));
        foreach (var p in await repository.ParticipantsAsync(lotId))
            repository.AddNotice(Notification.ForDepot(p.OwnerId, p.DepotId, "LIQUIDATION", "Phiên thanh lý đã bị hủy",
                $"Seller đã hủy phiên {lot.Title}. Lý do: {reason}", targetType: "LIQUIDATION_V2", targetId: lot.Id));
        await work.SaveAsync(); await tx.CommitAsync();
    }

    public async Task<LiquidationV2AgreementDto> SellerActionAsync(Guid sellerId, Guid agreementId, string action, string? text = null)
    {
        var initial = await repository.FindAgreementAsync(agreementId);
        if (initial == null || initial.Lot.SellerId != sellerId) throw new KeyNotFoundException("Không tìm thấy thỏa thuận.");
        await using var tx = await work.BeginAsync();
        // Cùng thứ tự khóa kho → phiên với Chủ kho và luồng nhập kho để tránh deadlock/race.
        await depots.LockDepotAsync(initial.DepotId);
        await repository.LockLotAsync(initial.LotId);
        var a = await repository.FindAgreementAsync(agreementId) ?? throw new KeyNotFoundException("Không tìm thấy thỏa thuận.");
        if (!await repository.IsActiveSellerAsync(sellerId)) throw new DepotForbiddenException("Tài khoản Seller không còn hoạt động.");
        var now = Now;
        if (action == "REPORT_ABANDON") ReportAbandon(a, sellerId, text, now, toUser: a.BuyerId, depotId: a.DepotId);
        else
        {
            RequireActive(a);
            if (action == "CONFIRM_PAYMENT")
            {
                RequireQuantityConfirmed(a);
                if (a.PaymentReportedAt == null) throw new DepotConflictException("Chủ kho chưa báo đã thanh toán nên chưa thể xác nhận đã nhận.");
                if (a.PaymentConfirmedAt == null)
                {
                    a.PaymentConfirmedAt = now;
                    repository.AddNotice(Notification.ForDepot(a.BuyerId, a.DepotId, "LIQUIDATION", "Seller đã xác nhận nhận tiền",
                        $"Seller xác nhận đã nhận đủ {a.Amount:N0} đ cho phiên {a.Lot.Title}. Hai bên có thể bàn giao hàng.", targetType: "LIQUIDATION_V2", targetId: a.LotId));
                }
            }
            else if (action == "CONFIRM_HANDOVER")
            {
                if (a.PaymentConfirmedAt == null) throw new DepotConflictException("Cần xác nhận đã nhận đủ tiền trước khi ghi nhận bàn giao.");
                if (a.SellerHandedOverAt == null)
                {
                    a.SellerHandedOverAt = now;
                    repository.AddNotice(Notification.ForDepot(a.BuyerId, a.DepotId, "LIQUIDATION", "Seller đã xác nhận bàn giao",
                        $"Seller xác nhận đã bàn giao đủ các hạng mục đã bán của phiên {a.Lot.Title}. Kiểm tra và xác nhận đã nhận hàng.", targetType: "LIQUIDATION_V2", targetId: a.LotId));
                }
            }
            else throw new DepotForbiddenException();
        }
        await work.SaveAsync(); await tx.CommitAsync();
        return await AgreementDtoAsync(a, forSeller: true);
    }

    private async Task<SellerLiquidationV2DetailDto> SellerDetailAsync(LiquidationLot lot, DateTime now)
    {
        var items = await repository.ItemsAsync(lot.Id);
        var counts = await repository.ItemParticipantCountsAsync(lot.Id);
        var agreements = await repository.LotAgreementsAsync(lot.Id);
        var names = await repository.DepotNamesAsync(agreements.Select(x => x.DepotId));
        var winners = agreements.SelectMany(a => a.Items.Select(i => (i.ItemId, a.DepotId, i.Amount))).ToDictionary(x => x.ItemId);
        var dtos = new List<LiquidationV2AgreementDto>();
        foreach (var a in agreements) dtos.Add(await AgreementDtoAsync(a, forSeller: true));
        return new(Summary(lot, now, items.Count, [.. items.Take(4).Select(x => x.Name)]) with { WonItemCount = items.Count(x => x.Status == "WON") },
            await repository.ParticipantCountAsync(lot.Id), lot.Description, lot.CollectionTerms, lot.Address, lot.ContactPhone, lot.CancelReason,
            lot.Status == "PUBLISHED" && lot.BidCount == 0 && now < lot.BiddingEnd,
            items.Select(x => new SellerLiquidationV2ItemDto(x.Id, x.Position, x.Kind, x.MaterialCode, x.Name, x.Description, x.Unit, x.Quantity,
                x.StartingUnitPrice, x.BidIncrement, x.HighestUnitPrice, x.BidCount, counts.GetValueOrDefault(x.Id), x.Status,
                winners.TryGetValue(x.Id, out var w) ? names.GetValueOrDefault(w.DepotId) : null, winners.TryGetValue(x.Id, out var w2) ? w2.Amount : null)).ToList(),
            dtos);
    }

    // ───────────────────────── Chủ kho ─────────────────────────
    public async Task<PagedResult<LiquidationV2SummaryDto>> ListAsync(Guid ownerId, Guid depotId, string? view, string? search, int page, int pageSize)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        CheckPage(page, pageSize, search);
        if (view is not (null or "" or "OPEN" or "PURCHASES" or "HISTORY")) throw new ArgumentException("Bộ lọc không hợp lệ.");
        var now = Now;
        var rows = await repository.DepotListAsync(ownerId, depotId, view, search?.Trim(), page, pageSize);
        return new() { Page = rows.Page, PageSize = rows.PageSize, TotalCount = rows.TotalCount,
            Items = rows.Items.Select(r => Summary(r.Lot, now, r.ItemCount, r.ItemNames) with
                { Registered = r.Registered, AgreementId = r.AgreementId, RegisteredItemCount = r.RegisteredItemCount, WonItemCount = r.WonItemCount }).ToList() };
    }

    public async Task<LiquidationV2DetailDto> DetailAsync(Guid ownerId, Guid depotId, Guid lotId)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var lot = await repository.FindLotAsync(lotId) ?? throw new KeyNotFoundException("Không tìm thấy phiên thanh lý.");
        return await DepotDetailAsync(lot, ownerId, depotId, Now);
    }

    private async Task<LiquidationV2DetailDto> DepotDetailAsync(LiquidationLot lot, Guid ownerId, Guid depotId, DateTime now)
    {
        var items = await repository.ItemsAsync(lot.Id);
        var participation = await repository.ParticipationAsync(lot.Id, ownerId);
        var mine = participation?.DepotId == depotId;
        var registered = mine ? (await repository.ParticipationItemIdsAsync(participation!.Id)).ToHashSet() : [];
        var myBids = mine ? (await repository.MyLatestBidsAsync(lot.Id, ownerId, depotId)).ToDictionary(x => x.ItemId) : [];
        var leaders = (await repository.BidsByIdsAsync(items.Where(x => x.HighestBidId.HasValue).Select(x => x.HighestBidId!.Value)))
            .ToDictionary(x => x.Id);
        var agreement = await repository.MyAgreementAsync(lot.Id, ownerId, depotId);
        var sellerActive = await repository.IsActiveSellerAsync(lot.SellerId);
        var open = lot.Status == "PUBLISHED" && sellerActive;
        var canBid = mine && participation!.SurveyDeclaredAt != null && open && now >= lot.BiddingStart && now < lot.BiddingEnd;
        var dtos = items.Select(x =>
        {
            var leading = x.HighestBidId.HasValue && leaders.TryGetValue(x.HighestBidId.Value, out var b) && b.OwnerId == ownerId && b.DepotId == depotId;
            var my = myBids.TryGetValue(x.Id, out var m) ? m : null;
            return new LiquidationV2ItemDto(x.Id, x.Position, x.Kind, x.MaterialCode, x.Name, x.Description, x.Unit, x.Quantity, x.StartingUnitPrice,
                x.BidIncrement, x.HighestUnitPrice, x.BidCount, LiquidationV2Rules.NextMin(x), x.Status, registered.Contains(x.Id), my?.UnitPrice, my?.CreatedAt,
                leading, x.Status == "WON" && leading, x.HighestUnitPrice.HasValue ? LiquidationV2Rules.Amount(x.Quantity, x.HighestUnitPrice.Value) : null,
                my != null ? LiquidationV2Rules.Amount(x.Quantity, my.UnitPrice) : null, LiquidationV2Rules.Amount(x.Quantity, x.StartingUnitPrice));
        }).ToList();
        // Địa chỉ/điện thoại chi tiết chỉ mở cho kho đã đăng ký khảo sát.
        return new(Summary(lot, now, items.Count, [.. items.Take(4).Select(x => x.Name)]) with
            { Registered = mine, AgreementId = agreement?.Id, RegisteredItemCount = registered.Count, WonItemCount = dtos.Count(x => x.WonByMe) },
            lot.Description, lot.CollectionTerms, mine ? lot.Address : null, mine ? lot.ContactPhone : null, mine ? depotId : null,
            mine && participation!.SurveyDeclaredAt != null, open && now < lot.SurveyEnd && lot.SellerId != ownerId, canBid, lot.CancelReason, dtos, agreement?.Id);
    }

    public async Task<LiquidationV2DetailDto> RegisterAsync(Guid ownerId, Guid depotId, Guid lotId, LiquidationV2ParticipationInput input)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (input == null || !input.Accepted) throw new ArgumentException("Cần xác nhận điều kiện của phiên.");
        var ids = input.ItemIds?.Distinct().ToList() ?? [];
        if (ids.Count is < 1 or > LiquidationV2Rules.MaxItems) throw new ArgumentException("Chọn ít nhất một hạng mục muốn tham gia.");
        await using var tx = await work.BeginAsync();
        await depots.LockDepotAsync(depotId);
        var lot = await repository.LockLotAsync(lotId) ?? throw new KeyNotFoundException("Không tìm thấy phiên.");
        var now = Now;
        if (lot.SellerId == ownerId || lot.Status != "PUBLISHED" || !await repository.IsActiveSellerAsync(lot.SellerId))
            throw new DepotConflictException("Phiên không còn cho đăng ký.");
        if (now >= lot.SurveyEnd) throw new DepotConflictException("Đã hết thời gian đăng ký khảo sát.");
        var items = await repository.ItemsAsync(lot.Id);
        if (ids.Any(id => items.All(x => x.Id != id))) throw new ArgumentException("Có hạng mục không thuộc phiên này.");
        var participation = await repository.ParticipationAsync(lotId, ownerId);
        if (participation != null && participation.DepotId != depotId)
            throw new DepotConflictException("Bạn đã tham gia bằng kho khác. Một chủ kho chỉ dùng một kho trong mỗi phiên.");
        var existing = participation == null ? [] : (await repository.ParticipationItemIdsAsync(participation.Id)).ToHashSet();
        var added = ids.Where(id => !existing.Contains(id)).ToList();
        if (added.Count > 0)
        {
            await scope.RequireTradingAsync(ownerId, depotId);
            if (participation == null)
            {
                participation = new() { LotId = lotId, DepotId = depotId, OwnerId = ownerId, RegisteredAt = now };
                repository.AddParticipation(participation);
                repository.AddNotice(SellerNotice(lot.SellerId, lot.Id, "Có kho đăng ký khảo sát",
                    $"Một kho đã đăng ký khảo sát {added.Count} hạng mục của phiên {lot.Title}."));
            }
            foreach (var id in added) repository.AddParticipationItem(new() { ParticipationId = participation.Id, ItemId = id, RegisteredAt = now });
            await work.SaveAsync();
        }
        await tx.CommitAsync();
        return await DepotDetailAsync(lot, ownerId, depotId, now);
    }

    public async Task<LiquidationV2DetailDto> ConfirmSurveyAsync(Guid ownerId, Guid depotId, Guid lotId, bool accepted)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (!accepted) throw new ArgumentException("Cần xác nhận đã khảo sát và hiểu hiện trạng.");
        await using var tx = await work.BeginAsync();
        await depots.LockDepotAsync(depotId);
        var lot = await repository.LockLotAsync(lotId) ?? throw new KeyNotFoundException("Không tìm thấy phiên.");
        var now = Now;
        if (lot.Status != "PUBLISHED" || !await repository.IsActiveSellerAsync(lot.SellerId)) throw new DepotConflictException("Phiên không còn hoạt động.");
        var participation = await repository.ParticipationAsync(lotId, ownerId);
        if (participation?.DepotId != depotId || now < lot.SurveyStart || now >= lot.BiddingEnd)
            throw new DepotConflictException("Cần đăng ký hạng mục trước và xác nhận hiện trạng từ lúc bắt đầu khảo sát đến trước khi đóng phiên.");
        if (participation.SurveyDeclaredAt == null)
        {
            await scope.RequireTradingAsync(ownerId, depotId);
            participation.SurveyDeclaredAt = now;
            repository.AddNotice(SellerNotice(lot.SellerId, lot.Id, "Kho đã xác nhận khảo sát",
                $"Một kho đã xác nhận hiểu hiện trạng phiên {lot.Title} và có thể trả giá khi phiên mở."));
            await work.SaveAsync();
        }
        await tx.CommitAsync();
        return await DepotDetailAsync(lot, ownerId, depotId, now);
    }

    public async Task<LiquidationV2BidResultDto> BidAsync(Guid ownerId, Guid depotId, Guid lotId, Guid itemId, LiquidationV2BidInput input)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (input == null || input.OperationId == Guid.Empty) throw new ArgumentException("Cần mã thao tác trả giá.");
        LiquidationRules.CheckMoney(input.UnitPrice);
        await using var tx = await work.BeginAsync();
        await depots.LockDepotAsync(depotId);
        await repository.LockOperationAsync(input.OperationId);
        var lot = await repository.LockLotAsync(lotId) ?? throw new KeyNotFoundException("Không tìm thấy phiên.");
        var items = await repository.ItemsAsync(lot.Id);
        var item = items.SingleOrDefault(x => x.Id == itemId) ?? throw new KeyNotFoundException("Không tìm thấy hạng mục.");
        var old = await repository.FindBidAsync(input.OperationId);
        if (old != null)
        {
            // Gửi lại cùng thao tác trả về kết quả cũ, không gia hạn thêm.
            if (old.OwnerId != ownerId || old.DepotId != depotId || old.ItemId != itemId || old.UnitPrice != input.UnitPrice)
                throw new DepotConflictException("Mã thao tác đã dùng cho lượt trả giá khác.");
            return new(old.Id, old.ItemId, old.UnitPrice, LiquidationV2Rules.Amount(item.Quantity, old.UnitPrice), old.CreatedAt, lot.BiddingEnd, lot.ExtensionCount, false);
        }
        await scope.RequireTradingAsync(ownerId, depotId);
        var now = Now;
        var participation = await repository.ParticipationAsync(lotId, ownerId);
        if (participation?.DepotId != depotId || participation.SurveyDeclaredAt == null || lot.SellerId == ownerId)
            throw new DepotForbiddenException("Kho cần đăng ký hạng mục và tự xác nhận đã khảo sát/hiểu hiện trạng trước khi trả giá.");
        if (!(await repository.ParticipationItemIdsAsync(participation.Id)).Contains(itemId))
            throw new DepotForbiddenException("Kho chưa đăng ký hạng mục này.");
        if (lot.Status != "PUBLISHED" || now < lot.BiddingStart || now >= lot.BiddingEnd || item.Status != "OPEN" || !await repository.IsActiveSellerAsync(lot.SellerId))
            throw new DepotConflictException("Phiên chưa mở, đã hết hạn hoặc không còn hoạt động.");
        if (input.ExpectedBidCount.HasValue && input.ExpectedBidCount.Value != item.BidCount)
            throw new DepotConflictException($"Giá hạng mục {item.Name} vừa thay đổi. Mức tối thiểu mới là {LiquidationV2Rules.NextMin(item):N0} đ/{LiquidationV2Rules.UnitLabel(item.Unit)}; kiểm tra lại trước khi trả giá.");
        LiquidationV2Rules.CheckBid(item, input.UnitPrice);
        var previous = item.HighestBidId.HasValue ? await repository.FindBidAsync(item.HighestBidId.Value) : null;
        if (previous != null && (previous.OwnerId != ownerId || previous.DepotId != depotId))
            repository.AddNotice(Notification.ForDepot(previous.OwnerId, previous.DepotId, "LIQUIDATION", "Giá của bạn đã bị vượt",
                $"Hạng mục {item.Name} của phiên {lot.Title} có đơn giá cao hơn. Xem giá hiện tại và thời gian còn lại trước khi trả tiếp.",
                targetType: "LIQUIDATION_V2", targetId: lot.Id));
        var (end, count, extended) = LiquidationV2Rules.Extend(now, lot.BiddingEnd, lot.ExtensionCount);
        var bid = new LiquidationItemBid { Id = input.OperationId, LotId = lot.Id, ItemId = item.Id, OwnerId = ownerId, DepotId = depotId,
            UnitPrice = input.UnitPrice, CreatedAt = now, ExtendedEnd = extended ? end : null };
        repository.AddBid(bid);
        item.HighestUnitPrice = input.UnitPrice; item.HighestBidId = bid.Id; item.BidCount++;
        lot.BidCount++; lot.BiddingEnd = end; lot.ExtensionCount = count;
        if (extended)
            repository.AddNotice(SellerNotice(lot.SellerId, lot.Id, "Phiên được gia hạn sát giờ",
                $"Phiên {lot.Title} nhận giá mới trong 2 phút cuối nên giờ đóng chung lùi thêm {LiquidationV2Rules.ExtensionMinutes} phút (lần {count}/{LiquidationV2Rules.MaxExtensions})."));
        await work.SaveAsync(); await tx.CommitAsync();
        return new(bid.Id, item.Id, bid.UnitPrice, LiquidationV2Rules.Amount(item.Quantity, bid.UnitPrice), bid.CreatedAt, lot.BiddingEnd, lot.ExtensionCount, extended);
    }

    public async Task<PagedResult<LiquidationV2MyBidDto>> MyBidsAsync(Guid ownerId, Guid depotId, Guid lotId, Guid itemId, int page)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (page < 1) throw new ArgumentException("Trang không hợp lệ.");
        var lot = await repository.FindLotAsync(lotId) ?? throw new KeyNotFoundException("Không tìm thấy phiên.");
        if ((await repository.ItemsAsync(lot.Id)).All(x => x.Id != itemId)) throw new KeyNotFoundException("Không tìm thấy hạng mục.");
        var rows = await repository.ItemBidsAsync(itemId, page, ownerId, depotId);
        return new() { Page = rows.Page, PageSize = rows.PageSize, TotalCount = rows.TotalCount,
            Items = rows.Items.Select(x => new LiquidationV2MyBidDto(x.Id, x.ItemId, x.UnitPrice, x.CreatedAt)).ToList() };
    }

    private async Task<LiquidationAgreement> BuyerAgreementAsync(Guid ownerId, Guid depotId, Guid agreementId)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var a = await repository.FindAgreementAsync(agreementId);
        if (a == null || a.BuyerId != ownerId || a.DepotId != depotId) throw new KeyNotFoundException("Không tìm thấy thỏa thuận.");
        return a;
    }

    public async Task<LiquidationV2AgreementDto> AgreementAsync(Guid ownerId, Guid depotId, Guid agreementId) =>
        await AgreementDtoAsync(await BuyerAgreementAsync(ownerId, depotId, agreementId), forSeller: false);

    public async Task<LiquidationV2AgreementDto> BuyerActionAsync(Guid ownerId, Guid depotId, Guid agreementId, string action, string? text = null)
    {
        var initial = await BuyerAgreementAsync(ownerId, depotId, agreementId);
        await using var tx = await work.BeginAsync();
        await depots.LockDepotAsync(initial.DepotId);
        await repository.LockLotAsync(initial.LotId);
        var a = await BuyerAgreementAsync(ownerId, depotId, agreementId);
        var now = Now;
        if (action == "REPORT_ABANDON") ReportAbandon(a, ownerId, text, now, toUser: a.Lot.SellerId, depotId: null);
        else
        {
            RequireActive(a);
            if (action == "REPORT_PAYMENT")
            {
                RequireQuantityConfirmed(a);
                var reference = text?.Trim() ?? "";
                if (reference.Length is < 1 or > 255) throw new ArgumentException("Cần ghi chú/tham chiếu thanh toán tối đa 255 ký tự.");
                if (a.PaymentReportedAt != null && a.PaymentReference != reference) throw new DepotConflictException("Tham chiếu đã ghi, không sửa ngầm bằng gửi lại.");
                if (a.PaymentReportedAt == null)
                {
                    a.PaymentReference = reference; a.PaymentReportedAt = now;
                    repository.AddNotice(SellerNotice(a.Lot.SellerId, a.LotId, "Chủ kho báo đã thanh toán",
                        $"Chủ kho báo đã thanh toán {a.Amount:N0} đ cho phiên {a.Lot.Title}. Hãy đối chiếu tiền thực nhận trước khi xác nhận."));
                }
            }
            else if (action == "CONFIRM_RECEIVED")
            {
                if (a.PaymentConfirmedAt == null || a.SellerHandedOverAt == null)
                    throw new DepotConflictException("Cần Seller xác nhận đã nhận tiền và đã bàn giao trước khi kho xác nhận nhận hàng.");
                if (a.OwnerCollectedAt == null)
                {
                    a.OwnerCollectedAt = now;
                    repository.AddNotice(SellerNotice(a.Lot.SellerId, a.LotId, "Chủ kho đã nhận hàng",
                        $"Chủ kho xác nhận đã nhận đủ các hạng mục đã mua của phiên {a.Lot.Title}. Thỏa thuận hoàn tất."));
                }
            }
            else throw new DepotForbiddenException();
        }
        await work.SaveAsync(); await tx.CommitAsync();
        return await AgreementDtoAsync(a, forSeller: false);
    }

    public async Task<LiquidationIntakeSourceDto> IntakeSourceAsync(Guid ownerId, Guid depotId, Guid agreementId)
    {
        var a = await BuyerAgreementAsync(ownerId, depotId, agreementId);
        var receipt = await repository.ReceiptAsync(a.Id);
        var items = await repository.ItemsAsync(a.LotId);
        var cancelled = a.Resolution == "CANCELLED";
        var disputed = a.AbandonReportedAt != null && a.Resolution == null;
        var quantityReady = a.PricingBasis != ConfirmedQuantityPricing || a.HandoverStatus == "CONFIRMED";
        var handover = a.HandoverStatus == "CONFIRMED" ? await repository.HandoverAsync(a.Id,a.HandoverRevision) : null;
        var ready = quantityReady && !cancelled && !disputed && receipt == null && a.PaymentConfirmedAt != null && a.SellerHandedOverAt != null;
        return new(a.PricingBasis == ConfirmedQuantityPricing ? 3 : 2, a.Id, a.LotId, a.DepotId, Revision(a), "VND", a.PricingBasis, a.PaymentConfirmedAt != null, a.SellerHandedOverAt != null,
            a.OwnerCollectedAt != null, cancelled, ready, a.Amount, receipt?.Id,
            a.Items.OrderBy(x => items.FirstOrDefault(i => i.Id == x.ItemId)?.Position ?? 0).Select(x =>
            {
                var item = items.FirstOrDefault(i => i.Id == x.ItemId);
                var confirmed = handover?.Lines.SingleOrDefault(l=>l.AgreementItemId==x.Id);
                return new LiquidationIntakeItemDto(x.Id, x.ItemId, item?.Kind ?? LiquidationV2Rules.KindOf(x.Unit), item?.MaterialCode, x.Name, x.Unit, x.Quantity, x.UnitPrice, confirmed?.Amount ?? x.Amount, confirmed?.Quantity);
            }).ToList());
    }

    // ───────────────────────── Đóng phiên ─────────────────────────
    public Task<List<Guid>> ExpiredAsync() => repository.ExpiredAsync(Now);

    public async Task FinalizeAsync(Guid lotId)
    {
        await using var tx = await work.BeginAsync();
        var lot = await repository.LockLotAsync(lotId);
        var now = Now;
        if (lot == null || lot.Status != "PUBLISHED" || now < lot.BiddingEnd) return;
        var items = await repository.ItemsAsync(lot.Id);
        var bids = (await repository.BidsByIdsAsync(items.Where(x => x.HighestBidId.HasValue).Select(x => x.HighestBidId!.Value))).ToDictionary(x => x.Id);
        var won = new List<(LiquidationItem Item, LiquidationItemBid Bid)>();
        foreach (var item in items)
        {
            if (item.HighestBidId.HasValue && bids.TryGetValue(item.HighestBidId.Value, out var bid))
            {
                if (bid.ItemId != item.Id || bid.UnitPrice != item.HighestUnitPrice) throw new InvalidOperationException("Giá thắng không khớp hạng mục; cần kiểm tra dữ liệu phiên.");
                item.Status = "WON"; won.Add((item, bid));
            }
            else item.Status = "UNSOLD";
        }
        lot.Status = won.Count > 0 ? "AWARDED" : "NO_SALE";
        lot.ClosedAt = now;
        // Hạn thu gom tính từ lúc đóng thực tế theo khoảng thời gian Seller đã công bố.
        lot.CollectionDeadline = now.AddHours(lot.CollectionWindowHours ?? LiquidationV2Rules.MinCollectionWindowHours);
        foreach (var group in won.GroupBy(x => (x.Bid.OwnerId, x.Bid.DepotId)))
        {
            var lines = group.Select(x => new LiquidationAgreementItem
            {
                ItemId = x.Item.Id, WinningBidId = x.Bid.Id, Name = x.Item.Name, Unit = x.Item.Unit, Quantity = x.Item.Quantity,
                UnitPrice = x.Bid.UnitPrice, Amount = LiquidationV2Rules.Amount(x.Item.Quantity, x.Bid.UnitPrice)
            }).ToList();
            var agreement = new LiquidationAgreement
            {
                LotId = lot.Id, FormatVersion = 2, PricingBasis = lot.PricingBasis, WinningBidId = null, DepotId = group.Key.DepotId, BuyerId = group.Key.OwnerId,
                Amount = lines.Sum(x => x.Amount), DepositAmount = 0, CreatedAt = now, Items = lines
            };
            repository.AddAgreement(agreement);
            repository.AddNotice(Notification.ForDepot(group.Key.OwnerId, group.Key.DepotId, "LIQUIDATION", "Kho của bạn đã thắng hạng mục",
                $"Bạn thắng {lines.Count} hạng mục của phiên {lot.Title}, tạm tính {agreement.Amount:N0} đ theo lượng đăng. {(agreement.PricingBasis == ConfirmedQuantityPricing ? "Cần chốt cân/kiểm đếm với Seller trước khi thanh toán. " : "")}Hai bên bàn giao trước {lot.CollectionDeadline:HH:mm dd/MM/yyyy} UTC.",
                targetType: "LIQUIDATION_V2", targetId: lot.Id));
        }
        var winners = won.Select(x => (x.Bid.OwnerId, x.Bid.DepotId)).ToHashSet();
        foreach (var p in await repository.ParticipantsAsync(lot.Id))
            if (!winners.Contains((p.OwnerId, p.DepotId)))
                repository.AddNotice(Notification.ForDepot(p.OwnerId, p.DepotId, "LIQUIDATION", "Phiên thanh lý đã kết thúc",
                    $"Phiên {lot.Title} đã đóng. Kho của bạn không thắng hạng mục nào.", targetType: "LIQUIDATION_V2", targetId: lot.Id));
        repository.AddNotice(SellerNotice(lot.SellerId, lot.Id, won.Count > 0 ? "Phiên thanh lý đã có kết quả" : "Phiên kết thúc chưa bán được",
            won.Count > 0 ? $"Phiên {lot.Title}: {won.Count}/{items.Count} hạng mục có kho thắng, chia cho {winners.Count} kho. Mở chi tiết để theo dõi thanh toán và bàn giao."
                : $"Phiên {lot.Title} kết thúc mà không có lượt trả giá hợp lệ."));
        await work.SaveAsync(); await tx.CommitAsync();
    }

    // ───────────────────────── Admin ─────────────────────────
    public async Task<PagedResult<LiquidationDisputeDto>> DisputesAsync(bool openOnly, int page, int pageSize)
    {
        CheckPage(page, pageSize, null);
        var rows = await repository.DisputesAsync(openOnly, page, pageSize);
        return new() { Page = rows.Page, PageSize = rows.PageSize, TotalCount = rows.TotalCount, Items = rows.Items.Select(DisputeDto).ToList() };
    }

    public async Task<LiquidationDisputeDto> ResolveAsync(Guid adminId, Guid agreementId, LiquidationDisputeResolveInput input)
    {
        var resolution = input?.Resolution?.Trim().ToUpperInvariant() ?? "";
        var note = input?.Note?.Trim() ?? "";
        if (resolution is not ("CONTINUE" or "CANCELLED")) throw new ArgumentException("Chỉ chọn Tiếp tục thực hiện hoặc Hủy thỏa thuận.");
        if (note.Length is < 5 or > 1000) throw new ArgumentException("Cần ghi chú xử lý từ 5 đến 1000 ký tự.");
        var initial = await repository.FindAgreementAsync(agreementId) ?? throw new KeyNotFoundException("Không tìm thấy thỏa thuận.");
        await using var tx = await work.BeginAsync();
        await depots.LockDepotAsync(initial.DepotId);
        await repository.LockLotAsync(initial.LotId);
        var a = await repository.FindAgreementAsync(agreementId) ?? throw new KeyNotFoundException("Không tìm thấy thỏa thuận.");
        if (a.AbandonReportedAt == null) throw new DepotConflictException("Thỏa thuận chưa có báo bỏ mua cần xử lý.");
        if (a.Resolution != null)
        {
            if (a.Resolution == resolution && a.ResolutionNote == note) return DisputeDto(await DisputeRowAsync(a));
            throw new DepotConflictException("Báo bỏ mua đã được xử lý.");
        }
        if (resolution == "CANCELLED" && (a.SellerHandedOverAt != null || a.OwnerCollectedAt != null || await repository.ReceiptAsync(a.Id) != null))
            throw new DepotConflictException("Hàng đã bàn giao/nhập kho nên không hủy thỏa thuận; hai bên cần tự thỏa thuận hoàn trả.");
        a.Resolution = resolution; a.ResolutionNote = note; a.ResolvedAt = Now; a.ResolvedBy = adminId;
        var text = resolution == "CANCELLED"
            ? $"Admin đã hủy thỏa thuận phiên {a.Lot.Title}. Hệ thống không tự phạt hay chuyển cho người trả giá khác; tiền đã giao (nếu có) do hai bên tự hoàn trả. Ghi chú: {note}"
            : $"Admin quyết định tiếp tục thực hiện thỏa thuận phiên {a.Lot.Title}. Ghi chú: {note}";
        repository.AddNotice(SellerNotice(a.Lot.SellerId, a.LotId, "Admin đã xử lý báo bỏ mua", text));
        repository.AddNotice(Notification.ForDepot(a.BuyerId, a.DepotId, "LIQUIDATION", "Admin đã xử lý báo bỏ mua", text, targetType: "LIQUIDATION_V2", targetId: a.LotId));
        await work.SaveAsync(); await tx.CommitAsync();
        return DisputeDto(await DisputeRowAsync(a));
    }

    private async Task<LiquidationDisputeRow> DisputeRowAsync(LiquidationAgreement a)
    {
        var seller = await repository.UserAsync(a.Lot.SellerId);
        var depot = (await repository.DepotNamesAsync([a.DepotId])).GetValueOrDefault(a.DepotId, "Kho");
        return new(a, a.Lot.Title, seller?.FullName ?? "Seller", depot, a.Lot.SellerId);
    }

    private static LiquidationDisputeDto DisputeDto(LiquidationDisputeRow r) => new(r.Agreement.Id, r.Agreement.LotId, r.Title, r.SellerName, r.DepotName,
        r.Agreement.Amount, Stage(r.Agreement), r.Agreement.AbandonReportedAt!.Value, r.Agreement.AbandonReportedBy == r.SellerId ? "SELLER" : "BUYER",
        r.Agreement.AbandonReason!, r.Agreement.Resolution, r.Agreement.ResolutionNote, r.Agreement.ResolvedAt);

    // Thông báo cho Seller của phiên V2: đích LIQUIDATION_V2 để giao diện mở trang phiên nhiều hạng mục, không mở nhầm trang V1.
    private static Notification SellerNotice(Guid seller, Guid lot, string title, string message) => new()
        { UserId = seller, Category = "LIQUIDATION", TargetType = "LIQUIDATION_V2", TargetId = lot, Title = title, Message = message };

    private static void CheckPage(int page, int pageSize, string? search)
    {
        if (page < 1 || pageSize is < 1 or > 100 || search?.Length > 255) throw new ArgumentException("Bộ lọc hoặc phân trang không hợp lệ.");
    }

    private static void RequireActive(LiquidationAgreement a)
    {
        if (a.Resolution == "CANCELLED") throw new DepotConflictException("Thỏa thuận đã được Admin hủy.");
        if (a.AbandonReportedAt != null && a.Resolution == null) throw new DepotConflictException("Thỏa thuận đang chờ Admin xử lý báo bỏ mua.");
    }

    private void ReportAbandon(LiquidationAgreement a, Guid reporter, string? reason, DateTime now, Guid toUser, Guid? depotId)
    {
        reason = reason?.Trim() ?? "";
        if (reason.Length is < 5 or > 1000) throw new ArgumentException("Cần lý do từ 5 đến 1000 ký tự.");
        if (a.AbandonReportedAt != null)
        {
            if (a.AbandonReportedBy == reporter && a.AbandonReason == reason) return;
            throw new DepotConflictException("Thỏa thuận đã có báo bỏ mua; chờ Admin xử lý.");
        }
        // Sau khi Seller xác nhận bàn giao, hàng đã đi; ngoại lệ lúc này do hai bên tự thỏa thuận, không còn là bỏ mua.
        if (a.SellerHandedOverAt != null || a.OwnerCollectedAt != null) throw new DepotConflictException("Hàng đã bàn giao nên không báo bỏ mua; hai bên tự xử lý ngoại lệ.");
        a.AbandonReportedAt = now; a.AbandonReportedBy = reporter; a.AbandonReason = reason;
        var message = $"Có báo bỏ mua/không thực hiện cho phiên {a.Lot.Title}. Lý do: {reason}. Thỏa thuận tạm dừng chờ Admin xử lý.";
        repository.AddNotice(depotId.HasValue
            ? Notification.ForDepot(toUser, depotId.Value, "LIQUIDATION", "Thỏa thuận có báo bỏ mua", message, targetType: "LIQUIDATION_V2", targetId: a.LotId)
            : SellerNotice(toUser, a.LotId, "Thỏa thuận có báo bỏ mua", message));
    }

    /// <summary>Giai đoạn kinh tế suy từ các mốc xác nhận rõ ràng, không nén thành một chữ COMPLETED khó hiểu.</summary>
    private static string Stage(LiquidationAgreement a) =>
        a.Resolution == "CANCELLED" ? "CANCELLED" :
        a.AbandonReportedAt != null && a.Resolution == null ? "DISPUTED" :
        a.PricingBasis == ConfirmedQuantityPricing && a.HandoverStatus != "CONFIRMED"
            ? a.HandoverStatus == "PENDING" ? "WAITING_SELLER_QUANTITY" : "WAITING_BUYER_QUANTITY" :
        a.PaymentConfirmedAt == null ? (a.PaymentReportedAt == null ? "WAITING_BUYER_PAYMENT" : "WAITING_SELLER_CONFIRM") :
        a.SellerHandedOverAt == null ? "WAITING_HANDOVER" : a.OwnerCollectedAt == null ? "WAITING_OWNER_RECEIVE" : "COMPLETED";

    private static string Revision(LiquidationAgreement a) => Retrack.API.Helpers.LiquidationSourceRevision.For(a);

    private async Task<LiquidationV2AgreementDto> AgreementDtoAsync(LiquidationAgreement a, bool forSeller)
    {
        var receipt = await repository.ReceiptAsync(a.Id);
        var depot = (await repository.DepotNamesAsync([a.DepotId])).GetValueOrDefault(a.DepotId, "Kho");
        var seller = await repository.UserAsync(a.Lot.SellerId);
        var buyer = await repository.UserAsync(a.BuyerId);
        var items = await repository.ItemsAsync(a.LotId);
        var stage = Stage(a);
        var handover = a.HandoverRevision == 0 ? null : await repository.HandoverAsync(a.Id, a.HandoverRevision);
        var next = forSeller
            ? stage switch { "WAITING_SELLER_QUANTITY" => "DECIDE_QUANTITY", "WAITING_SELLER_CONFIRM" => "CONFIRM_PAYMENT", "WAITING_HANDOVER" => "CONFIRM_HANDOVER", _ => null }
            : stage switch { "WAITING_BUYER_QUANTITY" => "PROPOSE_QUANTITY", "WAITING_BUYER_PAYMENT" => "REPORT_PAYMENT", "WAITING_OWNER_RECEIVE" => "CONFIRM_RECEIVED", "COMPLETED" when receipt == null => "STOCK_INTAKE", _ => null };
        return new(a.Id, a.LotId, a.Lot.Title, a.DepotId, depot, seller?.FullName ?? "Seller", forSeller ? null : seller?.Phone,
            forSeller ? buyer?.FullName : null, forSeller ? buyer?.Phone : null, forSeller ? a.Lot.Address : a.Lot.Address,
            a.Lot.ContactPhone, a.Lot.CollectionTerms, a.Lot.CollectionDeadline, a.Amount,
            a.Items.OrderBy(x => items.FirstOrDefault(i => i.Id == x.ItemId)?.Position ?? 0)
                .Select(x => new LiquidationV2AgreementItemDto(x.Id, x.ItemId, x.Name, x.Unit, x.Quantity, x.UnitPrice, x.Amount)).ToList(),
            a.PaymentReference, a.PaymentReportedAt, a.PaymentConfirmedAt, a.SellerHandedOverAt, a.OwnerCollectedAt, receipt?.Id, receipt?.Status,
            stage, next, a.AbandonReportedAt, a.AbandonReason,
            a.AbandonReportedAt == null ? null : a.AbandonReportedBy == a.Lot.SellerId ? "SELLER" : "BUYER",
            a.Resolution, a.ResolutionNote, a.ResolvedAt, a.CreatedAt, a.PricingBasis, a.HandoverRevision, a.HandoverStatus, HandoverDto(handover));
    }

    private static LiquidationV2SummaryDto Summary(LiquidationLot x, DateTime now, int itemCount, string[] names) => new(x.Id, x.Title, x.PublicArea,
        itemCount, names, x.BidCount, LiquidationRules.Phase(x.Status, now, x.SurveyStart, x.SurveyEnd, x.BiddingStart, x.BiddingEnd), x.Status,
        x.SurveyStart, x.SurveyEnd, x.BiddingStart, x.BiddingEnd, x.OriginalEnd.AddMinutes(LiquidationV2Rules.MaxExtensionMinutes), x.ExtensionCount,
        LiquidationV2Rules.MaxExtensions - x.ExtensionCount, x.CollectionDeadline, x.CollectionWindowHours ?? 0, x.ImageUrls, now, PricingBasis: x.PricingBasis);
}
