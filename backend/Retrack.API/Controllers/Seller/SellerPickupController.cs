using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Models;
using Retrack.API.Services;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Seller;

[ApiController]
[Route("api/seller/pickups")]
[Authorize(Roles = "SELLER")]
public class SellerPickupController : ControllerBase
{
    private readonly IPickupService _pickupService;
    private readonly ICloudinaryService _cloudinary;
    private readonly AppDbContext _db;

    public SellerPickupController(IPickupService pickupService, ICloudinaryService cloudinary, AppDbContext db)
    {
        _pickupService = pickupService;
        _cloudinary = cloudinary;
        _db = db;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Upload ảnh phế liệu lên Cloudinary (multipart/form-data)</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<string>.Fail("Vui lòng chọn ảnh."));

        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/heic" };
        if (!allowed.Contains(file.ContentType.ToLower()))
            return BadRequest(ApiResponse<string>.Fail("Chỉ chấp nhận ảnh JPG, PNG, WebP, HEIC."));

        using var stream = file.OpenReadStream();
        var url = await _cloudinary.UploadImageAsync(stream, file.FileName);
        return Ok(ApiResponse<object>.Ok(new { url }, "Upload thành công."));
    }

    /// <summary>Tạo yêu cầu thu gom phế liệu</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePickupRequestDto dto)
    {
        var result = await _pickupService.CreateAsync(GetUserId(), dto);
        return Ok(ApiResponse<PickupRequestDto>.Ok(result, "Tạo yêu cầu thành công."));
    }

    /// <summary>Lấy danh sách đơn của Seller (có filter theo status)</summary>
    [HttpGet]
    public async Task<IActionResult> GetMyRequests([FromQuery] string? status)
    {
        var requests = await _pickupService.GetBySellerAsync(GetUserId());
        if (!string.IsNullOrEmpty(status))
            requests = requests.Where(r => r.Status == status.ToUpper()).ToList();
        return Ok(ApiResponse<List<PickupRequestDto>>.Ok(requests));
    }

    /// <summary>Lấy chi tiết 1 đơn</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _pickupService.GetByIdAsync(id, GetUserId());
        if (result == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy."));
        if (result.SellerId != GetUserId()) return Forbid();
        return Ok(ApiResponse<PickupRequestDto>.Ok(result));
    }

    /// <summary>Hủy đơn (chỉ khi PENDING)</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelRequest(Guid id)
    {
        var req = await _db.PickupRequests.FindAsync(id);
        if (req == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy."));
        if (req.SellerId != GetUserId()) return Forbid();
        if (req.Status != "PENDING")
            return BadRequest(ApiResponse<string>.Fail("Chỉ hủy được đơn ở trạng thái chờ xử lý."));
        req.Status = "CANCELLED";
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Đã hủy đơn thành công."));
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
        var req = await _db.PickupRequests.FindAsync(id);
        if (req == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy."));
        if (req.SellerId != GetUserId()) return Forbid();
        if (req.Status != "WEIGHED") return BadRequest(ApiResponse<string>.Fail("Đơn chưa ở trạng thái chờ xác nhận."));
        req.Status = "SCHEDULED"; // trả về cho NV cân lại
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Đã từ chối. Nhân viên sẽ cân lại."));
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
        var depots = await _db.Depots.Include(d => d.Owner)
            .Where(d => d.Owner.IsActive)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Address,
                d.Latitude,
                d.Longitude,
                OwnerName = d.Owner.FullName,
                AvgRating = _db.SellerDepotReviews
                    .Where(r => r.DepotId == d.Id && r.Rating != null)
                    .Average(r => (double?)r.Rating) ?? 0,
                TotalDone = _db.PickupRequests
                    .Count(r => r.TargetDepotId == d.Id && r.Status == "DONE"),
            }).ToListAsync();

        var prices = await _db.MarketPrices
            .GroupBy(p => p.MaterialType)
            .Select(g => g.OrderByDescending(p => p.EffectiveDate).FirstOrDefault())
            .ToListAsync();

        var priceDict = prices.Where(p => p != null).ToDictionary(p => p.MaterialType.ToString(), p => p.PricePerKg);

        // If seller has location, calculate distance and sort
        if (lat.HasValue && lng.HasValue)
        {
            var sorted = depots
                .Select(d => new
                {
                    d.Id, d.Name, d.Address, d.Latitude, d.Longitude,
                    d.OwnerName, d.AvgRating, d.TotalDone,
                    Prices = priceDict,
                    DistanceKm = d.Latitude.HasValue && d.Longitude.HasValue
                        ? CalculateDistanceKm((double)lat.Value, (double)lng.Value,
                            (double)d.Latitude.Value, (double)d.Longitude.Value)
                        : (double?)null
                })
                .OrderBy(d => d.DistanceKm ?? double.MaxValue)
                .ToList();
            return Ok(ApiResponse<object>.Ok(sorted));
        }

        var resultWithoutLocation = depots.Select(d => new
        {
            d.Id, d.Name, d.Address, d.Latitude, d.Longitude,
            d.OwnerName, d.AvgRating, d.TotalDone,
            Prices = priceDict,
        }).ToList();

        return Ok(ApiResponse<object>.Ok(resultWithoutLocation));
    }

    /// <summary>Seller đánh giá kho sau khi đơn DONE</summary>
    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> ReviewDepot(Guid id, [FromBody] CreateReviewDto dto)
    {
        var req = await _db.PickupRequests.FindAsync(id);
        if (req == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy."));
        if (req.SellerId != GetUserId()) return Forbid();
        if (req.Status != "DONE") return BadRequest(ApiResponse<string>.Fail("Đơn chưa hoàn thành."));
        if (req.TargetDepotId == null) return BadRequest(ApiResponse<string>.Fail("Đơn không có kho vựa."));

        var existing = await _db.SellerDepotReviews.AnyAsync(r => r.PickupRequestId == id);
        if (existing) return BadRequest(ApiResponse<string>.Fail("Đã đánh giá rồi."));

        _db.SellerDepotReviews.Add(new SellerDepotReview
        {
            PickupRequestId = id,
            DepotId = req.TargetDepotId.Value,
            Rating = dto.Rating,
            Comment = dto.Comment
        });
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Đánh giá thành công."));
    }

    /// <summary>Thống kê thu nhập Seller</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var sellerId = GetUserId();
        var requests = await _db.PickupRequests
            .Where(r => r.SellerId == sellerId)
            .ToListAsync();

        var stats = new
        {
            Total = requests.Count,
            Pending = requests.Count(r => r.Status == "PENDING"),
            InProgress = requests.Count(r => r.Status is "SCHEDULED" or "WEIGHED" or "SELLER_CONFIRMED" or "AWAITING_PAYMENT"),
            Done = requests.Count(r => r.Status == "DONE"),
            TotalIncome = requests.Where(r => r.Status == "DONE").Sum(r => r.NetAmount),
            MonthlyIncome = requests
                .Where(r => r.Status == "DONE" && r.CreatedAt.Month == DateTime.UtcNow.Month && r.CreatedAt.Year == DateTime.UtcNow.Year)
                .Sum(r => r.NetAmount),
        };
        return Ok(ApiResponse<object>.Ok(stats));
    }

    /// <summary>Haversine formula to calculate distance in km</summary>
    private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
