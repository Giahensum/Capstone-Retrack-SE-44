using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services;

namespace Retrack.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SellerPickupController : ControllerBase
    {
        private readonly IPickupService _pickupService;

        public SellerPickupController(IPickupService pickupService) => _pickupService = pickupService;

        private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Seller tạo yêu cầu thu gom</summary>
        [HttpPost]
        [Authorize(Roles = "SELLER")]
        public async Task<IActionResult> Create([FromBody] CreatePickupRequestDto dto)
        {
            var result = await _pickupService.CreateAsync(GetUserId(), dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                ApiResponse<PickupRequestDto>.Ok(result, "Tạo yêu cầu thành công."));
        }

        /// <summary>Lấy chi tiết 1 yêu cầu</summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _pickupService.GetByIdAsync(id);
            if (result == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy."));
            return Ok(ApiResponse<PickupRequestDto>.Ok(result));
        }

        /// <summary>Seller xem danh sách yêu cầu của mình</summary>
        [HttpGet("my")]
        [Authorize(Roles = "SELLER")]
        public async Task<IActionResult> GetMy()
        {
            var result = await _pickupService.GetBySellerAsync(GetUserId());
            return Ok(ApiResponse<List<PickupRequestDto>>.Ok(result));
        }

        /// <summary>Seller xác nhận kết quả cân</summary>
        [HttpPatch("{id:guid}/confirm")]
        [Authorize(Roles = "SELLER")]
        public async Task<IActionResult> Confirm(Guid id)
        {
            var result = await _pickupService.ConfirmBySellerAsync(id, GetUserId());
            return Ok(ApiResponse<PickupRequestDto>.Ok(result, "Đã xác nhận."));
        }
    }

    // -------------------------------------------------------
    [ApiController]
    [Route("api/depot")]
    [Authorize(Roles = "DEPOT_OWNER,DEPOT_EMPLOYEE")]
    public class DepotController : ControllerBase
    {
        private readonly IPickupService _pickupService;

        public DepotController(IPickupService pickupService) => _pickupService = pickupService;

        private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Xem các yêu cầu đang chờ tại depot</summary>
        [HttpGet("{depotId:guid}/pickup-requests")]
        public async Task<IActionResult> GetRequests(Guid depotId)
        {
            var result = await _pickupService.GetPendingForDepotAsync(depotId);
            return Ok(ApiResponse<List<PickupRequestDto>>.Ok(result));
        }

        /// <summary>Nhân viên nhận đơn</summary>
        [HttpPatch("pickup-requests/{id:guid}/accept")]
        public async Task<IActionResult> Accept(Guid id)
        {
            var result = await _pickupService.AcceptRequestAsync(id, GetUserId());
            return Ok(ApiResponse<PickupRequestDto>.Ok(result, "Đã nhận đơn."));
        }

        /// <summary>Nhân viên nhập kết quả cân</summary>
        [HttpPatch("pickup-requests/{id:guid}/weigh")]
        public async Task<IActionResult> Weigh(Guid id, [FromBody] WeighRequestBody body)
        {
            var result = await _pickupService.WeighAndUpdateAsync(id, body.Items, body.CheckinImageUrl);
            return Ok(ApiResponse<PickupRequestDto>.Ok(result, "Cập nhật cân thành công."));
        }

        /// <summary>Depot Owner xác nhận đã thanh toán cho Seller</summary>
        [HttpPatch("pickup-requests/{id:guid}/payment-sent")]
        [Authorize(Roles = "DEPOT_OWNER")]
        public async Task<IActionResult> MarkPaymentSent(Guid id, [FromBody] PaymentProofBody body)
        {
            var result = await _pickupService.MarkPaymentSentAsync(id, body.PaymentProofUrl);
            return Ok(ApiResponse<PickupRequestDto>.Ok(result, "Đã ghi nhận thanh toán."));
        }

        public record WeighRequestBody(List<WeighItemDto> Items, string? CheckinImageUrl);
        public record PaymentProofBody(string PaymentProofUrl);
    }

    // -------------------------------------------------------
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "ADMIN")]
    public class AdminController : ControllerBase
    {
        private readonly IPickupService _pickupService;

        public AdminController(IPickupService pickupService) => _pickupService = pickupService;

        /// <summary>Admin xem tất cả pickup requests</summary>
        [HttpGet("pickup-requests")]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            // TODO: implement paged result
            return Ok(ApiResponse<string>.Ok("Admin endpoint ready"));
        }
    }
}

