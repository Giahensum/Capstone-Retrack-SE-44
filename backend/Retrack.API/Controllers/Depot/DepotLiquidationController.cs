using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Liquidation;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Depot;

[ApiController, Route("api/depot/liquidations"), Authorize(Roles = "DEPOT_OWNER")]
public class DepotLiquidationController(ILiquidationService service) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public async Task<IActionResult> List(Guid depotId, [FromQuery] LiquidationQuery query) =>
        Ok(ApiResponse<PagedResult<LiquidationSummaryDto>>.Ok(await service.ListAsync(UserId, depotId, query)));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid depotId, Guid id) => Ok(ApiResponse<LiquidationDetailDto>.Ok(await service.DetailAsync(UserId, depotId, id)));
    [HttpPost("{id:guid}/participation")]
    public async Task<IActionResult> Participate(Guid depotId, Guid id, LiquidationConsentInput dto)
    {
        await service.ParticipateAsync(UserId, depotId, id, dto.Accepted, false); return Ok(ApiResponse<string>.Ok("Đã đăng ký khảo sát cho kho. Các kho khác vẫn có thể tham gia."));
    }
    [HttpPatch("{id:guid}/survey-confirmation")]
    public async Task<IActionResult> Survey(Guid depotId, Guid id, LiquidationConsentInput dto)
    {
        await service.ParticipateAsync(UserId, depotId, id, dto.Accepted, true); return Ok(ApiResponse<string>.Ok("Đã lưu xác nhận tự khảo sát/hiểu hiện trạng của chủ kho."));
    }
    [HttpPost("{id:guid}/bids")]
    public async Task<IActionResult> Bid(Guid depotId, Guid id, LiquidationBidInput dto) =>
        Ok(ApiResponse<MyLiquidationBidDto>.Ok(await service.BidAsync(UserId, depotId, id, dto)));
    [HttpGet("{id:guid}/bids")]
    public async Task<IActionResult> Bids(Guid depotId, Guid id, int page = 1) =>
        Ok(ApiResponse<PagedResult<MyLiquidationBidDto>>.Ok(await service.MyBidsAsync(UserId, depotId, id, page)));
    [HttpGet("agreements/{id:guid}")]
    public async Task<IActionResult> Agreement(Guid depotId, Guid id) =>
        Ok(ApiResponse<LiquidationAgreementDto>.Ok(await service.AgreementAsync(UserId, depotId, id)));
    [HttpGet("{id:guid}/ladder")]
    public async Task<IActionResult> Ladder(Guid depotId, Guid id, int page = 1) =>
        Ok(ApiResponse<PagedResult<LiquidationLadderDto>>.Ok(await service.LadderAsync(UserId, depotId, id, page)));
    [HttpPatch("agreements/{id:guid}/sign")]
    public async Task<IActionResult> Sign(Guid depotId, Guid id)
    {
        await service.AgreementActionAsync(UserId, depotId, id, "SIGN"); return Ok(ApiResponse<string>.Ok("Đã ký hợp đồng đặt cọc (xác nhận điện tử của chủ kho)."));
    }
    [HttpPatch("agreements/{id:guid}/report-deposit")]
    public async Task<IActionResult> ReportDeposit(Guid depotId, Guid id, LiquidationPaymentInput dto)
    {
        await service.AgreementActionAsync(UserId, depotId, id, "REPORT_DEPOSIT", dto.Reference); return Ok(ApiResponse<string>.Ok("Đã báo chuyển cọc ngoài hệ thống, chờ Seller xác nhận."));
    }
    [HttpPatch("agreements/{id:guid}/report-payment")]
    public async Task<IActionResult> ReportPayment(Guid depotId, Guid id, LiquidationPaymentInput dto)
    {
        await service.AgreementActionAsync(UserId, depotId, id, "REPORT_PAYMENT", dto.Reference); return Ok(ApiResponse<string>.Ok("Đã báo chuyển tiền ngoài hệ thống, chờ Seller xác nhận."));
    }
    [HttpPatch("agreements/{id:guid}/confirm-collected")]
    public async Task<IActionResult> Collected(Guid depotId, Guid id)
    {
        await service.AgreementActionAsync(UserId, depotId, id, "CONFIRM_COLLECTED"); return Ok(ApiResponse<string>.Ok("Đã ghi nhận chủ kho thu đủ và đưa hàng về kho."));
    }
}
