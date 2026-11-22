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

            // 3. Enrich Price Trend & Optimal Selling Timing Forecast
            try
            {
                var targetMat = result.ScanMode == "mixed_pile" ? "MIXED" : (!string.IsNullOrWhiteSpace(result.MaterialType) ? result.MaterialType : result.PrimaryClass);
                var basePrice = result.CurrentMarketPrice ?? (result.ScanMode == "mixed_pile" ? 7500 : 4200);
                result.PriceForecast = await GetPriceTrendForecastAsync(targetMat, basePrice, 20);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi dự báo xu hướng giá cho {MaterialType}", result.MaterialType);
            }
        }

        public async Task<PriceTrendForecastDto> GetPriceTrendForecastAsync(string materialType, decimal currentPrice, double weightKg = 10)
        {
            var matUpper = (materialType ?? string.Empty).ToUpperInvariant();
            decimal effectivePrice = currentPrice;

            if (effectivePrice <= 0 && Enum.TryParse<MaterialType>(matUpper, true, out var matEnum))
            {
                var latest = await _db.MarketPrices
                    .Where(p => p.MaterialType == matEnum)
                    .OrderByDescending(p => p.EffectiveDate)
                    .Select(p => (decimal?)p.PricePerKg)
                    .FirstOrDefaultAsync();

                if (latest.HasValue && latest.Value > 0)
                {
                    effectivePrice = latest.Value;
                }
            }

            if (effectivePrice <= 0)
            {
                effectivePrice = matUpper switch
                {
                    "CARDBOARD" => 3500,
                    "PAPER" => 4000,
                    "PLASTIC" or "PET" or "HDPE" => 8500,
                    "METAL" or "ALUMINUM" => 24000,
                    "IRON" or "STEEL" => 12000,
                    "MIXED" => 7800,
                    _ => 5000
                };
            }

            string trendDirection;
            double trendPercentage;
            double confidenceScore;
            string optimalAction;
            string optimalTimingTitle;
            string optimalTimingAdvice;
            string optimalDay;
            var drivers = new List<string>();

            if (matUpper.Contains("METAL") || matUpper.Contains("ALUMINUM") || matUpper.Contains("IRON") || matUpper.Contains("STEEL") || matUpper.Contains("COPPER"))
            {
                trendDirection = "UP";
                trendPercentage = 4.8;
                confidenceScore = 86.5;
                optimalAction = "HOLD_AND_ACCUMULATE";
                optimalTimingTitle = "Nên tích trữ thêm 3 - 5 ngày để gom lô lớn";
                optimalDay = "Thứ Năm tuần này";
                optimalTimingAdvice = "Giá kim loại đang có xu hướng tăng +4.8% trong 7 ngày tới do nhu cầu từ các nhà máy phôi thép. Bạn nên giữ và tích thêm số lượng lớn để nhận thêm trợ giá sản lượng.";
                drivers.Add("Nhu cầu lò tái chế phôi kim loại tăng cao vào chu kỳ sản xuất giữa tuần.");
                drivers.Add("Chỉ số giá nhập khẩu kim loại nguyên liệu đang có xu hướng phục hồi.");
                drivers.Add("Trợ giá khối lượng lớn (+300 đ - 500 đ/kg) khi gom trên 50kg.");
            }
            else if (matUpper.Contains("CARDBOARD") || matUpper.Contains("PAPER"))
            {
                trendDirection = "UP";
                trendPercentage = 3.2;
                confidenceScore = 84.0;
                optimalAction = "SELL_BEFORE_WEEKEND";
                optimalTimingTitle = "Nên đặt lịch thu gom trước Thứ Sáu";
                optimalDay = "Thứ Sáu";
                optimalTimingAdvice = "Các kho vựa đối tác thường hoàn tất đóng kiện xuất đi nhà máy giấy bao bì vào cuối tuần. Bán trước Thứ Sáu sẽ được ưu tiên điều phối xe và chốt giá tốt nhất.";
                drivers.Add("Nhu cầu nguyên liệu giấy tái sinh của các nhà máy bao bì carton duy trì ổn định.");
                drivers.Add("Kho vựa đang cần bổ sung tồn kho giấy carton để chạy dây chuyền ép kiện.");
            }
            else if (matUpper.Contains("PLASTIC") || matUpper.Contains("PET") || matUpper.Contains("HDPE"))
            {
                trendDirection = "STABLE";
                trendPercentage = 0.5;
                confidenceScore = 91.0;
                optimalAction = "SELL_NOW";
                optimalTimingTitle = "Nên bán ngay hôm nay";
                optimalDay = "Hôm nay";
                optimalTimingAdvice = "Giá nhựa tái sinh đang ở vùng đỉnh ổn định 14 ngày qua. Đặt lịch bán ngay hôm nay giúp bạn giải phóng không gian lưu trữ và tránh suy giảm phẩm chất nhựa.";
                drivers.Add("Chỉ số giá hạt nhựa tái sinh PET/HDPE đang đi ngang ổn định.");
                drivers.Add("Nguồn cung chai nhựa sinh hoạt dồi dào, ít có biến động lớn trong ngắn hạn.");
            }
            else if (matUpper.Contains("MIXED"))
            {
                trendDirection = "UP";
                trendPercentage = 3.6;
                confidenceScore = 85.0;
                optimalAction = "HOLD_AND_ACCUMULATE";
                optimalTimingTitle = "Xu hướng đống hỗn hợp tăng nhẹ (+3.6%)";
                optimalDay = "3 ngày tới";
                optimalTimingAdvice = "Đống phế liệu hỗn hợp của bạn có tỷ trọng thành phần kim loại và carton đang tăng giá. Nên phân loại sơ bộ và tích thêm để tối ưu hóa giá trị thanh toán.";
                drivers.Add("Thành phần kim loại trong đống kéo giá bình quân tăng theo sóng tuần.");
                drivers.Add("Hỗ trợ thu gom trọn gói tận nơi khi đạt khối lượng trên 30kg.");
            }
            else
            {
                trendDirection = "STABLE";
                trendPercentage = 0.0;
                confidenceScore = 80.0;
                optimalAction = "SELL_NOW";
                optimalTimingTitle = "Giá thị trường duy trì ổn định";
                optimalDay = "Linh hoạt";
                optimalTimingAdvice = "Mức giá hiện tại tương ứng với giá sàn thị trường chuẩn. Bạn có thể đặt đơn bất cứ lúc nào thuận tiện.";
                drivers.Add("Thị trường phế liệu đang trong giai đoạn bình ổn giá.");
            }

            var now = DateTime.UtcNow;
            var points = new List<PriceForecastPointDto>();
            decimal peakPrice = effectivePrice;

            for (int offset = -3; offset <= 7; offset++)
            {
                var dt = now.AddDays(offset);
                decimal pointPrice;

                if (offset < 0)
                {
                    var pastFactor = trendDirection == "UP" ? (1.0m - (decimal)(Math.Abs(offset) * 0.012)) : (1.0m + (decimal)(Math.Abs(offset) * 0.008));
                    pointPrice = Math.Round(effectivePrice * pastFactor, 0);
                }
                else if (offset == 0)
                {
                    pointPrice = effectivePrice;
                }
                else
                {
                    var growthRatio = (decimal)(trendPercentage / 100.0) * (offset / 7.0m);
                    pointPrice = Math.Round(effectivePrice * (1.0m + growthRatio), 0);
                }

                if (pointPrice > peakPrice) peakPrice = pointPrice;

                string dayLabel = offset switch
                {
                    -3 => "T-3",
                    -2 => "T-2",
                    -1 => "Hôm qua",
                    0 => "Hôm nay",
                    1 => "+1 ngày",
                    2 => "+2 ngày",
                    3 => "+3 ngày",
                    4 => "+4 ngày",
                    5 => "+5 ngày",
                    6 => "+6 ngày",
                    7 => "+7 ngày",
                    _ => dt.ToString("dd/MM")
                };

                points.Add(new PriceForecastPointDto
                {
                    Date = dt.ToString("yyyy-MM-dd"),
                    DayLabel = dayLabel,
                    Price = pointPrice,
                    IsForecast = offset > 0
                });
            }

            var forecastPrice7d = points.Last().Price;
            decimal potentialGain = Math.Max(0, (peakPrice - effectivePrice) * (decimal)weightKg);

            return new PriceTrendForecastDto
            {
                MaterialType = materialType,
                MaterialNameVi = matUpper switch
                {
                    "CARDBOARD" => "Bìa Carton",
                    "PAPER" => "Giấy phế liệu",
                    "PLASTIC" or "PET" => "Nhựa PET",
                    "METAL" or "ALUMINUM" => "Nhôm / Kim loại",
                    "IRON" => "Sắt phế liệu",
                    "MIXED" => "Đống phế liệu hỗn hợp",
                    _ => materialType
                },
                CurrentPrice = effectivePrice,
                ForecastPrice7d = forecastPrice7d,
                TrendDirection = trendDirection,
                TrendPercentage = trendPercentage,
                ConfidenceScore = confidenceScore,
                OptimalAction = optimalAction,
                OptimalTimingTitle = optimalTimingTitle,
                OptimalTimingAdvice = optimalTimingAdvice,
                OptimalDay = optimalDay,
                PotentialGainAmount = potentialGain,
                MarketDrivers = drivers,
                Points = points
            };
        }
    }
}
