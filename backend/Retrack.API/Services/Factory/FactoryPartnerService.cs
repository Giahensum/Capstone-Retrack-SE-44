using Microsoft.EntityFrameworkCore;
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
        var source = Db.FactoryDepotPartnerships.AsNoTracking().Where(x => x.FactoryId == factory.Id)
            .Include(x => x.Depot).ThenInclude(x => x.Owner).OrderByDescending(x => x.CreatedAt);
        var total = await source.CountAsync(ct);
        var partnerships = await source.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
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
            CompletedOrderCount = Db.InventoryBatches.Count(b => b.TargetFactoryId == x.FactoryId && b.DepotId == x.DepotId && b.Status == "COMPLETED")
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
            Id = partner.Id, DepotId = partner.DepotId, Status = partner.EffectiveStatus
        });
    }
    public async Task<ServiceResult<PartnerRatingResponse>> RateAsync(Guid userId, Guid orderId, RatingRequest request, CancellationToken ct)
    {
        if (request.BlockPartner) return ServiceResult<PartnerRatingResponse>.Invalid("Dùng chức năng chặn đối tác riêng để xác nhận thao tác.");
        var factory = await CurrentFactory(userId, ct);
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
        if (partner is not null)
        {
            if (request.BlockPartner) partner.BlockedByFactory = true;
            // Đánh giá không tự thay quyết định hợp tác sau kiểm tra.
            partner.UpdatedAt = DateTime.UtcNow;
        }
        await Db.SaveChangesAsync(ct);
        return ServiceResult<PartnerRatingResponse>.Success(data: new PartnerRatingResponse
        {
            Id = review.Id,
            Rating = review.Rating,
            Comment = review.Comment
        }, message: "Đã lưu đánh giá đối tác.");
    }
}
