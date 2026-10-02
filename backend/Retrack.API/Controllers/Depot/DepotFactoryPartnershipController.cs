using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;
namespace Retrack.API.Controllers.Depot;

[ApiController]
[Route("api/depot/partnerships")]
[Authorize(Roles = "DEPOT_OWNER")]
public class DepotFactoryPartnershipController(IDepotPartnershipService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? depotId, [FromQuery] DepotQuery query, CancellationToken ct) =>
        Ok(ApiResponse<PagedResult<DepotPartnershipDto>>.Ok(await service.ListAsync(GetUserId(), depotId, query, ct)));
    [HttpPut("{factoryId:guid}/status")]
    public async Task<IActionResult> Update(Guid factoryId, [FromBody] UpdatePartnershipRequest request, [FromQuery] Guid? depotId, CancellationToken ct) =>
        Ok(ApiResponse<DepotPartnershipStatusDto>.Ok(await service.UpdateAsync(GetUserId(), depotId, factoryId, request.Status, ct)));
    private Guid GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : throw new UnauthorizedAccessException("Token thiếu user id hợp lệ.");
    }
}

public record UpdatePartnershipRequest(string Status);
