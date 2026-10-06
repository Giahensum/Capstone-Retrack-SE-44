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
        var accepted = factory.AcceptedMaterialsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(MaterialCatalog.Normalize).ToArray();
        var source = Db.InventoryBatches.AsNoTracking()
            .Where(x => ((x.Status == "MARKETPLACE" || x.Status == "LISTED" || x.Status == "DRAFT") && x.TargetFactoryId == null && x.DirectOfferFactoryId == null) ||
                (x.DirectOfferFactoryId == factory.Id && (x.Status == "PENDING_FACTORY" || x.Status == "READY_FOR_PICKUP" || x.Status == "PENDING_APPROVAL")) ||
                (x.TargetFactoryId == factory.Id && x.Status == "PENDING_APPROVAL"))
            .Include(x => x.Depot).ThenInclude(x => x.Owner).AsQueryable();
        if (query.Material.HasValue) { var values = MaterialCatalog.Values(query.Material.Value.ToString()); source = source.Where(x => values.Contains(x.MaterialType)); }
        if (query.MinWeightKg.HasValue) source = source.Where(x => x.DeclaredWeightKg >= query.MinWeightKg.Value);
        if (query.MaxWeightKg.HasValue) source = source.Where(x => x.DeclaredWeightKg <= query.MaxWeightKg.Value);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(x => x.Description != null && x.Description.Contains(query.Search));
        if (query.DirectOnly) source = source.Where(x => x.DirectOfferFactoryId == factory.Id || (x.TargetFactoryId == factory.Id && x.Status == "PENDING_APPROVAL"));
        else source = source.Where(x => x.TargetFactoryId == null && x.DirectOfferFactoryId == null);
        if (accepted.Length > 0)
        {
            var materialValues = accepted.SelectMany(MaterialCatalog.Values).Distinct().ToArray();
            source = source.Where(x => materialValues.Contains(x.MaterialType));
        }
        if (factory.CapacityKgPerMonth > 0) source = source.Where(x => x.DeclaredWeightKg <= factory.CapacityKgPerMonth);
        var total = await source.CountAsync(ct);
        var rows = await source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        var items = rows.Select(x => new MarketBatchResponse
        {
            Id = x.Id,
            BatchCode = x.Code ?? x.Id.ToString("N")[..8].ToUpperInvariant(),
            MaterialType = MaterialCatalog.Normalize(x.MaterialType),
            EstimatedWeightKg = x.DeclaredWeightKg,
            UnitPrice = (decimal?)null,
            Description = x.Description,
            ThumbnailImageUrl = x.ImageUrls.FirstOrDefault(),
            ImageUrls = x.ImageUrls,
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
            IsDirectOffer = x.DirectOfferFactoryId == factory.Id || (x.TargetFactoryId == factory.Id && x.Status == "PENDING_APPROVAL")
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
        await using var transaction = Db.Database.IsRelational() ? await Db.Database.BeginTransactionAsync(ct) : null;
        var depotId = await Db.InventoryBatches.AsNoTracking().Where(x => x.Id == batchId).Select(x => (Guid?)x.DepotId).SingleOrDefaultAsync(ct);
        if (depotId == null) return ServiceResult<BatchAcceptedResponse>.NotFound("Không tìm thấy lô hàng.");
        if (Db.Database.IsRelational())
        {
            // Cùng thứ tự khóa với Depot: kho trước, lô sau.
            await Db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId.Value} FOR UPDATE").LoadAsync(ct);
            await Db.InventoryBatches.FromSqlInterpolated($"SELECT * FROM inventory_batches WHERE id = {batchId} FOR UPDATE").LoadAsync(ct);
        }
        var batch = await Db.InventoryBatches.Include(x => x.Depot).Include(x => x.TransportJob)
            .SingleOrDefaultAsync(x => x.Id == batchId, ct);
        if (batch is null) return ServiceResult<BatchAcceptedResponse>.NotFound("Không tìm thấy lô hàng.");
        await Db.Entry(batch).ReloadAsync(ct);
        var availableOnMarketplace = (batch.Status is "MARKETPLACE" or "LISTED" or "DRAFT") && batch.TargetFactoryId is null;
        var directOfferForFactory = ((batch.Status is "PENDING_FACTORY" or "READY_FOR_PICKUP" or "PENDING_APPROVAL") && batch.DirectOfferFactoryId == factory.Id) ||
            (batch.Status == "PENDING_APPROVAL" && batch.TargetFactoryId == factory.Id);
        if (!availableOnMarketplace && !directOfferForFactory)
            return ServiceResult<BatchAcceptedResponse>.Conflict("Lô hàng không còn khả dụng cho nhà máy này.");
        var accepted = factory.AcceptedMaterialsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(MaterialCatalog.Normalize).ToArray();
        if ((accepted.Length > 0 && !accepted.Contains(MaterialCatalog.Normalize(batch.MaterialType))) || (factory.CapacityKgPerMonth > 0 && batch.DeclaredWeightKg > factory.CapacityKgPerMonth))
            return ServiceResult<BatchAcceptedResponse>.Invalid("Vật liệu hoặc khối lượng lô không phù hợp năng lực nhà máy.");
        var partnership = await Db.FactoryDepotPartnerships.AsNoTracking().SingleOrDefaultAsync(x => x.FactoryId == factory.Id && x.DepotId == batch.DepotId, ct);
        if (partnership?.IsBlocked == true) return ServiceResult<BatchAcceptedResponse>.Conflict("Đối tác này đang bị chặn.");
        if (partnership is null)
        {
            partnership = new FactoryDepotPartnership { FactoryId = factory.Id, DepotId = batch.DepotId, Status = "PENDING" };
            Db.FactoryDepotPartnerships.Add(partnership);
        }
        await Db.SaveChangesAsync(ct);
        // Nhận lô đầu không tự thiết lập quan hệ hợp tác lâu dài.
        if (directOfferForFactory) batch.DirectOfferFactoryId = null;
        batch.TargetFactoryId = factory.Id;
        batch.Status = "ACCEPTED";
        if (batch.TransportJob is null)
        {
            var job = new TransportJob { BatchId = batch.Id, Status = "PENDING" };
            Db.TransportJobs.Add(job);
            await Retrack.API.Services.Driver.DriverJobNotices.QueueAsync(Db, batch.DepotId, job.Id, ct);
        }
        await Db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
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
        await using var transaction = Db.Database.IsRelational() ? await Db.Database.BeginTransactionAsync(ct) : null;
        var depotId = await Db.InventoryBatches.AsNoTracking().Where(x => x.Id == batchId).Select(x => (Guid?)x.DepotId).SingleOrDefaultAsync(ct);
        if (depotId == null) return ServiceResult.NotFound("Không tìm thấy lời mời hợp tác.");
        if (Db.Database.IsRelational())
        {
            await Db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId.Value} FOR UPDATE").LoadAsync(ct);
            await Db.InventoryBatches.FromSqlInterpolated($"SELECT * FROM inventory_batches WHERE id = {batchId} FOR UPDATE").LoadAsync(ct);
        }
        var batch = await Db.InventoryBatches.SingleOrDefaultAsync(x => x.Id == batchId, ct);
        if (batch is null) return ServiceResult.NotFound("Không tìm thấy lời mời hợp tác.");
        await Db.Entry(batch).ReloadAsync(ct);
        var directOfferForFactory = ((batch.Status is "PENDING_FACTORY" or "READY_FOR_PICKUP" or "PENDING_APPROVAL") && batch.DirectOfferFactoryId == factory.Id) ||
            (batch.Status == "PENDING_APPROVAL" && batch.TargetFactoryId == factory.Id);
        // Không cho một nhà máy gỡ lô công khai của chủ kho khỏi chợ.
        if (!directOfferForFactory)
            return ServiceResult.Conflict("Lời mời không còn khả dụng.");
        batch.Status = "REJECTED";
        batch.RejectionReason = request.Reason.Trim();
        batch.DirectOfferFactoryId = null;
        await Db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        return ServiceResult.Success(message: "Đã từ chối lời mời.");
    }
}
