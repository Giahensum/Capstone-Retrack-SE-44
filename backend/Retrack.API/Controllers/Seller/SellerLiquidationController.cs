using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Seller;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Seller;

/// <summary>Seller đăng lô thanh lý, theo dõi phiên trả giá và xác nhận thỏa thuận (tiền đi ngoài hệ thống).</summary>
[ApiController, Route("api/seller/liquidations"), Authorize(Roles = "SELLER")]
public class SellerLiquidationController(ISellerLiquidationService service) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> Create(CreateLiquidationLotDto dto) =>
        Ok(ApiResponse<SellerLiquidationDetailDto>.Ok(await service.CreateAsync(UserId, dto), "Đã đăng lô thanh lý."));

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] SellerLiquidationQuery query) =>
        Ok(ApiResponse<PagedResult<SellerLiquidationSummaryDto>>.Ok(await service.ListAsync(UserId, query)));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id) =>
        Ok(ApiResponse<SellerLiquidationDetailDto>.Ok(await service.DetailAsync(UserId, id)));

    [HttpGet("{id:guid}/bids")]
    public async Task<IActionResult> Bids(Guid id, int page = 1) =>
        Ok(ApiResponse<PagedResult<SellerLiquidationBidDto>>.Ok(await service.BidsAsync(UserId, id, page)));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancelLiquidationLotDto dto)
    {
        await service.CancelAsync(UserId, id, dto.Reason);
        return Ok(ApiResponse<string>.Ok("Đã hủy lô thanh lý."));
    }

    [HttpPatch("agreements/{id:guid}/confirm-payment")]
    public async Task<IActionResult> ConfirmPayment(Guid id) =>
        Ok(ApiResponse<SellerLiquidationAgreementDto>.Ok(await service.ConfirmPaymentAsync(UserId, id), "Đã xác nhận nhận đủ tiền."));

    [HttpPatch("agreements/{id:guid}/confirm-handover")]
    public async Task<IActionResult> ConfirmHandover(Guid id) =>
        Ok(ApiResponse<SellerLiquidationAgreementDto>.Ok(await service.ConfirmHandoverAsync(UserId, id), "Đã ghi nhận bàn giao."));

    [HttpPatch("agreements/{id:guid}/sign")]
    public async Task<IActionResult> Sign(Guid id) =>
        Ok(ApiResponse<SellerLiquidationAgreementDto>.Ok(await service.SignAsync(UserId, id), "Đã ký hợp đồng đặt cọc."));

    [HttpPatch("agreements/{id:guid}/confirm-deposit")]
    public async Task<IActionResult> ConfirmDeposit(Guid id) =>
        Ok(ApiResponse<SellerLiquidationAgreementDto>.Ok(await service.ConfirmDepositAsync(UserId, id), "Đã xác nhận nhận tiền cọc."));
}
