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
            bool uploadToCloud = false)
        {
            // Copy stream for Cloudinary if needed
            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                await imageStream.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }

            var aiBaseUrl = GetAiBaseUrl();
            _logger.LogInformation("Gửi ảnh {FileName} tới AI microservice tại {Url}...", fileName, aiBaseUrl);

            using var content = new MultipartFormDataContent();
            var byteContent = new ByteArrayContent(imageBytes);
            byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            content.Add(byteContent, "file", fileName);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync($"{aiBaseUrl}/api/ai/classify", content);
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

            // Tra cứu giá thị trường thực tế từ Database
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

        public async Task<SellerAiClassifyResponseDto> ClassifyBase64Async(string imageBase64, bool uploadToCloud = false)
        {
            var aiBaseUrl = GetAiBaseUrl();
            var payload = new { image_base64 = imageBase64 };
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

        public async Task<MaterialPriceForecastDto> GetPriceForecastAsync(string? materialType, decimal? userWeightKg = null)
        {
            var norm = NormalizeMaterialType(materialType);
            var meta = GetMaterialMetadata(norm);

            // 1. Lấy giá niêm yết hiện tại từ MarketPrices
            decimal marketPrice = meta.DefaultPrice;
            if (Enum.TryParse<MaterialType>(norm, true, out var matEnum))
            {
                var dbMarketPrice = await _db.MarketPrices
                    .Where(p => p.MaterialType == matEnum)
                    .OrderByDescending(p => p.EffectiveDate)
                    .Select(p => (decimal?)p.PricePerKg)
                    .FirstOrDefaultAsync();

                if (dbMarketPrice.HasValue && dbMarketPrice.Value > 0)
                {
                    marketPrice = dbMarketPrice.Value;
                }
            }

            // 2. Lấy dữ liệu nhu cầu từ các nhà máy đang hoạt động (Factory Demands)
            var activeDemands = await _db.FactoryDemands
                .Where(d => d.IsActive && d.MaterialType == norm)
                .Select(d => new { d.RequiredWeightKg, d.MaxPricePerKg })
                .ToListAsync();

            decimal totalDemandKg = activeDemands.Sum(d => d.RequiredWeightKg);
            decimal? maxFactoryPrice = activeDemands.Max(d => d.MaxPricePerKg);

            // 3. Lấy dữ liệu giá thu mua thực tế của các kho vựa (Depot Buying Prices)
            var depotPrices = await _db.DepotBuyingPrices
                .Where(p => p.IsActive && p.MaterialType == norm)
                .Select(p => p.PricePerKg)
                .ToListAsync();

            int activeDepots = depotPrices.Count;
            decimal avgDepotPrice = activeDepots > 0 ? depotPrices.Average() : marketPrice;

            // Tổng hợp giá cơ sở hiện tại (Ưu tiên bình quân có trọng số giữa thị trường và kho vựa)
            decimal currentBaseline = activeDepots > 0
                ? Math.Round((marketPrice * 0.35m + avgDepotPrice * 0.65m) / 50m) * 50m
                : marketPrice;

            // 4. Tính toán áp lực cầu (Demand-Pull Momentum)
            decimal demandDrift = 0m;
            if (maxFactoryPrice.HasValue && maxFactoryPrice.Value > currentBaseline)
            {
                decimal premium = (maxFactoryPrice.Value - currentBaseline) / currentBaseline;
                demandDrift = Math.Min(0.012m, premium * 0.08m);
            }
            else if (totalDemandKg >= 8000)
            {
                demandDrift = 0.006m;
            }
            else if (totalDemandKg > 0)
            {
                demandDrift = 0.002m;
            }
            else
            {
                demandDrift = -0.0025m;
            }

            // 5. Xây dựng trục thời gian: 7 ngày quá khứ (-7..-1), hôm nay (0) và 7 ngày dự báo (+1..+7)
            var today = DateTime.UtcNow.Date;
            var timeline = new List<PriceForecastPointDto>();
            int seed = Math.Abs(norm.GetHashCode()) % 7;

            // Quá khứ: -7 đến -1
            for (int i = -7; i < 0; i++)
            {
                var dt = today.AddDays(i);
                decimal cycle = 0.014m * (decimal)Math.Sin((i + seed) * 0.95);
                decimal histPrice = currentBaseline * (1m + demandDrift * i + cycle);
                histPrice = Math.Round(histPrice / 50m) * 50m;

                timeline.Add(new PriceForecastPointDto
                {
                    Date = dt.ToString("yyyy-MM-dd"),
                    DayLabel = GetDayOfWeekLabel(dt, false),
                    Price = histPrice,
                    MinPrice = histPrice,
                    MaxPrice = histPrice,
                    IsForecast = false
                });
            }

            // Hôm nay: 0
            timeline.Add(new PriceForecastPointDto
            {
                Date = today.ToString("yyyy-MM-dd"),
                DayLabel = "Hôm nay",
                Price = currentBaseline,
                MinPrice = currentBaseline,
                MaxPrice = currentBaseline,
                IsForecast = false
            });

            // Tương lai: +1 đến +7
            decimal highestFuturePrice = currentBaseline;
            int peakDayOffset = 0;

            for (int i = 1; i <= 7; i++)
            {
                var dt = today.AddDays(i);
                decimal damp = 1.0m / (1.0m + 0.035m * i);
                decimal cycle = 0.012m * (decimal)Math.Sin(i * 0.85);
                decimal projected = currentBaseline * (1m + demandDrift * i * damp + cycle);
                projected = Math.Round(projected / 50m) * 50m;

                decimal spread = 0.015m * (decimal)Math.Sqrt(i);
                decimal minP = Math.Round(projected * (1m - spread) / 50m) * 50m;
                decimal maxP = Math.Round(projected * (1m + spread) / 50m) * 50m;

                if (projected > highestFuturePrice)
                {
                    highestFuturePrice = projected;
                    peakDayOffset = i;
                }

                timeline.Add(new PriceForecastPointDto
                {
                    Date = dt.ToString("yyyy-MM-dd"),
                    DayLabel = GetDayOfWeekLabel(dt, true),
                    Price = projected,
                    MinPrice = minP,
                    MaxPrice = maxP,
                    IsForecast = true
                });
            }

            decimal priceAtDay7 = timeline.Last().Price;
            decimal projectedChangePct = currentBaseline > 0
                ? Math.Round((priceAtDay7 - currentBaseline) / currentBaseline * 100m, 1)
                : 0m;

            string trend;
            if (projectedChangePct >= 1.5m) trend = "UPWARD";
            else if (projectedChangePct <= -1.5m) trend = "DOWNWARD";
            else trend = "STABLE";

            // 6. Gợi ý thời điểm bán tối ưu dựa trên logic kinh tế học và khối lượng
            decimal effectiveWeight = userWeightKg ?? 15m;
            string recCode;
            string recTitle;
            string recDetail;
            string optWindow;
            decimal profitBoostPct = 0m;

            if (effectiveWeight < 20m)
            {
                recCode = "HOLD_ACCUMULATE";
                recTitle = "Nên gom thêm để đạt mốc 20 – 30 kg nhận ưu đãi lớn";
                recDetail = $"Khối lượng hiện tại (~{effectiveWeight:0} kg) chưa tối ưu cước vận chuyển. Khi gom thêm để đạt tối thiểu 20 kg (hoặc 30 kg), bạn sẽ được miễn phí điều phối xe tận nơi và nhận trợ giá số lượng lớn (+2.5% đến +5%).";
                optWindow = $"Trong 3 – 5 ngày tới ({today.AddDays(3):dd/MM} – {today.AddDays(5):dd/MM})";
                profitBoostPct = Math.Max(3.5m, projectedChangePct + 4.5m);
            }
            else if (trend == "UPWARD" && peakDayOffset >= 2)
            {
                recCode = "HOLD_ACCUMULATE";
                recTitle = "Nên gom thêm 3 – 5 ngày để đón đỉnh giá thị trường";
                var peakDate = today.AddDays(peakDayOffset);
                recDetail = $"Lực cầu từ các nhà máy tái chế đang tăng mạnh (tổng cầu đang mở: {totalDemandKg:N0} kg). Dự báo giá sẽ đạt đỉnh khoảng ~{highestFuturePrice:N0} đ/kg vào ngày {peakDate:dd/MM}.";
                optWindow = $"Khoảng ngày {today.AddDays(Math.Max(1, peakDayOffset - 1)):dd/MM} – {today.AddDays(Math.Min(7, peakDayOffset + 1)):dd/MM}";
                profitBoostPct = Math.Round((highestFuturePrice - currentBaseline) / currentBaseline * 100m, 1);
            }
            else if (trend == "DOWNWARD" || peakDayOffset <= 1)
            {
                recCode = "SELL_NOW";
                recTitle = "Thời điểm vàng: Bán ngay hôm nay để chốt giá cao nhất";
                recDetail = $"Giá phế liệu hiện tại ({currentBaseline:N0} đ/kg) đang ở vùng đỉnh chu kỳ ngắn hạn. Bán ngay hôm nay giúp bạn bảo toàn lợi nhuận tối đa và tránh rủi ro giá điều chỉnh giảm ({projectedChangePct}%).";
                optWindow = "Trong vòng 24 – 48 giờ tới";
                profitBoostPct = 0m;
            }
            else
            {
                recCode = "NEUTRAL";
                recTitle = "Giá thị trường ổn định — Có thể bán bất kỳ lúc nào";
                recDetail = $"Biên độ giá dao động dưới 1.5%. Các kho vựa đối tác đang thu mua đều đặn, bạn có thể chủ động đặt lịch theo thời gian rảnh của gia đình.";
                optWindow = "Linh hoạt trong 7 ngày tới";
                profitBoostPct = 0m;
            }

            return new MaterialPriceForecastDto
            {
                MaterialType = norm,
                MaterialNameVi = meta.NameVi,
                Unit = "kg",
                CurrentPrice = currentBaseline,
                ProjectedPrice7d = priceAtDay7,
                ProjectedChangePct = projectedChangePct,
                Trend = trend,
                RecommendationCode = recCode,
                RecommendationTitle = recTitle,
                RecommendationDetail = recDetail,
                OptimalTimeWindow = optWindow,
                EstimatedProfitIncreasePct = profitBoostPct,
                FactoryDemandWeightKg = totalDemandKg,
                ActiveDepotsBuying = activeDepots,
                Timeline = timeline
            };
        }

        public async Task<List<MaterialPriceForecastDto>> GetAllPriceForecastsAsync()
        {
            var majorMaterials = new[] { "CARDBOARD", "PET", "ALUMINUM", "IRON", "COPPER", "PAPER", "HDPE" };
            var list = new List<MaterialPriceForecastDto>();
            foreach (var mat in majorMaterials)
            {
                list.Add(await GetPriceForecastAsync(mat, 25m));
            }
            return list;
        }

        private static string NormalizeMaterialType(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "CARDBOARD";
            var upper = raw.Trim().ToUpperInvariant();
            if (upper.Contains("CARTON") || upper.Contains("BÌA") || upper.Contains("BIA")) return "CARDBOARD";
            if (upper.Contains("PET") || upper.Contains("CHAI")) return "PET";
            if (upper.Contains("HDPE") || upper.Contains("CAN")) return "HDPE";
            if (upper.Contains("NHÔM") || upper.Contains("NHOM") || upper.Contains("ALU")) return "ALUMINUM";
            if (upper.Contains("SẮT") || upper.Contains("SAT") || upper.Contains("THÉP") || upper.Contains("STEEL") || upper.Contains("IRON")) return "IRON";
            if (upper.Contains("ĐỒNG") || upper.Contains("DONG") || upper.Contains("COPPER")) return "COPPER";
            if (upper.Contains("GIẤY") || upper.Contains("GIAY") || upper.Contains("PAPER")) return "PAPER";
            return upper;
        }

        private static (string NameVi, decimal DefaultPrice) GetMaterialMetadata(string norm) => norm switch
        {
            "CARDBOARD" => ("Bìa Carton & Thùng giấy", 4000m),
            "PET" => ("Nhựa PET tái chế (Chai nước)", 9000m),
            "HDPE" => ("Nhựa HDPE gia dụng (Can dầu)", 12000m),
            "ALUMINUM" => ("Nhôm phế liệu (Vỏ lon bia)", 28000m),
            "IRON" => ("Sắt & Thép phế liệu", 7500m),
            "COPPER" => ("Đồng phế liệu (Dây điện)", 75000m),
            "PAPER" => ("Giấy & Sách báo cũ", 3000m),
            _ => (norm, 5000m)
        };

        private static string GetDayOfWeekLabel(DateTime dt, bool isFuture)
        {
            string day = dt.DayOfWeek switch
            {
                DayOfWeek.Monday => "T2",
                DayOfWeek.Tuesday => "T3",
                DayOfWeek.Wednesday => "T4",
                DayOfWeek.Thursday => "T5",
                DayOfWeek.Friday => "T6",
                DayOfWeek.Saturday => "T7",
                DayOfWeek.Sunday => "CN",
                _ => ""
            };
            return $"{day} ({dt:dd/MM})";
        }

        private async Task EnrichWithMarketPrice(SellerAiClassifyResponseDto result)
        {
            if (string.IsNullOrWhiteSpace(result.MaterialType)) return;

            if (Enum.TryParse<MaterialType>(result.MaterialType, true, out var matEnum))
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
        }
    }
}
