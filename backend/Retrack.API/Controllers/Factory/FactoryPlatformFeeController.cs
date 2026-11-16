using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Factory;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/platform-fees"), Authorize(Roles = "FACTORY")]
public sealed class FactoryPlatformFeeController(FactoryPlatformFeeService service) : FactoryControllerBase
{
    [HttpGet("transactions")]
    public async Task<IActionResult> Transactions([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        ValidatePage(page, pageSize);
        return Ok(ApiResponse<PagedResult<FactoryFeeEntry>>.Ok(await service.FeesAsync(CurrentUserId, page, pageSize, ct)));
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> Invoices([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        ValidatePage(page, pageSize);
        return Ok(ApiResponse<PagedResult<FeeInvoiceDto>>.Ok(await service.InvoicesAsync(CurrentUserId, page, pageSize, ct)));
    }

    [HttpPatch("invoices/{id:guid}/confirmation")]
    public async Task<IActionResult> Confirm(Guid id, PaymentProofDto proof, CancellationToken ct)
    {
        await service.ConfirmAsync(CurrentUserId, id, proof, ct);
        return Ok(ApiResponse<string>.Ok("Đã gửi chứng từ, chờ Admin đối soát."));
    }

    [HttpPost("invoices/{id:guid}/payos-link")]
    public async Task<IActionResult> CreateLink(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<PayOsLinkDto>.Ok(await service.CreatePayOsLinkAsync(CurrentUserId, id, ct)));

    [HttpPost("invoices/{id:guid}/payos-sync")]
    public async Task<IActionResult> Sync(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<string>.Ok(await service.SyncPayOsAsync(CurrentUserId, id, ct)));

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentException("Trang hoặc kích thước trang không hợp lệ.");
    }
}
