using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers;

[ApiController, Authorize, Route("api/billing/model")]
public sealed class BillingModelController(IDepotSubscriptionService service) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get() => Ok(ApiResponse<bool>.Ok(await service.IsSubscriptionOnlyAsync()));
}
