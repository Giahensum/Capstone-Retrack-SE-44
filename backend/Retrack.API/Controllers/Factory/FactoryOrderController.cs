using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.Controllers.Shared;
using Retrack.API.DTOs.Factory;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/orders")]
[Authorize(Roles = "FACTORY")]
public class FactoryOrderController(IFactoryOrderService service) : FactoryControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] OrderQuery query, CancellationToken ct)
        => this.ToActionResult(await service.ListAsync(CurrentUserId, query, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => this.ToActionResult(await service.GetAsync(CurrentUserId, id, ct));

    [HttpPost("{id:guid}/receive")]
    public async Task<IActionResult> Receive(Guid id, CancellationToken ct)
        => this.ToActionResult(await service.ReceiveAsync(CurrentUserId, id, ct));

    [HttpPost("{id:guid}/settle")]
    public async Task<IActionResult> Settle(Guid id, SettlementRequest request, CancellationToken ct)
        => this.ToActionResult(await service.SettleAsync(CurrentUserId, id, request, ct));

    [HttpPut("{id:guid}/invoice")]
    public async Task<IActionResult> Invoice(Guid id, InvoiceRequest request, CancellationToken ct)
        => this.ToActionResult(await service.InvoiceAsync(CurrentUserId, id, request, ct));
}
