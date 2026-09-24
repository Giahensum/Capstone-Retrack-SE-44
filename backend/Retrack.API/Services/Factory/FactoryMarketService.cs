using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Factory;

public class FactoryMarketService(AppDbContext db) : FactoryServiceBase(db), IFactoryMarketService
{
    public async Task<ServiceResult<PageResponse<MarketBatchResponse>>> BatchesAsync(Guid userId, MarketQuery query, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        var accepted = factory.AcceptedMaterialsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var source = Db.InventoryBatches.AsNoTracking()
            .Where(x => ((x.Status == "MARKETPLACE" || x.Status == "LISTED" || x.Status == "DRAFT") && x.TargetFactoryId == null) ||
                (x.DirectOfferFactoryId == factory.Id && (x.Status == "PENDING_FACTORY" || x.Status == "READY_FOR_PICKUP")))
            .Include(x => x.Depot).ThenInclude(x => x.Owner).AsQueryable();
        if (query.Material.HasValue) source = source.Where(x => x.MaterialType == query.Material.Value.ToString());
        if (query.MinWeightKg.HasValue) source = source.Where(x => x.DeclaredWeightKg >= query.MinWeightKg.Value);
        if (query.MaxWeightKg.HasValue) source = source.Where(x => x.DeclaredWeightKg <= query.MaxWeightKg.Value);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(x => x.Description != null && x.Description.Contains(query.Search));
        if (query.DirectOnly) source = source.Where(x => x.DirectOfferFactoryId == factory.Id);
        else source = source.Where(x => x.TargetFactoryId == null && x.DirectOfferFactoryId == null);
        var all = await source.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var matches = all.Where(x => accepted.Length == 0 || accepted.Contains(x.MaterialType))
            .Where(x => factory.CapacityKgPerMonth <= 0 || x.DeclaredWeightKg <= factory.CapacityKgPerMonth).ToList();
        var total = matches.Count;
        var items = matches.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(x => new MarketBatchResponse
        {
            Id = x.Id,
            BatchCode = x.Id.ToString("N")[..8].ToUpperInvariant(),
            MaterialType = x.MaterialType,
            EstimatedWeightKg = x.DeclaredWeightKg,
            UnitPrice = (decimal?)null,
            Description = x.Description,
            ThumbnailImageUrl = (string?)null,
            ImageUrls = Array.Empty<string>(),
            CreatedAt = x.CreatedAt,
            Depot = new MarketDepotResponse
            {
                Id = x.Depot.Id,
                CompanyName = x.Depot.Name,
                Address = x.Depot.Address,
                ContactPhone = x.Depot.Owner?.Phone,
                Latitude = x.Depot.Latitude,
                Longitude = x.Depot.Longitude
            },
            IsDirectOffer = x.DirectOfferFactoryId == factory.Id
        });
        return ServiceResult<PageResponse<MarketBatchResponse>>.Success(data: new PageResponse<MarketBatchResponse>
        {
            Items = items.ToList(),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling((double)total / query.PageSize)
        });
    }

    public async Task<ServiceResult<IReadOnlyList<MarketPriceResponse>>> PricesAsync(CancellationToken ct)
    {
        var rows = await Db.MarketPrices.AsNoTracking().OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CreatedAt).ToListAsync(ct);
        var prices = rows.GroupBy(x => x.MaterialType).Select(x => x.First()).OrderBy(x => x.MaterialType);
        return ServiceResult<IReadOnlyList<MarketPriceResponse>>.Success(data: prices.Select(x => new MarketPriceResponse
        {
            MaterialType = x.MaterialType.ToString(),
            PricePerKg = x.PricePerKg,
            EffectiveDate = x.EffectiveDate,
            Source = x.Source
        }).ToList());
    }

    public async Task<ServiceResult<BatchAcceptedResponse>> AcceptAsync(Guid userId, Guid batchId, BatchOfferRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        var batch = await Db.InventoryBatches.Include(x => x.Depot)
            .SingleOrDefaultAsync(x => x.Id == batchId, ct);
        if (batch is null) return ServiceResult<BatchAcceptedResponse>.NotFound("Không tìm thấy lô hàng.");
        var availableOnMarketplace = (batch.Status is "MARKETPLACE" or "LISTED" or "DRAFT") && batch.TargetFactoryId is null;
        var directOfferForFactory = (batch.Status is "PENDING_FACTORY" or "READY_FOR_PICKUP") && batch.DirectOfferFactoryId == factory.Id;
        if (!availableOnMarketplace && !directOfferForFactory)
            return ServiceResult<BatchAcceptedResponse>.Conflict("Lô hàng không còn khả dụng cho nhà máy này.");
        var accepted = factory.AcceptedMaterialsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if ((accepted.Length > 0 && !accepted.Contains(batch.MaterialType)) || (factory.CapacityKgPerMonth > 0 && batch.DeclaredWeightKg > factory.CapacityKgPerMonth))
            return ServiceResult<BatchAcceptedResponse>.Invalid("Vật liệu hoặc khối lượng lô không phù hợp năng lực nhà máy.");
        var partnership = await Db.FactoryDepotPartnerships.SingleOrDefaultAsync(x => x.FactoryId == factory.Id && x.DepotId == batch.DepotId, ct);
        if (partnership?.Status == "BLOCKED") return ServiceResult<BatchAcceptedResponse>.Conflict("Đối tác này đang bị chặn.");
        if (partnership is null)
        {
            partnership = new FactoryDepotPartnership { FactoryId = factory.Id, DepotId = batch.DepotId, Status = "PENDING" };
            Db.FactoryDepotPartnerships.Add(partnership);
        }
        await Db.SaveChangesAsync(ct);
        if (!directOfferForFactory && partnership?.Status != "APPROVED")
            return ServiceResult<BatchAcceptedResponse>.Conflict("Vựa cần duyệt quan hệ đối tác trước khi nhà máy nhận lô.");
        if (directOfferForFactory && partnership?.Status != "APPROVED")
            return ServiceResult<BatchAcceptedResponse>.Conflict("Quan hệ đối tác chưa được vựa duyệt.");
        if (directOfferForFactory) batch.DirectOfferFactoryId = null;
        batch.TargetFactoryId = factory.Id;
        batch.Status = "ACCEPTED";
        if (batch.TransportJob is null)
            Db.TransportJobs.Add(new TransportJob { BatchId = batch.Id, Status = "PENDING" });
        await Db.SaveChangesAsync(ct);
        return ServiceResult<BatchAcceptedResponse>.Success(data: new BatchAcceptedResponse
        {
            Id = batch.Id,
            BatchId = batch.Id,
            Status = "ACCEPTED",
            AgreedPrice = request.AgreedPricePerKg
        }, message: "Đã nhận lô hàng.", resourceId: batch.Id);
    }

    public async Task<ServiceResult> RejectOfferAsync(Guid userId, Guid batchId, DecisionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) return ServiceResult.Invalid("Nhập lý do từ chối.");
        var factory = await CurrentFactory(userId, ct);
        var batch = await Db.InventoryBatches.SingleOrDefaultAsync(x => x.Id == batchId, ct);
        if (batch is null) return ServiceResult.NotFound("Không tìm thấy lời mời hợp tác.");
        var directOfferForFactory = (batch.Status is "PENDING_FACTORY" or "READY_FOR_PICKUP") && batch.DirectOfferFactoryId == factory.Id;
        var marketplaceOffer = (batch.Status is "MARKETPLACE" or "LISTED" or "DRAFT") && batch.TargetFactoryId is null;
        if (!directOfferForFactory && !marketplaceOffer)
            return ServiceResult.Conflict("Lời mời không còn khả dụng.");
        batch.Status = "REJECTED";
        batch.RejectionReason = request.Reason.Trim();
        batch.DirectOfferFactoryId = null;
        await Db.SaveChangesAsync(ct);
        return ServiceResult.Success(message: "Đã từ chối lời mời.");
    }
}
