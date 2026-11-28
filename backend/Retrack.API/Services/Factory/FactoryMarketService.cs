using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Shared;
using Retrack.API.Repositories;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Services.Factory;

public class FactoryMarketService(AppDbContext db, IFactoryMarketRepository? reads = null,
    IFactoryOrderRepository? orders = null, IFactoryUnitOfWork? work = null) : FactoryServiceBase(db, orders, work), IFactoryMarketService
{
    private readonly IFactoryMarketRepository marketReads = reads ?? new FactoryMarketRepository(db);
    public async Task<ServiceResult<PageResponse<MarketBatchResponse>>> BatchesAsync(Guid userId, MarketQuery query, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        var accepted = factory.AcceptedMaterialsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(MaterialCatalog.Normalize).ToArray();
        var materialValues = query.Material.HasValue ? MaterialCatalog.Values(query.Material.Value.ToString()) : null;
        var acceptedValues = accepted.SelectMany(MaterialCatalog.Values).Distinct().ToArray();
        var page = await marketReads.BatchesAsync(factory.Id, query, materialValues, acceptedValues,
            string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(), factory.CapacityKgPerMonth, ct);
        var items = page.Items.Select(x => new MarketBatchResponse
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
            TotalCount = page.TotalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling((double)page.TotalCount / query.PageSize)
        });
    }

    public async Task<ServiceResult<IReadOnlyList<MarketPriceResponse>>> PricesAsync(CancellationToken ct)
    {
        var rows = await marketReads.PricesAsync(ct);
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
        await using var transaction = await Work.BeginAsync(ct);
        var batch = await OrderRepository.LockMarketAsync(batchId, includeTransport: true, ct);
        if (batch is null) return ServiceResult<BatchAcceptedResponse>.NotFound("Không tìm thấy lô hàng.");
        var availableOnMarketplace = (batch.Status is "MARKETPLACE" or "LISTED" or "DRAFT") && batch.TargetFactoryId is null;
        var directOfferForFactory = ((batch.Status is "PENDING_FACTORY" or "READY_FOR_PICKUP" or "PENDING_APPROVAL") && batch.DirectOfferFactoryId == factory.Id) ||
            (batch.Status == "PENDING_APPROVAL" && batch.TargetFactoryId == factory.Id);
        if (!availableOnMarketplace && !directOfferForFactory)
            return ServiceResult<BatchAcceptedResponse>.Conflict("Lô hàng không còn khả dụng cho nhà máy này.");
        var accepted = factory.AcceptedMaterialsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(MaterialCatalog.Normalize).ToArray();
        if ((accepted.Length > 0 && !accepted.Contains(MaterialCatalog.Normalize(batch.MaterialType))) || (factory.CapacityKgPerMonth > 0 && batch.DeclaredWeightKg > factory.CapacityKgPerMonth))
            return ServiceResult<BatchAcceptedResponse>.Invalid("Vật liệu hoặc khối lượng lô không phù hợp năng lực nhà máy.");
        var partnership = await Workflow.PartnershipAsync(factory.Id, batch.DepotId, ct);
        if (partnership?.IsBlocked == true) return ServiceResult<BatchAcceptedResponse>.Conflict("Đối tác này đang bị chặn.");
        if (partnership is null)
        {
            partnership = new FactoryDepotPartnership { FactoryId = factory.Id, DepotId = batch.DepotId, Status = "PENDING" };
            Workflow.AddPartnership(partnership);
        }
        await Work.SaveAsync(ct);
        // Nhận lô đầu không tự thiết lập quan hệ hợp tác lâu dài.
        if (directOfferForFactory) batch.DirectOfferFactoryId = null;
        batch.TargetFactoryId = factory.Id;
        batch.Status = "ACCEPTED";
        await NotifyDepotAsync(batch.DepotId, "Nhà máy đã nhận lô", $"{factory.Name} đã nhận lô {batch.Code ?? batch.Id.ToString()}. Kho có thể theo dõi bước vận chuyển.", batch.Id, ct: ct);
        if (batch.TransportJob is null)
        {
            var job = new TransportJob { BatchId = batch.Id, Status = batch.ShippingMethod == "EXTERNAL" ? "EXTERNAL_WAITING" : "PENDING" };
            Workflow.AddTransport(job);
            if (batch.ShippingMethod == "INTERNAL") await Retrack.API.Services.Driver.DriverJobNotices.QueueAsync(new Retrack.API.Repositories.DriverDeliveryRepository(Db), batch.DepotId, job.Id, ct);
        }
        await Work.SaveAsync(ct);
        await transaction.CommitAsync(ct);
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
        await using var transaction = await Work.BeginAsync(ct);
        var batch = await OrderRepository.LockMarketAsync(batchId, includeTransport: false, ct);
        if (batch is null) return ServiceResult.NotFound("Không tìm thấy lời mời hợp tác.");
        var directOfferForFactory = ((batch.Status is "PENDING_FACTORY" or "READY_FOR_PICKUP" or "PENDING_APPROVAL") && batch.DirectOfferFactoryId == factory.Id) ||
            (batch.Status == "PENDING_APPROVAL" && batch.TargetFactoryId == factory.Id);
        // Không cho một nhà máy gỡ lô công khai của chủ kho khỏi chợ.
        if (!directOfferForFactory)
            return ServiceResult.Conflict("Lời mời không còn khả dụng.");
        batch.Status = "REJECTED";
        batch.RejectionReason = request.Reason.Trim();
        batch.DirectOfferFactoryId = null;
        await NotifyDepotAsync(batch.DepotId, "Nhà máy từ chối nhận lô", $"{factory.Name} từ chối lô {batch.Code ?? batch.Id.ToString()}: {batch.RejectionReason}", batch.Id, ct: ct);
        await Work.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult.Success(message: "Đã từ chối lời mời.");
    }
}
