using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Money;

namespace Retrack.API.Controllers.Money;

/// <summary>
/// Thu tiền vào sàn qua PayOS. {flow} ∈ PICKUP, BATCH, LIQUIDATION_DEPOSIT, LIQUIDATION_BALANCE; {sourceId} là đơn thu gom / lô / thỏa thuận thanh lý.
/// Người trả được suy ra từ dòng nguồn và JWT, không bao giờ từ body.
/// </summary>
[ApiController, Route("api/money/collections"), Authorize(Roles = "DEPOT_OWNER,FACTORY,SELLER")]
public sealed class MoneyCollectionsController(IMoneyCollectionService service) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("{flow}/{sourceId:guid}")]
    public async Task<IActionResult> Get(string flow, Guid sourceId, CancellationToken ct) =>
        Ok(ApiResponse<MoneyCollectionDto>.Ok(await service.GetAsync(UserId, flow.ToUpperInvariant(), sourceId, ct)));

    /// <summary>Chỉ người trả. 409 nếu money.mode không phải PLATFORM hoặc PayOS chưa cấu hình.</summary>
    [HttpPost("{flow}/{sourceId:guid}/payos-link")]
    public async Task<IActionResult> CreateLink(string flow, Guid sourceId, CancellationToken ct) =>
        Ok(ApiResponse<PayOsLinkDto>.Ok(await service.CreateLinkAsync(UserId, flow.ToUpperInvariant(), sourceId, ct)));

    /// <summary>Gọi sau khi người trả quay về từ PayOS; chỉ tin số liệu PayOS trả về, không tin tham số trình duyệt.</summary>
    [HttpPost("{flow}/{sourceId:guid}/payos-sync")]
    public async Task<IActionResult> Sync(string flow, Guid sourceId, CancellationToken ct) =>
        Ok(ApiResponse<MoneyCollectionDto>.Ok(await service.SyncAsync(UserId, flow.ToUpperInvariant(), sourceId, ct)));
}
