using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Admin;

[ApiController, Authorize(Roles = "ADMIN"), Route("api/admin/subscriptions")]
public sealed class AdminSubscriptionController(IDepotSubscriptionService service) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("summary")] public async Task<IActionResult> Summary() => Ok(ApiResponse<SubscriptionAdminSummaryDto>.Ok(await service.AdminSummaryAsync(Actor)));
    [HttpPut("price")] public async Task<IActionResult> Price(SubscriptionPriceDto dto)
    { await service.SetPriceAsync(Actor, dto.MonthlyPrice); return Ok(ApiResponse<string>.Ok("Đã đổi giá cho đơn mua mới; đơn cũ giữ nguyên.")); }
    [HttpGet("orders")] public async Task<IActionResult> Orders(int page = 1, int pageSize = 20, string? status = null) => Ok(ApiResponse<PagedResult<SubscriptionOrderDto>>.Ok(await service.OrdersAsync(Actor, null, page, pageSize, status)));
    [HttpPost("orders/{id:guid}/approve")] public async Task<IActionResult> Approve(Guid id, SubscriptionApprovalDto dto)
    { await service.ApproveAsync(Actor, id, dto.Reference); return Ok(ApiResponse<string>.Ok("Đã ghi nhận tiền và gia hạn gói.")); }
    [HttpPost("orders/{id:guid}/payos-sync")] public async Task<IActionResult> Sync(Guid id) => Ok(ApiResponse<string>.Ok(await service.SyncAsync(Actor, Guid.Empty, id)));
    [HttpPost("orders/{id:guid}/reject-proof")] public async Task<IActionResult> Reject(Guid id, SubscriptionRejectionDto dto)
    { await service.RejectProofAsync(Actor, id, dto.Reason); return Ok(ApiResponse<string>.Ok("Đã yêu cầu bổ sung chứng từ, chưa kích hoạt gói.")); }
}
