using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.Controllers.Shared;
using Retrack.API.DTOs.Factory;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/marketplace")]
[Authorize(Roles = "FACTORY")]
public class FactoryMarketController(IFactoryMarketService service) : FactoryControllerBase
{
    [HttpGet("batches")]
    public async Task<IActionResult> Batches([FromQuery] MarketQuery query, CancellationToken ct)
        => this.ToActionResult(await service.BatchesAsync(CurrentUserId, query, ct));

    [HttpGet("prices")]
    public async Task<IActionResult> Prices(CancellationToken ct)
        => this.ToActionResult(await service.PricesAsync(ct));

    [HttpPost("batches/{batchId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid batchId, BatchOfferRequest request, CancellationToken ct)
        => this.ToActionResult(await service.AcceptAsync(CurrentUserId, batchId, request, ct), id => $"/api/factory/orders/{id}");

    [HttpPost("offers/{batchId:guid}/reject")]
    public async Task<IActionResult> RejectOffer(Guid batchId, [FromBody] DecisionRequest request, CancellationToken ct)
        => this.ToActionResult(await service.RejectOfferAsync(CurrentUserId, batchId, request, ct));
}
