using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services;

namespace Retrack.API.Controllers.Admin;

/// <summary>
/// UC-7.8 (cấu hình phí), UC-7.9 (doanh thu), UC-7.10 (lịch sử giao dịch),
/// UC-7.11 (hóa đơn phí hàng tháng), UC-7.12 (theo dõi thanh toán phí)
/// </summary>
[ApiController]
[Route("api/admin/billing")]
[Authorize(Roles = "ADMIN")]
public class AdminBillingController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminBillingController(IAdminService adminService) => _adminService = adminService;

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Xem cấu hình phí nền tảng hiện tại (UC-7.8)</summary>
    [HttpGet("fee-config")]
    public async Task<IActionResult> GetFeeConfig()
    {
        var result = await _adminService.GetFeeConfigAsync();
        return Ok(ApiResponse<FeeConfigDto>.Ok(result));
    }

    /// <summary>Cập nhật % phí nền tảng (UC-7.8)</summary>
    [HttpPut("fee-config")]
    public async Task<IActionResult> UpdateFeeConfig([FromBody] UpdateFeeConfigDto dto)
    {
        var result = await _adminService.UpdateFeeConfigAsync(dto, GetUserId());
        return Ok(ApiResponse<FeeConfigDto>.Ok(result, "Đã cập nhật phí nền tảng."));
    }

    /// <summary>Doanh thu nền tảng theo khoảng thời gian (UC-7.9)</summary>
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenue([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string groupBy = "day")
    {
        var effectiveFrom = from ?? DateTime.UtcNow.AddMonths(-1);
        var effectiveTo = to ?? DateTime.UtcNow;
        var result = await _adminService.GetRevenueReportAsync(effectiveFrom, effectiveTo, groupBy);
        return Ok(ApiResponse<RevenueReportDto>.Ok(result));
    }

    /// <summary>Lịch sử giao dịch toàn hệ thống (UC-7.10)</summary>
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? sourceType,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _adminService.GetTransactionsAsync(from, to, sourceType, page, pageSize);
        return Ok(ApiResponse<PagedResult<TransactionHistoryItemDto>>.Ok(result));
    }

    /// <summary>Tạo hóa đơn phí hàng tháng cho tất cả payer có phát sinh phí (UC-7.11)</summary>
    [HttpPost("invoices/generate")]
    public async Task<IActionResult> GenerateInvoices([FromBody] GenerateInvoiceRequestDto dto)
    {
        var result = await _adminService.GenerateMonthlyInvoicesAsync(dto.Year, dto.Month, GetUserId());
        return Ok(ApiResponse<List<PlatformInvoiceDto>>.Ok(result, $"Đã tạo {result.Count} hóa đơn."));
    }

    /// <summary>Danh sách hóa đơn phí, lọc theo trạng thái thanh toán (UC-7.11, UC-7.12)</summary>
    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _adminService.GetInvoicesAsync(status, page, pageSize);
        return Ok(ApiResponse<PagedResult<PlatformInvoiceDto>>.Ok(result));
    }

    /// <summary>Đánh dấu hóa đơn đã thanh toán (UC-7.12)</summary>
    [HttpPatch("invoices/{id:guid}/mark-paid")]
    public async Task<IActionResult> MarkPaid(Guid id)
    {
        var result = await _adminService.MarkInvoicePaidAsync(id, GetUserId());
        return Ok(ApiResponse<PlatformInvoiceDto>.Ok(result, "Đã ghi nhận thanh toán."));
    }

    /// <summary>Gửi lại thông báo nhắc thanh toán hóa đơn (UC-7.12)</summary>
    [HttpPost("invoices/{id:guid}/remind")]
    public async Task<IActionResult> Remind(Guid id)
    {
        await _adminService.ResendInvoiceReminderAsync(id);
        return Ok(ApiResponse<string>.Ok(string.Empty, "Đã gửi nhắc nhở."));
    }
}
