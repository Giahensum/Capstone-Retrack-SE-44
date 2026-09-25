using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.Controllers.Shared;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/dashboard")]
[Authorize(Roles = "FACTORY")]
public class FactoryDashboardController(IFactoryDashboardService service) : FactoryControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => this.ToActionResult(await service.GetAsync(CurrentUserId, ct));
}
