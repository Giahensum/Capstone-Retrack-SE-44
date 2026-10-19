using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Depot;

[ApiController, Route("api/depot/stock"), Authorize(Roles = "DEPOT_OWNER,DEPOT_EMPLOYEE")]
public class DepotStockController(IDepotStockService service) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [Authorize(Roles = "DEPOT_OWNER"), HttpGet("receipts/{id:guid}/adjustments")]
    public async Task<IActionResult> Adjustments(Guid depotId, Guid id, [FromQuery] DepotQuery query) =>
        Ok(ApiResponse<PagedResult<StockAdjustmentDto>>.Ok(await service.AdjustmentsAsync(UserId, depotId, id, query)));
    [Authorize(Roles = "DEPOT_OWNER"), HttpGet("receipts/{id:guid}/cost-balance")]
    public async Task<IActionResult> Balance(Guid depotId, Guid id) =>
        Ok(ApiResponse<List<StockLineCostBalanceDto>>.Ok(await service.CurrentReceiptCostsAsync(UserId, depotId, id)));
    [Authorize(Roles = "DEPOT_OWNER"), HttpPost("receipts/{id:guid}/adjustments")]
    public async Task<IActionResult> AddAdjustment(Guid depotId, Guid id, CreateStockAdjustmentDto dto)
    {
        await service.AddAdjustmentAsync(UserId, depotId, id, dto); return Ok(ApiResponse<string>.Ok("Đã ghi khoản điều chỉnh, giữ nguyên lịch sử đã xuất."));
    }
    [HttpGet("receipts")]
    public async Task<IActionResult> List(Guid depotId, [FromQuery] DepotQuery query) =>
        Ok(ApiResponse<PagedResult<StockReceiptDto>>.Ok(await service.ListAsync(UserId, depotId, query)));
    [HttpPost("receipts")]
    public async Task<IActionResult> Create(Guid depotId, CreateStockReceiptDto dto) =>
        Ok(ApiResponse<StockReceiptDto>.Ok(await service.CreateAsync(UserId, depotId, dto)));
    [HttpPatch("receipts/{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid depotId, Guid id) =>
        Ok(ApiResponse<StockReceiptDto>.Ok(await service.ReviewAsync(UserId, depotId, id, "SUBMITTED")));
    [Authorize(Roles = "DEPOT_OWNER"), HttpPut("receipts/{id:guid}/costs")]
    public async Task<IActionResult> UpdateCosts(Guid depotId, Guid id, UpdateStockCostsDto dto) =>
        Ok(ApiResponse<StockReceiptDto>.Ok(await service.UpdateCostsAsync(UserId, depotId, id, dto)));
    [Authorize(Roles = "DEPOT_OWNER"), HttpPatch("receipts/{id:guid}/post")]
    public async Task<IActionResult> Post(Guid depotId, Guid id) =>
        Ok(ApiResponse<StockReceiptDto>.Ok(await service.ReviewAsync(UserId, depotId, id, "POSTED")));
    [Authorize(Roles = "DEPOT_OWNER"), HttpPatch("receipts/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid depotId, Guid id, ReviewStockReceiptDto dto) =>
        Ok(ApiResponse<StockReceiptDto>.Ok(await service.ReviewAsync(UserId, depotId, id, "REJECTED", dto.Reason)));
    [Authorize(Roles = "DEPOT_OWNER"), HttpPatch("receipts/{id:guid}/void")]
    public async Task<IActionResult> Void(Guid depotId, Guid id, ReviewStockReceiptDto dto) =>
        Ok(ApiResponse<StockReceiptDto>.Ok(await service.ReviewAsync(UserId, depotId, id, "VOIDED", dto.Reason)));
    [Authorize(Roles = "DEPOT_OWNER"), HttpGet("reusable")]
    public async Task<IActionResult> Reusable(Guid depotId, [FromQuery] DepotQuery query) =>
        Ok(ApiResponse<PagedResult<ReusableStockDto>>.Ok(await service.ReusableAsync(UserId, depotId, query)));
    [Authorize(Roles = "DEPOT_OWNER"), HttpPost("reusable/{id:guid}/sales")]
    public async Task<IActionResult> Sell(Guid depotId, Guid id, CreateReuseSaleDto dto) =>
        Ok(ApiResponse<ReuseSaleDto>.Ok(await service.SellAsync(UserId, depotId, id, dto)));
    [Authorize(Roles = "DEPOT_OWNER"), HttpGet("sales")]
    public async Task<IActionResult> Sales(Guid depotId, [FromQuery] DepotQuery query) =>
        Ok(ApiResponse<PagedResult<ReuseSaleDto>>.Ok(await service.SalesAsync(UserId, depotId, query)));
    [Authorize(Roles = "DEPOT_OWNER"), HttpGet("batches/{id:guid}/costs")]
    public async Task<IActionResult> Costs(Guid depotId, Guid id) =>
        Ok(ApiResponse<BatchCostDto>.Ok(await service.BatchCostsAsync(UserId, depotId, id)));
    [Authorize(Roles = "DEPOT_OWNER"), HttpPost("batches/{id:guid}/expenses")]
    public async Task<IActionResult> Expense(Guid depotId, Guid id, CreateBatchExpenseDto dto)
    {
        await service.AddBatchExpenseAsync(UserId, depotId, id, dto);
        return Ok(ApiResponse<string>.Ok("Đã ghi chi phí; báo cáo được cập nhật theo các khoản đã ghi nhận."));
    }
}
