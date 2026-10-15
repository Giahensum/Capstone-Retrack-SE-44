using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Seller;

[ApiController]
[Route("api/seller/pickups")]
[Authorize(Roles = "SELLER")]
public class SellerPickupController : ControllerBase
{
    private readonly IPickupService _pickupService;
    private readonly ISellerService _sellerService;
    private readonly ICloudinaryService _cloudinary;

    public SellerPickupController(
        IPickupService pickupService,
        ISellerService sellerService,
        ICloudinaryService cloudinary)
    {
        _pickupService = pickupService;
        _sellerService = sellerService;
        _cloudinary = cloudinary;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Upload ảnh phế liệu lên Cloudinary (multipart/form-data)</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<string>.Fail("Vui lòng chọn ảnh để tải lên."));

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(ApiResponse<string>.Fail("Dung lượng ảnh không được vượt quá 10MB."));

        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/heic" };
        if (!allowed.Contains(file.ContentType.ToLower()))
            return BadRequest(ApiResponse<string>.Fail("Chỉ chấp nhận ảnh định dạng JPG, PNG, WebP hoặc HEIC."));

        using var stream = file.OpenReadStream();
        var url = await _cloudinary.UploadImageAsync(stream, file.FileName);
        return Ok(ApiResponse<object>.Ok(new { url }, "Upload ảnh thành công."));
    }

    /// <summary>Tạo yêu cầu thu gom phế liệu</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePickupRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            return BadRequest(ApiResponse<string>.Fail(firstError ?? "Dữ liệu tạo yêu cầu không hợp lệ."));
        }

        try
        {
            var result = await _pickupService.CreateAsync(GetUserId(), dto);
            return Ok(ApiResponse<PickupRequestDto>.Ok(result, "Tạo yêu cầu thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    /// <summary>Lấy danh sách đơn của Seller (có filter theo status)</summary>
    [HttpGet]
    public async Task<IActionResult> GetMyRequests([FromQuery] string? status)
    {
        var requests = await _pickupService.GetBySellerAsync(GetUserId());
        if (!string.IsNullOrEmpty(status))
            requests = requests.Where(r => r.Status.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        return Ok(ApiResponse<List<PickupRequestDto>>.Ok(requests));
    }

    /// <summary>Lấy chi tiết 1 đơn</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _pickupService.GetByIdAsync(id, GetUserId());
        if (result == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy đơn thu gom."));
        if (result.SellerId != GetUserId()) return Forbid();
        return Ok(ApiResponse<PickupRequestDto>.Ok(result));
    }

    /// <summary>Hủy đơn (chỉ khi PENDING)</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelRequest(Guid id)
    {
        var (success, message, statusCode) = await _sellerService.CancelRequestAsync(id, GetUserId());
        return statusCode switch
        {
            404 => NotFound(ApiResponse<string>.Fail(message)),
            403 => Forbid(),
            400 => BadRequest(ApiResponse<string>.Fail(message)),
            _ => Ok(ApiResponse<string>.Ok(message))
        };
    }

    /// <summary>Seller xác nhận giá sau khi NV cân</summary>
    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> ConfirmWeigh(Guid id)
    {
        var result = await _pickupService.ConfirmBySellerAsync(id, GetUserId());
        return Ok(ApiResponse<PickupRequestDto>.Ok(result, "Xác nhận giá thành công."));
    }

    /// <summary>Seller từ chối kết quả cân → trả về cho NV</summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> RejectWeigh(Guid id)
    {
        var (success, message, statusCode) = await _sellerService.RejectWeighAsync(id, GetUserId());
        return statusCode switch
        {
            404 => NotFound(ApiResponse<string>.Fail(message)),
            403 => Forbid(),
            400 => BadRequest(ApiResponse<string>.Fail(message)),
            _ => Ok(ApiResponse<string>.Ok(message))
        };
    }

    /// <summary>Seller xác nhận đã nhận tiền → DONE (UC-1.9)</summary>
    [HttpPost("{id:guid}/confirm-payment")]
    public async Task<IActionResult> ConfirmPayment(Guid id)
    {
        await _pickupService.MarkDoneAsync(id, GetUserId());
        return Ok(ApiResponse<string>.Ok("Xác nhận nhận tiền thành công. Đơn hoàn tất!"));
    }

    /// <summary>Danh sách kho vựa gần Seller (cho chọn kho hoặc broadcast)</summary>
    [HttpGet("depots")]
    public async Task<IActionResult> GetNearbyDepots([FromQuery] decimal? lat, [FromQuery] decimal? lng)
    {
        try
        {
            var depots = await _sellerService.GetNearbyDepotsAsync(lat, lng);
            return Ok(ApiResponse<List<NearbyDepotDto>>.Ok(depots));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }

    /// <summary>Seller đánh giá kho sau khi đơn DONE</summary>
    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> ReviewDepot(Guid id, [FromBody] CreateReviewDto dto)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            return BadRequest(ApiResponse<string>.Fail(firstError ?? "Dữ liệu đánh giá không hợp lệ."));
        }

        var (success, message, statusCode) = await _sellerService.ReviewDepotAsync(id, GetUserId(), dto);
        return statusCode switch
        {
            404 => NotFound(ApiResponse<string>.Fail(message)),
            403 => Forbid(),
            400 => BadRequest(ApiResponse<string>.Fail(message)),
            _ => Ok(ApiResponse<string>.Ok(message))
        };
    }

    /// <summary>Thống kê thu nhập Seller</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _sellerService.GetStatsAsync(GetUserId());
        return Ok(ApiResponse<SellerStatsDto>.Ok(stats));
    }
}
