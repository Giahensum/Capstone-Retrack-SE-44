using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Liquidation;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Depot;

/// <summary>Chủ kho: phiên thanh lý nhiều hạng mục — đăng ký hạng mục, trả giá theo đơn giá, thỏa thuận và nguồn nhập kho.</summary>
[ApiController, Route("api/depot/liquidations/v2"), Authorize(Roles = "DEPOT_OWNER")]
public class DepotLiquidationV2Controller(ILiquidationV2Service service) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List(Guid depotId, string? view = null, string? search = null, int page = 1, int pageSize = 20) =>
        Ok(ApiResponse<PagedResult<LiquidationV2SummaryDto>>.Ok(await service.ListAsync(UserId, depotId, view, search, page, pageSize)));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid depotId, Guid id) =>
        Ok(ApiResponse<LiquidationV2DetailDto>.Ok(await service.DetailAsync(UserId, depotId, id)));

    [HttpPost("{id:guid}/participation")]
    public async Task<IActionResult> Register(Guid depotId, Guid id, LiquidationV2ParticipationInput dto) =>
        Ok(ApiResponse<LiquidationV2DetailDto>.Ok(await service.RegisterAsync(UserId, depotId, id, dto), "Đã đăng ký khảo sát các hạng mục đã chọn."));

    [HttpPatch("{id:guid}/survey-confirmation")]
    public async Task<IActionResult> Survey(Guid depotId, Guid id, LiquidationConsentInput dto) =>
        Ok(ApiResponse<LiquidationV2DetailDto>.Ok(await service.ConfirmSurveyAsync(UserId, depotId, id, dto.Accepted), "Đã lưu xác nhận tự khảo sát/hiểu hiện trạng."));

    [HttpPost("{id:guid}/items/{itemId:guid}/bids")]
    public async Task<IActionResult> Bid(Guid depotId, Guid id, Guid itemId, LiquidationV2BidInput dto) =>
        Ok(ApiResponse<LiquidationV2BidResultDto>.Ok(await service.BidAsync(UserId, depotId, id, itemId, dto), "Đã ghi nhận đơn giá."));

    [HttpGet("{id:guid}/items/{itemId:guid}/bids")]
    public async Task<IActionResult> MyBids(Guid depotId, Guid id, Guid itemId, int page = 1) =>
        Ok(ApiResponse<PagedResult<LiquidationV2MyBidDto>>.Ok(await service.MyBidsAsync(UserId, depotId, id, itemId, page)));

    [HttpGet("agreements/{id:guid}")]
    public async Task<IActionResult> Agreement(Guid depotId, Guid id) =>
        Ok(ApiResponse<LiquidationV2AgreementDto>.Ok(await service.AgreementAsync(UserId, depotId, id)));

    [HttpPost("agreements/{id:guid}/handover-quantity")]
    public async Task<IActionResult> ProposeQuantity(Guid depotId, Guid id, ProposeLiquidationHandoverDto dto) =>
        Ok(ApiResponse<LiquidationV2AgreementDto>.Ok(await service.ProposeHandoverAsync(UserId,depotId,id,dto),"Đã gửi bảng cân/kiểm đếm cho Seller."));

    [HttpPatch("agreements/{id:guid}/report-payment")]
    public async Task<IActionResult> ReportPayment(Guid depotId, Guid id, LiquidationPaymentInput dto) =>
        Ok(ApiResponse<LiquidationV2AgreementDto>.Ok(await service.BuyerActionAsync(UserId, depotId, id, "REPORT_PAYMENT", dto.Reference), "Đã báo đã thanh toán trực tiếp, chờ Seller xác nhận."));

    [HttpPatch("agreements/{id:guid}/confirm-received")]
    public async Task<IActionResult> Received(Guid depotId, Guid id) =>
        Ok(ApiResponse<LiquidationV2AgreementDto>.Ok(await service.BuyerActionAsync(UserId, depotId, id, "CONFIRM_RECEIVED"), "Đã xác nhận nhận đủ hàng."));

    [HttpPost("agreements/{id:guid}/abandon-report")]
    public async Task<IActionResult> Abandon(Guid depotId, Guid id, LiquidationV2ReasonInput dto) =>
        Ok(ApiResponse<LiquidationV2AgreementDto>.Ok(await service.BuyerActionAsync(UserId, depotId, id, "REPORT_ABANDON", dto.Reason), "Đã gửi báo cáo cho Admin xử lý."));

    [HttpGet("agreements/{id:guid}/intake-source")]
    public async Task<IActionResult> IntakeSource(Guid depotId, Guid id) =>
        Ok(ApiResponse<LiquidationIntakeSourceDto>.Ok(await service.IntakeSourceAsync(UserId, depotId, id)));
}
