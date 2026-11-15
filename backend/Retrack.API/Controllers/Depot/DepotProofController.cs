using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Depot;

[ApiController]
[Route("api/depot/proofs")]
[Authorize(Roles = "DEPOT_OWNER")]
public sealed class DepotProofController(IDepotProofService proofs) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromQuery] Guid depotId, [FromForm] DepotProofUpload request)
    {
        var ownerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var url = await proofs.UploadAsync(ownerId, depotId, request.File);
        return Ok(ApiResponse<object>.Ok(new { url }));
    }
}
