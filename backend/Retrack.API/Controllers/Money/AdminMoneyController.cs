using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Money;
using Retrack.API.Services.Money;

namespace Retrack.API.Controllers.Money;

/// <summary>Thu chi qua sàn dành cho Admin: cấu hình, hoa hồng, kỳ chi, số dư, chuyển thử, sổ cái, người nhận. Mỏng: logic nằm ở PayoutEngine/MoneyAdminService.</summary>
[ApiController]
[Route("api/admin/money")]
[Authorize(Roles = "ADMIN")]
public class AdminMoneyController(PayoutEngine engine, MoneyAdminService admin) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ----- Cấu hình -----
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct) => Ok(ApiResponse<MoneySettingsDto>.Ok(await admin.GetSettingsAsync(ct)));

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateMoneySettingsDto dto, CancellationToken ct) =>
        Ok(ApiResponse<MoneySettingsDto>.Ok(await admin.UpdateSettingsAsync(dto, ActorId, ct), "Đã lưu cấu hình thu chi."));

    // ----- Hoa hồng -----
    [HttpGet("commissions")]
    public async Task<IActionResult> GetCommissions([FromQuery] Guid? userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<CommissionsDto>.Ok(await admin.GetCommissionsAsync(userId, page, pageSize, ct)));

    /// <summary>Đặt hoa hồng mặc định của một dòng tiền: { flow, rate }.</summary>
    [HttpPut("commissions/default")]
    public async Task<IActionResult> SetDefaultCommission([FromBody] SetCommissionDto dto, CancellationToken ct) =>
        Ok(ApiResponse<MoneySettingsDto>.Ok(await admin.SetDefaultCommissionAsync(dto, ActorId, ct), "Đã lưu hoa hồng mặc định."));

    /// <summary>Đặt (hoặc xóa khi rate = null) hoa hồng riêng của người nhận: { flow, rate }.</summary>
    [HttpPut("commissions/{userId:guid}")]
    public async Task<IActionResult> SetPayeeCommission(Guid userId, [FromBody] SetCommissionDto dto, CancellationToken ct) =>
        Ok(ApiResponse<CommissionOverrideDto?>.Ok(await admin.SetPayeeCommissionAsync(userId, dto, ActorId, ct), "Đã lưu hoa hồng riêng."));

    // ----- Kỳ chi -----
    [HttpPost("payouts/preview")]
    public async Task<IActionResult> Preview([FromBody] PayoutPreviewRequest req, CancellationToken ct) =>
        Ok(ApiResponse<PayoutBatchResult>.Ok(await engine.PreviewAsync(req.PayeeIds, req.PeriodStart.UtcDateTime, req.PeriodEnd.UtcDateTime, ct)));

    [HttpPost("payouts/run")]
    public async Task<IActionResult> Run([FromBody] PayoutPreviewRequest req, CancellationToken ct) =>
        Ok(ApiResponse<PayoutBatchResult>.Ok(await engine.RunAsync(req.PayeeIds, req.PeriodStart.UtcDateTime, req.PeriodEnd.UtcDateTime, ActorId, autoOnly: false, ct), "Đã chạy đối soát."));

    [HttpGet("payouts")]
    public async Task<IActionResult> ListPayouts([FromQuery] string? status, [FromQuery] Guid? payeeId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<PayoutRunListDto>>.Ok(await admin.ListPayoutsAsync(status, payeeId, from, to, page, pageSize, ct)));

    [HttpGet("payouts/{id:guid}")]
    public async Task<IActionResult> GetPayout(Guid id, CancellationToken ct) => Ok(ApiResponse<PayoutRunDetailDto>.Ok(await admin.GetPayoutAsync(id, ct)));

    [HttpPost("payouts/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) => await Detail(await engine.ApproveAsync(id, ActorId, ct), "Đã duyệt kỳ chi.", ct);

    [HttpPost("payouts/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct) => await Detail(await engine.RetryAsync(id, ActorId, ct), "Đã gửi lại kỳ chi.", ct);

    [HttpPost("payouts/{id:guid}/reconcile")]
    public async Task<IActionResult> Reconcile(Guid id, CancellationToken ct) => await Detail(await engine.ReconcileAsync(id, ActorId, ct), "Đã đối soát kỳ chi.", ct);

    [HttpPost("payouts/{id:guid}/release")]
    public async Task<IActionResult> Release(Guid id, CancellationToken ct) => await Detail(await engine.ReleaseAsync(id, ActorId, ct), "Đã giải phóng kỳ chi.", ct);

    [HttpPost("payouts/{id:guid}/mark-paid")]
    public async Task<IActionResult> MarkPaid(Guid id, [FromBody] MarkPaidRequest req, CancellationToken ct) =>
        await Detail(await engine.MarkPaidManuallyAsync(id, ActorId, req.Reference, req.Note, req.Confirm, ct), "Đã đánh dấu đã chi tay.", ct);

    private async Task<IActionResult> Detail(Retrack.API.Models.PayoutRun run, string message, CancellationToken ct) =>
        Ok(ApiResponse<PayoutRunDetailDto>.Ok(await admin.GetPayoutAsync(run.Id, ct), message));

    // ----- Số dư và chuyển thử -----
    [HttpGet("balance")]
    public async Task<IActionResult> Balance(CancellationToken ct) => Ok(ApiResponse<ProviderBalance>.Ok(await admin.GetBalanceAsync(ct)));

    [HttpPost("test-transfer")]
    public async Task<IActionResult> TestTransfer([FromBody] TestTransferBody body, CancellationToken ct) =>
        Ok(ApiResponse<TestTransferResult>.Ok(await engine.TestTransferAsync(ActorId, new(body.ClientKey, body.Amount, body.ToBin, body.ToAccountNumber), ct), "Đã gửi lệnh chuyển thử."));

    [HttpGet("test-transfer/status")]
    public async Task<IActionResult> TestTransferStatus([FromQuery] string key, CancellationToken ct) =>
        Ok(ApiResponse<TestTransferStatus>.Ok(await engine.GetTestTransferStatusAsync(key, ct)));

    // ----- Thu và sổ cái -----
    [HttpGet("collections")]
    public async Task<IActionResult> Collections([FromQuery] string? status, [FromQuery] string? flow, [FromQuery] Guid? payeeId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<MoneyCollectionDto>>.Ok(await admin.ListCollectionsAsync(status, flow, payeeId, page, pageSize, ct)));

    [HttpGet("ledger")]
    public async Task<IActionResult> Ledger([FromQuery] Guid? payeeId, [FromQuery] string? flow, [FromQuery] string? state, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<PayableLineDto>>.Ok(await admin.ListLedgerAsync(payeeId, flow, state, page, pageSize, ct)));

    // ----- Người nhận -----
    [HttpGet("payees")]
    public async Task<IActionResult> Payees([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<PayeeSummaryDto>>.Ok(await admin.ListPayeesAsync(search, page, pageSize, ct)));

    [HttpGet("payees/{userId:guid}/bank")]
    public async Task<IActionResult> PayeeBank(Guid userId, CancellationToken ct) => Ok(ApiResponse<PayeeBankAdminDto>.Ok(await admin.GetPayeeBankAsync(userId, ct)));

    [HttpPost("payees/{userId:guid}/bank/verify")]
    public async Task<IActionResult> VerifyBank(Guid userId, [FromBody] VerifyBankRequest req, CancellationToken ct) =>
        Ok(ApiResponse<PayeeBankDto>.Ok(await admin.VerifyBankAsync(userId, req.ExpectedAccountNumber, ActorId, ct), "Đã xác minh tài khoản ngân hàng."));

    [HttpPut("payees/{userId:guid}/auto-payout")]
    public async Task<IActionResult> SetAutoPayout(Guid userId, [FromBody] AutoPayoutRequest req, CancellationToken ct) =>
        Ok(ApiResponse<bool>.Ok(await admin.SetAutoPayoutAsync(userId, req.AutoEnabled, ActorId, ct), "Đã lưu công tắc tự chi."));
}
