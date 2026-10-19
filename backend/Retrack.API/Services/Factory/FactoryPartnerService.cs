using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Factory;

public class FactoryPartnerService(AppDbContext db) : FactoryServiceBase(db), IFactoryPartnerService
{
    public async Task<ServiceResult<PageResponse<PartnerResponse>>> ListAsync(Guid userId, PageQuery query, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        IQueryable<FactoryDepotPartnership> source = Db.FactoryDepotPartnerships.AsNoTracking().Where(x => x.FactoryId == factory.Id)
            .Include(x => x.Depot).ThenInclude(x => x.Owner);
        if (!string.IsNullOrWhiteSpace(query.PartnerStatus))
            source = query.PartnerStatus == "BLOCKED"
                ? source.Where(x => x.Status == "BLOCKED" || x.BlockedByDepot || x.BlockedByFactory)
                : source.Where(x => x.Status == query.PartnerStatus && !x.BlockedByDepot && !x.BlockedByFactory);
        var total = await source.CountAsync(ct);
        var partnerships = await source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        var depotIds = partnerships.Select(x => x.DepotId).ToArray();
        var reviews = await Db.FactoryDepotReviews.AsNoTracking().Where(x => x.FactoryId == factory.Id && depotIds.Contains(x.DepotId))
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var latestReviews = reviews.GroupBy(x => x.DepotId).ToDictionary(x => x.Key, x => x.First());
        var items = partnerships.Select(x => new PartnerResponse
        {
            Id = x.Id,
            DepotId = x.DepotId,
            Name = x.Depot.Name,
            Address = x.Depot.Address,
            ContactPhone = x.Depot.Owner.Phone,
            Status = x.EffectiveStatus, BlockedByFactory = x.BlockedByFactory, BlockedByDepot = x.BlockedByDepot, LegacyBlocked = x.Status == "BLOCKED",
            Rating = x.Depot.Rating,
            LatestRating = latestReviews.TryGetValue(x.DepotId, out var review) ? review.Rating : null,
            LatestComment = latestReviews.TryGetValue(x.DepotId, out review) ? review.Comment : null,
            ReviewCount = reviews.Count(r => r.DepotId == x.DepotId),
            OrderCount = Db.InventoryBatches.Count(b => b.TargetFactoryId == x.FactoryId && b.DepotId == x.DepotId && b.Status != "MARKETPLACE"),
            CompletedOrderCount = Db.InventoryBatches.Count(b => b.TargetFactoryId == x.FactoryId && b.DepotId == x.DepotId && b.Status == "COMPLETED"),
            ContractCode = x.ContractCode, ContractVersion = x.ContractVersion, ContractTerms = x.ContractTerms,
            ContractTermsHash = x.ContractTermsHash, ContractStatus = x.ContractStatus,
            FactorySignedAt = x.FactorySignedAt, DepotSignedAt = x.DepotSignedAt,
            ContractEffectiveAt = x.ContractEffectiveAt, ContractExpiresAt = x.ContractExpiresAt,
            TerminationStatus = x.TerminationStatus, TerminationRequestedBy = x.TerminationRequestedBy,
            TerminationRequestedAt = x.TerminationRequestedAt, TerminationReason = x.TerminationReason, TerminatedAt = x.TerminatedAt
        }).ToList();
        return ServiceResult<PageResponse<PartnerResponse>>.Success(data: new PageResponse<PartnerResponse>
        {
            Items = items.ToList(),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling((double)total / query.PageSize)
        });
    }

    public async Task<ServiceResult<PartnerStatusResponse>> UpdateStatusAsync(Guid userId, Guid depotId, PartnerStatusRequest request, CancellationToken ct)
    {
        var status = request.Status.ToString();
        if (status is not ("APPROVED" or "DECLINED" or "BLOCKED" or "UNBLOCKED"))
            return ServiceResult<PartnerStatusResponse>.Invalid("Chọn hợp tác, không hợp tác, chặn hoặc bỏ chặn.");
        if (status == "APPROVED")
            return ServiceResult<PartnerStatusResponse>.Invalid("Hãy lập hợp đồng điện tử để chủ vựa cùng xác nhận hợp tác.");
        var factory = await CurrentFactory(userId, ct);
        await using var tx = Db.Database.IsRelational() ? await Db.Database.BeginTransactionAsync(ct) : null;
        if (Db.Database.IsRelational())
            await Db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId} FOR UPDATE").LoadAsync(ct);
        var partner = await Db.FactoryDepotPartnerships.SingleOrDefaultAsync(x => x.FactoryId == factory.Id && x.DepotId == depotId, ct);
        if (partner is null) return ServiceResult<PartnerStatusResponse>.NotFound("Không tìm thấy đối tác.");
        await Db.Entry(partner).ReloadAsync(ct);
        if (status is "APPROVED" or "DECLINED")
        {
            if (partner.IsBlocked) return ServiceResult<PartnerStatusResponse>.Conflict("Quan hệ đang bị chặn; quyết định hợp tác không được mở chặn.");
            if (status == "DECLINED" && partner.ContractStatus == "ACTIVE")
                return ServiceResult<PartnerStatusResponse>.Conflict("Hợp đồng đang có hiệu lực; không thể đổi sang không hợp tác mà chưa thanh lý hợp đồng.");
            var inspected = await Db.InventoryBatches.AnyAsync(b => b.DepotId == depotId && b.TargetFactoryId == factory.Id &&
                b.QualityCheck != null && (b.Status == "VERIFIED" || b.Status == "REJECTED" || b.Status == "COMPLETED" || b.Status == "PAID"), ct);
            if (!inspected) return ServiceResult<PartnerStatusResponse>.Conflict("Cần hoàn tất kiểm tra chất lượng ít nhất một lô trước khi quyết định hợp tác.");
            partner.Status = status;
        }
        else partner.BlockedByFactory = status == "BLOCKED";
        partner.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);
        return ServiceResult<PartnerStatusResponse>.Success(data: new PartnerStatusResponse
        {
            Id = partner.Id, DepotId = partner.DepotId, Status = partner.EffectiveStatus,
            ContractCode = partner.ContractCode, ContractStatus = partner.ContractStatus
        });
    }

    public async Task<ServiceResult<PartnerStatusResponse>> ProposeContractAsync(Guid userId, Guid depotId, PartnershipContractRequest request, CancellationToken ct)
    {
        if (!request.ConfirmElectronicSignature)
            return ServiceResult<PartnerStatusResponse>.Invalid("Cần xác nhận ký điện tử trước khi gửi hợp đồng.");
        var terms = request.Terms.Trim();
        if (terms.Length is < 50 or > 10000)
            return ServiceResult<PartnerStatusResponse>.Invalid("Nội dung hợp đồng phải từ 50 đến 10.000 ký tự.");
        var factory = await CurrentFactory(userId, ct);
        await using var tx = Db.Database.IsRelational() ? await Db.Database.BeginTransactionAsync(ct) : null;
        if (Db.Database.IsRelational())
            await Db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId} FOR UPDATE").LoadAsync(ct);
        var partner = await Db.FactoryDepotPartnerships.SingleOrDefaultAsync(x => x.FactoryId == factory.Id && x.DepotId == depotId, ct);
        if (partner is null) return ServiceResult<PartnerStatusResponse>.NotFound("Không tìm thấy quan hệ với vựa.");
        await Db.Entry(partner).ReloadAsync(ct);
        if (partner.IsBlocked) return ServiceResult<PartnerStatusResponse>.Conflict("Quan hệ đang bị chặn; chưa thể lập hợp đồng.");
        var inspected = await Db.InventoryBatches.AnyAsync(b => b.DepotId == depotId && b.TargetFactoryId == factory.Id &&
            b.QualityCheck != null && (b.Status == "VERIFIED" || b.Status == "REJECTED" || b.Status == "COMPLETED" || b.Status == "PAID"), ct);
        if (!inspected) return ServiceResult<PartnerStatusResponse>.Conflict("Cần hoàn tất kiểm tra chất lượng ít nhất một lô trước khi đề nghị hợp đồng.");
        if (partner.ContractStatus == "ACTIVE") return ServiceResult<PartnerStatusResponse>.Conflict("Hợp đồng đang có hiệu lực; không thể ghi đè nội dung đã ký.");
        var now = DateTime.UtcNow;
        var version = partner.ContractVersion + 1;
        partner.ContractCode ??= $"HDHT-{now:yyyy}-{partner.Id.ToString("N")[..8].ToUpperInvariant()}";
        partner.ContractVersion = version;
        partner.ContractTerms = terms;
        partner.ContractTermsHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(terms))).ToLowerInvariant();
        partner.ContractStatus = "PENDING_DEPOT";
        partner.FactorySignedBy = userId;
        partner.FactorySignedAt = now;
        partner.DepotSignedBy = null;
        partner.DepotSignedAt = null;
        partner.ContractEffectiveAt = null;
        partner.ContractExpiresAt = now.AddMonths(request.ValidMonths);
        partner.Status = "PENDING";
        partner.UpdatedAt = now;
        await Db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);
        return ServiceResult<PartnerStatusResponse>.Success(data: new PartnerStatusResponse
        {
            Id = partner.Id, DepotId = partner.DepotId, Status = partner.EffectiveStatus,
            ContractCode = partner.ContractCode, ContractStatus = partner.ContractStatus
        }, message: "Đã ký và gửi hợp đồng điện tử cho chủ vựa xác nhận.");
    }

    public async Task<ServiceResult<PartnerStatusResponse>> RequestTerminationAsync(Guid userId, Guid depotId, ContractTerminationRequest request, CancellationToken ct)
    {
        var reason = request.Reason.Trim();
        if (reason.Length is < 10 or > 2000)
            return ServiceResult<PartnerStatusResponse>.Invalid("Lý do chấm dứt phải từ 10 đến 2.000 ký tự.");
        var factory = await CurrentFactory(userId, ct);
        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        await Db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId} FOR UPDATE").LoadAsync(ct);
        var partner = await Db.FactoryDepotPartnerships.SingleOrDefaultAsync(x => x.FactoryId == factory.Id && x.DepotId == depotId, ct);
        if (partner is null) return ServiceResult<PartnerStatusResponse>.NotFound("Không tìm thấy quan hệ với vựa.");
        await Db.Entry(partner).ReloadAsync(ct);
        if (partner.ContractStatus != "ACTIVE") return ServiceResult<PartnerStatusResponse>.Conflict("Chỉ hợp đồng đang hiệu lực mới được đề nghị chấm dứt.");
        if (partner.TerminationStatus is "PENDING_DEPOT" or "PENDING_FACTORY")
            return ServiceResult<PartnerStatusResponse>.Conflict("Hợp đồng đã có đề nghị chấm dứt đang chờ xử lý.");
        if (await HasOpenOrders(factory.Id, depotId, ct))
            return ServiceResult<PartnerStatusResponse>.Conflict("Cần hoàn tất các lô đang giao, KCS hoặc chờ quyết toán trước khi chấm dứt hợp đồng.");
        partner.TerminationStatus = "PENDING_DEPOT";
        partner.TerminationRequestedBy = userId;
        partner.TerminationRequestedAt = DateTime.UtcNow;
        partner.TerminationReason = reason;
        partner.TerminatedAt = null;
        partner.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<PartnerStatusResponse>.Success(data: StatusView(partner), message: "Đã gửi đề nghị chấm dứt cho chủ vựa xác nhận.");
    }

    public async Task<ServiceResult<PartnerStatusResponse>> DecideTerminationAsync(Guid userId, Guid depotId, ContractTerminationDecisionRequest request, CancellationToken ct)
    {
        var decision = request.Decision.Trim().ToUpperInvariant();
        if (decision is not ("ACCEPT" or "REJECT")) return ServiceResult<PartnerStatusResponse>.Invalid("Quyết định chỉ nhận ACCEPT hoặc REJECT.");
        var factory = await CurrentFactory(userId, ct);
        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        await Db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId} FOR UPDATE").LoadAsync(ct);
        var partner = await Db.FactoryDepotPartnerships.SingleOrDefaultAsync(x => x.FactoryId == factory.Id && x.DepotId == depotId, ct);
        if (partner is null) return ServiceResult<PartnerStatusResponse>.NotFound("Không tìm thấy quan hệ với vựa.");
        await Db.Entry(partner).ReloadAsync(ct);
        if (partner.TerminationStatus != "PENDING_FACTORY") return ServiceResult<PartnerStatusResponse>.Conflict("Không có đề nghị chấm dứt từ chủ vựa đang chờ nhà máy xử lý.");
        if (decision == "ACCEPT" && await HasOpenOrders(factory.Id, depotId, ct))
            return ServiceResult<PartnerStatusResponse>.Conflict("Cần hoàn tất các lô đang xử lý trước khi chấp thuận chấm dứt.");
        ApplyTerminationDecision(partner, decision);
        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<PartnerStatusResponse>.Success(data: StatusView(partner), message: decision == "ACCEPT" ? "Hợp đồng đã chấm dứt." : "Đã từ chối đề nghị chấm dứt.");
    }

    private Task<bool> HasOpenOrders(Guid factoryId, Guid depotId, CancellationToken ct) =>
        Db.InventoryBatches.AnyAsync(x => x.TargetFactoryId == factoryId && x.DepotId == depotId &&
            x.Status != "COMPLETED" && x.Status != "PAID" && x.Status != "REJECTED" && x.Status != "CANCELLED", ct);

    private static void ApplyTerminationDecision(FactoryDepotPartnership partner, string decision)
    {
        if (decision == "ACCEPT")
        {
            partner.TerminationStatus = "TERMINATED";
            partner.TerminatedAt = DateTime.UtcNow;
            partner.ContractStatus = "TERMINATED";
            partner.Status = "DECLINED";
        }
        else partner.TerminationStatus = "REJECTED";
        partner.UpdatedAt = DateTime.UtcNow;
    }

    private static PartnerStatusResponse StatusView(FactoryDepotPartnership partner) => new()
    {
        Id = partner.Id, DepotId = partner.DepotId, Status = partner.EffectiveStatus,
        ContractCode = partner.ContractCode, ContractStatus = partner.ContractStatus,
        TerminationStatus = partner.TerminationStatus
    };
    public async Task<ServiceResult<PartnerRatingResponse>> RateAsync(Guid userId, Guid orderId, RatingRequest request, CancellationToken ct)
    {
        if (request.BlockPartner) return ServiceResult<PartnerRatingResponse>.Invalid("Dùng chức năng chặn đối tác riêng để xác nhận thao tác.");
        if (request.PartnershipStatus == PartnershipStatus.APPROVED)
            return ServiceResult<PartnerRatingResponse>.Invalid("Đánh giá đã tách khỏi ký hợp đồng; hãy lập hợp đồng trong mục Vựa đối tác.");
        if (request.PartnershipStatus is not null and not PartnershipStatus.DECLINED)
            return ServiceResult<PartnerRatingResponse>.Invalid("Quyết định tại đây chỉ nhận DECLINED; hợp tác cần hợp đồng hai bên.");
        var factory = await CurrentFactory(userId, ct);
        await using var tx = Db.Database.IsRelational() ? await Db.Database.BeginTransactionAsync(ct) : null;
        var batch = await Db.InventoryBatches.SingleOrDefaultAsync(x => x.Id == orderId && x.TargetFactoryId == factory.Id, ct);
        if (batch is null) return ServiceResult<PartnerRatingResponse>.NotFound("Không tìm thấy đơn hàng.");
        if (batch.Status is not ("PAID" or "COMPLETED")) return ServiceResult<PartnerRatingResponse>.Conflict("Chỉ đánh giá sau khi đã quyết toán.");
        var review = await Db.FactoryDepotReviews.SingleOrDefaultAsync(x => x.BatchId == batch.Id && x.FactoryId == factory.Id, ct);
        if (review is null)
        {
            review = new FactoryDepotReview { BatchId = batch.Id, FactoryId = factory.Id, DepotId = batch.DepotId };
            Db.FactoryDepotReviews.Add(review);
        }
        review.Rating = request.Rating;
        review.Comment = request.Comment?.Trim();
        var partner = await Db.FactoryDepotPartnerships.SingleOrDefaultAsync(x => x.FactoryId == factory.Id && x.DepotId == batch.DepotId, ct);
        if (request.PartnershipStatus is not null)
        {
            if (partner is null)
                return ServiceResult<PartnerRatingResponse>.NotFound("Không tìm thấy quan hệ hợp tác với vựa.");
            if (partner.IsBlocked)
                return ServiceResult<PartnerRatingResponse>.Conflict("Quan hệ đang bị chặn; hãy xử lý trạng thái chặn trước.");
            partner.Status = request.PartnershipStatus.Value.ToString();
            partner.UpdatedAt = DateTime.UtcNow;
        }
        await Db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);
        return ServiceResult<PartnerRatingResponse>.Success(data: new PartnerRatingResponse
        {
            Id = review.Id,
            Rating = review.Rating,
            Comment = review.Comment,
            PartnershipStatus = request.PartnershipStatus?.ToString()
        }, message: request.PartnershipStatus is null
            ? "Đã lưu đánh giá đối tác."
            : "Đã lưu đánh giá và quyết định hợp tác.");
    }
}
