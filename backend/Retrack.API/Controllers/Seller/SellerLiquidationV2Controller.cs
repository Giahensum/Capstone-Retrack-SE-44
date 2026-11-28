using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Liquidation;
using Retrack.API.DTOs.Seller;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Seller;

/// <summary>Seller: phiên thanh lý nhiều hạng mục; tiền đi trực tiếp giữa hai bên, nền tảng chỉ ghi nhận xác nhận.</summary>
[ApiController, Route("api/seller/liquidations/v2"), Authorize(Roles = "SELLER")]
public class SellerLiquidationV2Controller(ISellerLiquidationV2Service service) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> Create(CreateLiquidationV2Dto dto) =>
        Ok(ApiResponse<SellerLiquidationV2DetailDto>.Ok(await service.CreateAsync(UserId, dto), "Đã đăng phiên thanh lý nhiều hạng mục."));

    [HttpGet]
    public async Task<IActionResult> List(string? search = null, int page = 1, int pageSize = 20) =>
        Ok(ApiResponse<PagedResult<SellerLiquidationV2SummaryDto>>.Ok(await service.ListAsync(UserId, search, page, pageSize)));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id) =>
        Ok(ApiResponse<SellerLiquidationV2DetailDto>.Ok(await service.DetailAsync(UserId, id)));

    [HttpGet("{id:guid}/items/{itemId:guid}/bids")]
    public async Task<IActionResult> Bids(Guid id, Guid itemId, int page = 1) =>
        Ok(ApiResponse<PagedResult<SellerLiquidationV2BidDto>>.Ok(await service.ItemBidsAsync(UserId, id, itemId, page)));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancelLiquidationLotDto dto)
    {
        await service.CancelAsync(UserId, id, dto.Reason);
        return Ok(ApiResponse<string>.Ok("Đã hủy phiên thanh lý."));
    }

    [HttpPatch("agreements/{id:guid}/handover-quantity")]
    public async Task<IActionResult> DecideQuantity(Guid id, DecideLiquidationHandoverDto dto) =>
        Ok(ApiResponse<LiquidationV2AgreementDto>.Ok(await service.DecideHandoverAsync(UserId,id,dto),"Đã lưu phản hồi bảng cân/kiểm đếm."));

    [HttpPatch("agreements/{id:guid}/confirm-payment")]
    public async Task<IActionResult> ConfirmPayment(Guid id) =>
        Ok(ApiResponse<LiquidationV2AgreementDto>.Ok(await service.SellerActionAsync(UserId, id, "CONFIRM_PAYMENT"), "Đã xác nhận nhận đủ tiền."));

    [HttpPatch("agreements/{id:guid}/confirm-handover")]
    public async Task<IActionResult> ConfirmHandover(Guid id) =>
        Ok(ApiResponse<LiquidationV2AgreementDto>.Ok(await service.SellerActionAsync(UserId, id, "CONFIRM_HANDOVER"), "Đã ghi nhận bàn giao."));

    [HttpPost("agreements/{id:guid}/abandon-report")]
    public async Task<IActionResult> Abandon(Guid id, LiquidationV2ReasonInput dto) =>
        Ok(ApiResponse<LiquidationV2AgreementDto>.Ok(await service.SellerActionAsync(UserId, id, "REPORT_ABANDON", dto.Reason), "Đã gửi báo cáo cho Admin xử lý."));
}
