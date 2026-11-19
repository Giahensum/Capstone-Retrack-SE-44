using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services.ESign;

namespace Retrack.API.Controllers.ESign;

/// <summary>Ký điện tử hợp đồng đặt cọc thanh lý (DocuSign). Dùng chung cho Seller và Chủ kho; mã là id của thỏa thuận (liquidation_agreements).</summary>
[ApiController]
[Route("api/liquidations/esign")]
[Authorize(Roles = "SELLER,DEPOT_OWNER")]
public class LiquidationEsignController(ILiquidationEsignService service) : ControllerBase
{
    [HttpGet("{agreementId:guid}")]
    public async Task<IActionResult> Status(Guid agreementId, CancellationToken ct) =>
        Ok(ApiResponse<LiquidationEsignStatusDto>.Ok(await service.GetStatusAsync(GetUserId(), agreementId, ct)));

    /// <summary>Trả địa chỉ trang ký DocuSign của bên người gọi; frontend chuyển trình duyệt tới đó.</summary>
    [HttpPost("{agreementId:guid}/sign")]
    public async Task<IActionResult> Sign(Guid agreementId, CancellationToken ct) =>
        Ok(ApiResponse<EsignSigningUrlDto>.Ok(await service.CreateSigningUrlAsync(GetUserId(), agreementId, ct)));

    [HttpPost("{agreementId:guid}/refresh")]
    public async Task<IActionResult> Refresh(Guid agreementId, CancellationToken ct) =>
        Ok(ApiResponse<LiquidationEsignStatusDto>.Ok(await service.RefreshAsync(GetUserId(), agreementId, ct)));

    [HttpGet("{agreementId:guid}/document")]
    public async Task<IActionResult> Document(Guid agreementId, CancellationToken ct)
    {
        var (content, fileName) = await service.DownloadAsync(GetUserId(), agreementId, ct);
        return File(content, "application/pdf", fileName);
    }

    private Guid GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : throw new UnauthorizedAccessException("Token thiếu user id hợp lệ.");
    }
}
