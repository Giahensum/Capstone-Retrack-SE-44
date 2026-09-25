using Retrack.API.DTOs.Factory;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Interfaces;

public interface IFactoryMarketService
{
    Task<ServiceResult<PageResponse<MarketBatchResponse>>> BatchesAsync(Guid userId, MarketQuery query, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<MarketPriceResponse>>> PricesAsync(CancellationToken ct);
    Task<ServiceResult<BatchAcceptedResponse>> AcceptAsync(Guid userId, Guid batchId, BatchOfferRequest request, CancellationToken ct);
    Task<ServiceResult> RejectOfferAsync(Guid userId, Guid batchId, DecisionRequest request, CancellationToken ct);
}
