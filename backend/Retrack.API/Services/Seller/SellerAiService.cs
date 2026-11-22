using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Models.Enums;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Seller
{
    public class SellerAiService : ISellerAiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _db;
        private readonly ICloudinaryService _cloudinary;
        private readonly ILogger<SellerAiService> _logger;

        public SellerAiService(
            HttpClient httpClient,
            IConfiguration configuration,
            AppDbContext db,
            ICloudinaryService cloudinary,
            ILogger<SellerAiService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _db = db;
            _cloudinary = cloudinary;
            _logger = logger;
        }

        private string GetAiBaseUrl()
        {
            return _configuration["AiService:Url"] ?? "http://127.0.0.1:5005";
        }

        public async Task<SellerAiClassifyResponseDto> ClassifyAsync(
            Stream imageStream,
            string fileName,
            string contentType,
            bool uploadToCloud = false,
            string scanMode = "single")
        {
            // Copy stream for Cloudinary if needed
            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                await imageStream.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }

            var aiBaseUrl = GetAiBaseUrl();
            _logger.LogInformation("Gửi ảnh {FileName} (chế độ: {Mode}) tới AI microservice tại {Url}...", fileName, scanMode, aiBaseUrl);

            using var content = new MultipartFormDataContent();
            var byteContent = new ByteArrayContent(imageBytes);
            byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            content.Add(byteContent, "file", fileName);

            HttpResponseMessage response;
            try
            {
                var queryMode = Uri.EscapeDataString(scanMode ?? "single");
                response = await _httpClient.PostAsync($"{aiBaseUrl}/api/ai/classify?mode={queryMode}", content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối tới AI microservice tại {Url}", aiBaseUrl);
                throw new InvalidOperationException(
                    "Dịch vụ AI thị giác máy tính đang tạm thời không phản hồi. Vui lòng kiểm tra dịch vụ AI trên cổng 5005.",
                    ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errText = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("AI microservice trả về lỗi HTTP {Status}: {Body}", response.StatusCode, errText);
                throw new InvalidOperationException($"Dịch vụ AI báo lỗi: {errText}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<SellerAiClassifyResponseDto>(json, options)
                         ?? throw new InvalidOperationException("Không thể giải mã phản hồi từ dịch vụ AI.");

            result.ScanMode = scanMode ?? "single";

            // Tra cứu giá thị trường thực tế từ Database cho cả đơn vật thể và đống hỗn hợp
            await EnrichWithMarketPrice(result);

            // Tải lên Cloudinary nếu người dùng yêu cầu
            if (uploadToCloud)
            {
                try
                {
                    using var cloudStream = new MemoryStream(imageBytes);
                    result.UploadedUrl = await _cloudinary.UploadImageAsync(cloudStream, fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Upload Cloudinary kèm theo thất bại, bỏ qua");
                }
            }

            return result;
        }

        public async Task<SellerAiClassifyResponseDto> ClassifyBase64Async(string imageBase64, bool uploadToCloud = false, string scanMode = "single")
        {
            var aiBaseUrl = GetAiBaseUrl();
            var payload = new { image_base64 = imageBase64, scan_mode = scanMode ?? "single" };
            var jsonPayload = JsonSerializer.Serialize(payload);
            using var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync($"{aiBaseUrl}/api/ai/classify-base64", httpContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối tới AI microservice tại {Url}", aiBaseUrl);
                throw new InvalidOperationException(
                    "Dịch vụ AI thị giác máy tính đang tạm thời không phản hồi. Vui lòng kiểm tra dịch vụ AI trên cổng 5005.",
                    ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errText = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Dịch vụ AI báo lỗi: {errText}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<SellerAiClassifyResponseDto>(json, options)
                         ?? throw new InvalidOperationException("Không thể giải mã phản hồi từ dịch vụ AI.");

            result.ScanMode = scanMode ?? "single";

            await EnrichWithMarketPrice(result);

            if (uploadToCloud)
            {
                try
                {
                    var cleanB64 = imageBase64;
                    if (cleanB64.Contains("base64,")) cleanB64 = cleanB64.Split("base64,")[1];
                    var rawBytes = Convert.FromBase64String(cleanB64);
                    using var cloudStream = new MemoryStream(rawBytes);
                    result.UploadedUrl = await _cloudinary.UploadImageAsync(cloudStream, $"ai_scan_{DateTime.UtcNow.Ticks}.jpg");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Upload Cloudinary từ base64 thất bại");
                }
            }

            return result;
        }

        public async Task<object> GetStatusAsync()
        {
            var aiBaseUrl = GetAiBaseUrl();
            try
            {
                var res = await _httpClient.GetAsync($"{aiBaseUrl}/api/ai/health");
                if (!res.IsSuccessStatusCode)
                {
                    res = await _httpClient.GetAsync($"{aiBaseUrl}/api/ai/status");
                }

                if (res.IsSuccessStatusCode)
                {
                    var body = await res.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<object>(body) ?? new { status = "ok" };
                }
                return new { status = "error", code = (int)res.StatusCode };
            }
            catch (Exception ex)
            {
                return new { status = "offline", message = ex.Message };
            }
        }

        public async Task<object> GetClassesAsync()
        {
            var aiBaseUrl = GetAiBaseUrl();
            try
            {
                var res = await _httpClient.GetAsync($"{aiBaseUrl}/api/ai/classes");
                if (res.IsSuccessStatusCode)
                {
                    var body = await res.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<object>(body) ?? new { };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi lấy danh sách classes từ AI server");
            }

            return new { classes = Array.Empty<string>() };
        }

        private async Task EnrichWithMarketPrice(SellerAiClassifyResponseDto result)
        {
            // 1. Enrich for primary material (Single Mode)
            if (!string.IsNullOrWhiteSpace(result.MaterialType) &&
                Enum.TryParse<MaterialType>(result.MaterialType, true, out var matEnum))
            {
                try
                {
                    var latestPrice = await _db.MarketPrices
                        .Where(p => p.MaterialType == matEnum)
                        .OrderByDescending(p => p.EffectiveDate)
                        .Select(p => (decimal?)p.PricePerKg)
                        .FirstOrDefaultAsync();

                    if (latestPrice.HasValue && latestPrice.Value > 0)
                    {
                        result.CurrentMarketPrice = latestPrice.Value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Lỗi truy vấn giá thị trường cho {MaterialType}", result.MaterialType);
                }
            }

            // 2. Enrich for each detected object in Mixed Pile Mode
            if (result.DetectedObjects != null && result.DetectedObjects.Count > 0)
            {
                foreach (var obj in result.DetectedObjects)
                {
                    if (!string.IsNullOrWhiteSpace(obj.MaterialType) &&
                        Enum.TryParse<MaterialType>(obj.MaterialType, true, out var objEnum))
                    {
                        try
                        {
                            var price = await _db.MarketPrices
                                .Where(p => p.MaterialType == objEnum)
                                .OrderByDescending(p => p.EffectiveDate)
                                .Select(p => (decimal?)p.PricePerKg)
                                .FirstOrDefaultAsync();

                            if (price.HasValue && price.Value > 0)
                            {
                                obj.UnitPrice = price.Value;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Lỗi truy vấn giá thị trường cho đối tượng con {MaterialType}", obj.MaterialType);
                        }
                    }
                }
            }
        }
    }
}
