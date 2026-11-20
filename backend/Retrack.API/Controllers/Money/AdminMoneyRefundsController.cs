using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Money;
using Retrack.API.Services.Money;

namespace Retrack.API.Controllers.Money;

/// <summary>Bất thường thu tiền và hoàn tiền cho người trả (Admin). Mỏng: logic nằm ở RefundEngine/MoneyAdminService.</summary>
[ApiController]
[Route("api/admin/money")]
[Authorize(Roles = "ADMIN")]
public class AdminMoneyRefundsController(RefundEngine engine, MoneyAdminService admin) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ----- Bất thường thu tiền -----
    [HttpGet("anomalies")]
    public async Task<IActionResult> Anomalies([FromQuery] string? status, [FromQuery] string? kind, [FromQuery] string? flow, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<AdminAnomalyDto>>.Ok(await admin.ListAnomaliesAsync(status, kind, flow, page, pageSize, ct)));

    /// <summary>Số bất thường OPEN và số lệnh hoàn chờ duyệt (huy hiệu).</summary>
    [HttpGet("anomalies/summary")]
    public async Task<IActionResult> AnomalySummary(CancellationToken ct) => Ok(ApiResponse<AnomalySummaryDto>.Ok(await admin.GetAnomalySummaryAsync(ct)));

    [HttpGet("anomalies/{id:guid}")]
    public async Task<IActionResult> Anomaly(Guid id, CancellationToken ct) => Ok(ApiResponse<AdminAnomalyDto>.Ok(await admin.GetAnomalyAsync(id, ct)));

    [HttpPost("anomalies/{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveAnomalyRequest req, CancellationToken ct) =>
        Ok(ApiResponse<AdminAnomalyDto>.Ok(await admin.ResolveAnomalyAsync(id, req.Note, ActorId, ct), "Đã đóng bất thường (không hoàn tiền)."));

    [HttpPost("anomalies/{id:guid}/refund")]
    public async Task<IActionResult> RefundAnomaly(Guid id, [FromBody] CreateAnomalyRefundRequest req, CancellationToken ct) =>
        await Detail(await engine.CreateForAnomalyAsync(id, req.Amount, req.Reason, ActorId, ct), "Đã tạo lệnh hoàn tiền.", ct);

    // ----- Hoàn khoản thu -----
    [HttpPost("collections/{id:guid}/refund")]
    public async Task<IActionResult> RefundCollection(Guid id, [FromBody] CreateCollectionRefundRequest req, CancellationToken ct) =>
        await Detail(await engine.CreateForCollectionAsync(id, req.Kind, req.Amount, req.Reason, ActorId, ct), "Đã tạo lệnh hoàn tiền.", ct);

    // ----- Lệnh hoàn tiền -----
    [HttpGet("refunds")]
    public async Task<IActionResult> Refunds([FromQuery] string? status, [FromQuery] Guid? payerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<AdminRefundDto>>.Ok(await admin.ListRefundsAsync(status, payerId, page, pageSize, ct)));

    [HttpGet("refunds/{id:guid}")]
    public async Task<IActionResult> Refund(Guid id, CancellationToken ct) => Ok(ApiResponse<AdminRefundDetailDto>.Ok(await admin.GetRefundAsync(id, ct)));

    [HttpPost("refunds/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) => await Detail(await engine.ApproveAsync(id, ActorId, ct), "Đã duyệt lệnh hoàn.", ct);

    [HttpPost("refunds/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct) => await Detail(await engine.RetryAsync(id, ActorId, ct), "Đã gửi lại lệnh hoàn.", ct);

    [HttpPost("refunds/{id:guid}/reconcile")]
    public async Task<IActionResult> Reconcile(Guid id, CancellationToken ct) => await Detail(await engine.ReconcileAsync(id, ActorId, ct), "Đã đối soát lệnh hoàn.", ct);

    [HttpPost("refunds/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelRefundRequest? req, CancellationToken ct) =>
        await Detail(await engine.CancelAsync(id, ActorId, req?.Note, ct), "Đã hủy lệnh hoàn.", ct);

    [HttpPost("refunds/{id:guid}/mark-paid")]
    public async Task<IActionResult> MarkPaid(Guid id, [FromBody] MarkPaidRequest req, CancellationToken ct) =>
        await Detail(await engine.MarkPaidManuallyAsync(id, ActorId, req.Reference, req.Note, req.Confirm, ct), "Đã đánh dấu đã hoàn tay.", ct);

    private async Task<IActionResult> Detail(Retrack.API.Models.MoneyRefund refund, string message, CancellationToken ct) =>
        Ok(ApiResponse<AdminRefundDetailDto>.Ok(await admin.GetRefundAsync(refund.Id, ct), message));
}
