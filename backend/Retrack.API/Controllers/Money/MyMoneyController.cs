using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Money;
using Retrack.API.Services.Money;

namespace Retrack.API.Controllers.Money;

/// <summary>Thu nhập và tài khoản ngân hàng của chính người nhận. Mã người dùng lấy từ token, không nhận từ request.</summary>
[ApiController]
[Route("api/money")]
[Authorize]
public class MyMoneyController(MyMoneyService service) : ControllerBase
{
    private const string PayeeRoleList = "SELLER,DEPOT_OWNER,FACTORY";
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Danh sách ngân hàng kèm mã BIN để chọn trên giao diện (mọi người dùng đã đăng nhập).</summary>
    [HttpGet("banks")]
    public IActionResult Banks() =>
        Ok(ApiResponse<IReadOnlyList<BankOptionDto>>.Ok(VietnameseBanks.All.Select(b => new BankOptionDto(b.Bin, b.Name)).ToList()));

    [HttpGet("my/earnings")]
    [Authorize(Roles = PayeeRoleList)]
    public async Task<IActionResult> Earnings(CancellationToken ct) => Ok(ApiResponse<MyEarningsDto>.Ok(await service.GetEarningsAsync(UserId, ct)));

    [HttpGet("my/payouts")]
    [Authorize(Roles = PayeeRoleList)]
    public async Task<IActionResult> Payouts([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<MyPayoutDto>>.Ok(await service.ListPayoutsAsync(UserId, page, pageSize, ct)));

    /// <summary>Các lệnh hoàn tiền của chính người trả (SELLER, DEPOT_OWNER, FACTORY): code, amount, status, statusNote, createdAt, paidAt.</summary>
    [HttpGet("my/refunds")]
    [Authorize(Roles = PayeeRoleList)]
    public async Task<IActionResult> Refunds([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<MyRefundDto>>.Ok(await service.ListRefundsAsync(UserId, page, pageSize, ct)));

    [HttpGet("my/bank")]
    [Authorize(Roles = PayeeRoleList)]
    public async Task<IActionResult> GetBank(CancellationToken ct) => Ok(ApiResponse<PayeeBankDto>.Ok(await service.GetBankAsync(UserId, ct)));

    [HttpPut("my/bank")]
    [Authorize(Roles = PayeeRoleList)]
    public async Task<IActionResult> UpdateBank([FromBody] UpdateBankRequest req, CancellationToken ct) =>
        Ok(ApiResponse<PayeeBankDto>.Ok(await service.UpdateBankAsync(UserId, req, ct), "Đã lưu tài khoản ngân hàng."));
}
