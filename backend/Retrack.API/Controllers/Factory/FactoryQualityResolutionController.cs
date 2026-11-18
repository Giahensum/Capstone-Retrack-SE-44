using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.Controllers.Shared;
using Retrack.API.DTOs.Factory;
using Retrack.API.Services.Factory;
namespace Retrack.API.Controllers.Factory;

[Authorize(Roles = "FACTORY"), Route("api/factory/orders")]
public sealed class FactoryQualityResolutionController(FactoryQualityResolutionService service) : FactoryControllerBase
{
    [HttpGet("{id:guid}/workflow-events")]
    public async Task<IActionResult> History(Guid id, [FromQuery] PageQuery query, CancellationToken ct) => this.ToActionResult(await service.HistoryAsync(CurrentUserId,id,query,ct));
    [HttpPost("{id:guid}/quality-resolution")]
    public async Task<IActionResult> Choose(Guid id, QualityResolutionRequest input, CancellationToken ct) => this.ToActionResult(await service.ChooseAsync(CurrentUserId, id, input, ct));
    [HttpPost("{id:guid}/return-handover")]
    public async Task<IActionResult> Handover(Guid id, ReturnHandoverRequest input, CancellationToken ct) => this.ToActionResult(await service.HandoverAsync(CurrentUserId, id, input, ct));
}
