using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.Controllers.Shared;
using Retrack.API.DTOs.Factory;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/partners")]
[Authorize(Roles = "FACTORY")]
public class FactoryPartnerController(IFactoryPartnerService service) : FactoryControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery query, CancellationToken ct)
        => this.ToActionResult(await service.ListAsync(CurrentUserId, query, ct));

    [HttpPut("{depotId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid depotId, [FromBody] PartnerStatusRequest request, CancellationToken ct)
        => this.ToActionResult(await service.UpdateStatusAsync(CurrentUserId, depotId, request, ct));

    [HttpPost("{depotId:guid}/contract")]
    public async Task<IActionResult> ProposeContract(Guid depotId, PartnershipContractRequest request, CancellationToken ct)
        => this.ToActionResult(await service.ProposeContractAsync(CurrentUserId, depotId, request, ct));

    [HttpPost("{depotId:guid}/contract/termination")]
    public async Task<IActionResult> RequestTermination(Guid depotId, ContractTerminationRequest request, CancellationToken ct)
        => this.ToActionResult(await service.RequestTerminationAsync(CurrentUserId, depotId, request, ct));

    [HttpPost("{depotId:guid}/contract/termination/decision")]
    public async Task<IActionResult> DecideTermination(Guid depotId, ContractTerminationDecisionRequest request, CancellationToken ct)
        => this.ToActionResult(await service.DecideTerminationAsync(CurrentUserId, depotId, request, ct));

    [HttpPost("orders/{orderId:guid}/rating")]
    public async Task<IActionResult> Rate(Guid orderId, RatingRequest request, CancellationToken ct)
        => this.ToActionResult(await service.RateAsync(CurrentUserId, orderId, request, ct));
}
