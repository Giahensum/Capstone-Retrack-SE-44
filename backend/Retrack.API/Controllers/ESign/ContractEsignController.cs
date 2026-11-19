using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services.ESign;

namespace Retrack.API.Controllers.ESign;

/// <summary>Ký điện tử hợp đồng hợp tác Nhà máy – Vựa (DocuSign sandbox). Dùng chung cho cả hai bên; mã quan hệ hợp tác lấy từ danh sách đối tác.</summary>
[ApiController]
[Route("api/contracts/esign")]
[Authorize(Roles = "FACTORY,DEPOT_OWNER")]
public class ContractEsignController(IContractEsignService service) : ControllerBase
{
    [HttpGet("{partnershipId:guid}")]
    public async Task<IActionResult> Status(Guid partnershipId, CancellationToken ct) =>
        Ok(ApiResponse<EsignStatusDto>.Ok(await service.GetStatusAsync(GetUserId(), partnershipId, ct)));

    /// <summary>Trả địa chỉ trang ký DocuSign của bên người gọi; frontend chuyển trình duyệt tới đó.</summary>
    [HttpPost("{partnershipId:guid}/sign")]
    public async Task<IActionResult> Sign(Guid partnershipId, CancellationToken ct) =>
        Ok(ApiResponse<EsignSigningUrlDto>.Ok(await service.CreateSigningUrlAsync(GetUserId(), partnershipId, ct)));

    [HttpPost("{partnershipId:guid}/refresh")]
    public async Task<IActionResult> Refresh(Guid partnershipId, CancellationToken ct) =>
        Ok(ApiResponse<EsignStatusDto>.Ok(await service.RefreshAsync(GetUserId(), partnershipId, ct)));

    [HttpGet("{partnershipId:guid}/document")]
    public async Task<IActionResult> Document(Guid partnershipId, CancellationToken ct)
    {
        var (content, fileName) = await service.DownloadAsync(GetUserId(), partnershipId, ct);
        return File(content, "application/pdf", fileName);
    }

    private Guid GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : throw new UnauthorizedAccessException("Token thiếu user id hợp lệ.");
    }
}

/// <summary>
/// Địa chỉ DocuSign gọi khi hợp đồng đổi trạng thái. Không dùng JWT; không tin nội dung gửi tới: chỉ lấy mã envelope để tra cứu,
/// rồi hệ thống tự hỏi lại DocuSign bằng khóa của mình. Luôn trả 200 để DocuSign không gửi lại liên tục.
/// </summary>
[ApiController]
public class DocuSignWebhookController(IContractEsignService service, ILiquidationEsignService liquidation, ILogger<DocuSignWebhookController> logger) : ControllerBase
{
    [HttpPost("api/esign/docusign/webhook"), AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(ct);
        // Mã envelope chỉ thuộc về một loại hợp đồng; hai dịch vụ tự bỏ qua mã không phải của mình.
        try { await service.HandleWebhookAsync(body, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Xử lý webhook DocuSign (hợp đồng đối tác) lỗi");
        }
        try { await liquidation.HandleWebhookAsync(body, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Xử lý webhook DocuSign (hợp đồng thanh lý) lỗi");
        }
        return Ok();
    }
}
