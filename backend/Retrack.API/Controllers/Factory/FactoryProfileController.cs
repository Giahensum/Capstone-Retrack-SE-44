using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.Controllers.Shared;
using Retrack.API.DTOs.Factory;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/profile")]
[Authorize(Roles = "FACTORY")]
public class FactoryProfileController(IFactoryProfileService service) : FactoryControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => this.ToActionResult(await service.GetAsync(CurrentUserId, ct));

    [HttpPut]
    public async Task<IActionResult> Update(ProfileRequest request, CancellationToken ct)
        => this.ToActionResult(await service.UpdateAsync(CurrentUserId, request, ct));
}
