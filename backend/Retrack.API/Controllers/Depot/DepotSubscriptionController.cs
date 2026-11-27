using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Depot;

[ApiController, Authorize(Roles = "DEPOT_OWNER"), Route("api/depot/subscription")]
public sealed class DepotSubscriptionController(IDepotSubscriptionService service) : ControllerBase
{
    private Guid Owner => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet] public async Task<IActionResult> Get(Guid depotId) => Ok(ApiResponse<DepotSubscriptionDto>.Ok(await service.GetAsync(Owner, depotId)));
    [HttpGet("payment-account")] public async Task<IActionResult> Account(Guid depotId) => Ok(ApiResponse<PlatformPaymentAccountDto>.Ok(await service.PaymentAccountAsync(Owner, depotId)));
    [HttpPost("trial")] public async Task<IActionResult> Trial(Guid depotId) => Ok(ApiResponse<DepotSubscriptionDto>.Ok(await service.StartTrialAsync(Owner, depotId)));
    [HttpGet("orders")] public async Task<IActionResult> Orders(Guid depotId, int page = 1, int pageSize = 20) => Ok(ApiResponse<PagedResult<SubscriptionOrderDto>>.Ok(await service.OrdersAsync(Owner, depotId, page, pageSize)));
    [HttpPost("orders")] public async Task<IActionResult> Create(Guid depotId, CreateSubscriptionOrderDto dto) => Ok(ApiResponse<SubscriptionOrderDto>.Ok(await service.CreateOrderAsync(Owner, depotId, dto.OperationId)));
    [HttpPost("orders/{id:guid}/payos-link")] public async Task<IActionResult> Link(Guid depotId, Guid id) => Ok(ApiResponse<PayOsLinkDto>.Ok(await service.CreateLinkAsync(Owner, depotId, id)));
    [HttpPost("orders/{id:guid}/payos-sync")] public async Task<IActionResult> Sync(Guid depotId, Guid id) => Ok(ApiResponse<string>.Ok(await service.SyncAsync(Owner, depotId, id)));
    [HttpGet("refunds")] public async Task<IActionResult> Refunds(Guid depotId, int page = 1, int pageSize = 20) => Ok(ApiResponse<PagedResult<SubscriptionRefundDto>>.Ok(await service.RefundsAsync(Owner, depotId, page, pageSize)));
    [HttpPost("refunds")] public async Task<IActionResult> RequestRefund(Guid depotId, SubscriptionRefundRequestDto dto) =>
        Ok(ApiResponse<SubscriptionRefundDto>.Ok(await service.RequestRefundAsync(Owner, depotId, dto), "Đã gửi yêu cầu hoàn tiền; Admin sẽ đối soát và phản hồi."));
    [HttpPost("orders/{id:guid}/proof")] public async Task<IActionResult> Proof(Guid depotId, Guid id, SubscriptionProofDto dto)
    { await service.SubmitProofAsync(Owner, depotId, id, dto); return Ok(ApiResponse<string>.Ok("Đã gửi chứng từ; gói chỉ kích hoạt sau đối soát.")); }
}
