using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Seller
{
    [ApiController]
    [Route("api/seller/ai")]
    [Authorize(Roles = "SELLER")]
    public class SellerAiController : ControllerBase
    {
        private readonly ISellerAiService _aiService;
        private readonly ILogger<SellerAiController> _logger;

        public SellerAiController(ISellerAiService aiService, ILogger<SellerAiController> logger)
        {
            _aiService = aiService;
            _logger = logger;
        }

        /// <summary>
        /// Phân loại rác / phế liệu qua ảnh (Hỗ trợ 2 chế độ: single hoặc mixed_pile đa vật thể)
        /// </summary>
        [HttpPost("classify")]
        [RequestSizeLimit(15 * 1024 * 1024)] // 15MB
        public async Task<IActionResult> Classify(IFormFile? file, [FromQuery] bool upload = false, [FromQuery] string mode = "single")
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse<string>.Fail("Vui lòng chọn hoặc chụp ảnh phế liệu cần nhận diện."));

            if (file.Length > 15 * 1024 * 1024)
                return BadRequest(ApiResponse<string>.Fail("Dung lượng ảnh không được vượt quá 15MB."));

            var allowedMime = new[] { "image/jpeg", "image/png", "image/webp", "image/bmp" };
            if (!allowedMime.Contains(file.ContentType.ToLower()))
                return BadRequest(ApiResponse<string>.Fail("Định dạng ảnh không hỗ trợ. Vui lòng tải ảnh JPG, PNG, WebP hoặc BMP."));

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _aiService.ClassifyAsync(stream, file.FileName, file.ContentType, upload, mode);
                var message = mode == "mixed_pile"
                    ? "Nhận diện đống phế liệu hỗn hợp thành công với YOLOv8 đa vật thể."
                    : "Nhận diện phế liệu thành công bằng AI.";
                return Ok(ApiResponse<SellerAiClassifyResponseDto>.Ok(result, message));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Lỗi phân loại AI: {Message}", ex.Message);
                return StatusCode(503, ApiResponse<string>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi nhận diện AI");
                return StatusCode(500, ApiResponse<string>.Fail("Đã xảy ra lỗi khi phân tích ảnh bằng AI. Vui lòng thử lại sau."));
            }
        }

        /// <summary>
        /// Phân loại rác / phế liệu qua chuỗi Base64 (từ Webcam / Camera trực tiếp)
        /// </summary>
        [HttpPost("classify-base64")]
        public async Task<IActionResult> ClassifyBase64([FromBody] AiClassifyBase64RequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ImageBase64))
                return BadRequest(ApiResponse<string>.Fail("Dữ liệu ảnh base64 không được để trống."));

            try
            {
                var result = await _aiService.ClassifyBase64Async(dto.ImageBase64, dto.UploadToCloud, dto.ScanMode);
                var message = dto.ScanMode == "mixed_pile"
                    ? "Nhận diện đống phế liệu hỗn hợp thành công từ Camera trực tiếp."
                    : "Nhận diện phế liệu thành công bằng AI.";
                return Ok(ApiResponse<SellerAiClassifyResponseDto>.Ok(result, message));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Lỗi phân loại AI (base64): {Message}", ex.Message);
                return StatusCode(503, ApiResponse<string>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi nhận diện AI qua base64");
                return StatusCode(500, ApiResponse<string>.Fail("Đã xảy ra lỗi khi phân tích ảnh bằng AI. Vui lòng thử lại sau."));
            }
        }

        /// <summary>
        /// Kiểm tra trạng thái máy chủ AI
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var status = await _aiService.GetStatusAsync();
            return Ok(ApiResponse<object>.Ok(status));
        }

        /// <summary>
        /// Lấy danh mục 12 loại phế liệu AI có thể nhận diện và hướng dẫn tái chế
        /// </summary>
        [HttpGet("classes")]
        public async Task<IActionResult> GetClasses()
        {
            var classes = await _aiService.GetClassesAsync();
            return Ok(ApiResponse<object>.Ok(classes));
        }

        /// <summary>
        /// Dự báo xu hướng giá & Gợi ý thời điểm bán tối ưu (Optimal Selling Timing)
        /// </summary>
        [HttpGet("price-forecast")]
        public async Task<IActionResult> GetPriceForecast(
            [FromQuery] string material = "CARDBOARD",
            [FromQuery] decimal price = 0,
            [FromQuery] double weight = 10)
        {
            var forecast = await _aiService.GetPriceTrendForecastAsync(material, price, weight);
            return Ok(ApiResponse<PriceTrendForecastDto>.Ok(forecast, "Lấy dự báo xu hướng giá và thời điểm bán thành công."));
        }
    }
}
