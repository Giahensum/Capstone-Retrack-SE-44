using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;
namespace Retrack.API.Controllers.Depot;

[ApiController, Route("api/depot/reports"), Authorize(Roles = "DEPOT_OWNER")]
public sealed class DepotReportController(IDepotReportService service) : ControllerBase
{
    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue([FromQuery] Guid depotId, [FromQuery] PeriodQuery period) =>
        Ok(ApiResponse<RevenueReportDto>.Ok(await service.RevenueAsync(OwnerId, depotId, period)));
    [HttpGet("staff")]
    public async Task<IActionResult> Staff([FromQuery] Guid depotId, [FromQuery] PeriodQuery period, [FromQuery] DepotQuery query) =>
        Ok(ApiResponse<PagedResult<StaffPerformanceDto>>.Ok(await service.PerformanceAsync(OwnerId, depotId, period, query)));
    [HttpGet("staff/{id:guid}")]
    public async Task<IActionResult> History(Guid id, [FromQuery] Guid depotId, [FromQuery] PeriodQuery period, [FromQuery] DepotQuery query) =>
        Ok(ApiResponse<PagedResult<StaffHistoryDto>>.Ok(await service.HistoryAsync(OwnerId, depotId, id, period, query)));
    [HttpGet("fees")]
    public async Task<IActionResult> Fees([FromQuery] Guid depotId, [FromQuery] PeriodQuery period, [FromQuery] DepotQuery query) =>
        Ok(ApiResponse<PagedResult<FeeEntryDto>>.Ok(await service.FeesAsync(OwnerId, depotId, period, query)));
    [HttpGet("fees/summary")]
    public async Task<IActionResult> FeeSummary([FromQuery] Guid depotId, [FromQuery] PeriodQuery period) =>
        Ok(ApiResponse<FeeSummaryDto>.Ok(await service.FeeSummaryAsync(OwnerId, depotId, period)));
    [HttpGet("invoices")]
    public async Task<IActionResult> Invoices([FromQuery] Guid depotId, [FromQuery] DepotQuery query) =>
        Ok(ApiResponse<PagedResult<FeeInvoiceDto>>.Ok(await service.InvoicesAsync(OwnerId, depotId, query)));
    [HttpPatch("invoices/{id:guid}/confirmation")]
    public async Task<IActionResult> Confirm(Guid id, [FromQuery] Guid depotId, PaymentProofDto dto)
    {
        await service.ConfirmInvoiceAsync(OwnerId, depotId, id, dto);
        return Ok(ApiResponse<string>.Ok("Đã gửi xác nhận, chờ Admin đối soát."));
    }
}
