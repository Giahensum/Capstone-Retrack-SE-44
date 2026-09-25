using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.Controllers.Shared;
using Retrack.API.DTOs.Factory;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/demands")]
[Authorize(Roles = "FACTORY")]
public class FactoryDemandController(IFactoryDemandService service) : FactoryControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery query, CancellationToken ct)
        => this.ToActionResult(await service.ListAsync(CurrentUserId, query, ct));

    [HttpPost]
    public async Task<IActionResult> Create(DemandRequest request, CancellationToken ct)
        => this.ToActionResult(await service.CreateAsync(CurrentUserId, request, ct), id => $"/api/factory/demands/{id}");

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, DemandRequest request, CancellationToken ct)
        => this.ToActionResult(await service.UpdateAsync(CurrentUserId, id, request, ct));

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> Toggle(Guid id, [FromBody] DemandStatusRequest request, CancellationToken ct)
        => this.ToActionResult(await service.ToggleAsync(CurrentUserId, id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => this.ToActionResult(await service.DeleteAsync(CurrentUserId, id, ct));
}
