using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.Controllers.Shared;
using Retrack.API.DTOs.Factory;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/qc")]
[Authorize(Roles = "FACTORY")]
public class FactoryQCController(IQCService service) : FactoryControllerBase
{
    [HttpPost("orders/{id:guid}/weigh")]
    public async Task<IActionResult> Weigh(Guid id, WeighRequest request, CancellationToken ct)
        => this.ToActionResult(await service.WeighAsync(CurrentUserId, id, request, ct));

    [HttpPost("orders/{id:guid}/quality")]
    public async Task<IActionResult> Quality(Guid id, QualityRequest request, CancellationToken ct)
        => this.ToActionResult(await service.QualityAsync(CurrentUserId, id, request, ct));
}
