using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Retrack.API.Controllers.Seller;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Seller;
using Xunit;

namespace Retrack.API.Tests
{
    public class FakeSellerAiService : ISellerAiService
    {
        public SellerAiClassifyResponseDto? MockResponse { get; set; }
        public bool ShouldThrow { get; set; }
        public string ErrorMessage { get; set; } = "Dịch vụ AI không phản hồi";

        public Task<SellerAiClassifyResponseDto> ClassifyAsync(Stream imageStream, string fileName, string contentType, bool uploadToCloud = false, string mode = "single")
        {
            if (ShouldThrow) throw new InvalidOperationException(ErrorMessage);
            var resp = MockResponse ?? new SellerAiClassifyResponseDto();
            resp.ScanMode = mode;
            return Task.FromResult(resp);
        }

        public Task<SellerAiClassifyResponseDto> ClassifyBase64Async(string imageBase64, bool uploadToCloud = false, string mode = "single")
        {
            if (ShouldThrow) throw new InvalidOperationException(ErrorMessage);
            var resp = MockResponse ?? new SellerAiClassifyResponseDto();
            resp.ScanMode = mode;
            return Task.FromResult(resp);
        }

        public Task<object> GetStatusAsync()
        {
            return Task.FromResult<object>(new { status = "healthy", model = "EfficientNet-B4" });
        }

        public Task<object> GetClassesAsync()
        {
            return Task.FromResult<object>(new { classes = new[] { "cardboard", "plastic" } });
        }

        public Task<MaterialPriceForecastDto> GetPriceForecastAsync(string? materialType, decimal? userWeightKg = null)
        {
            return Task.FromResult(new MaterialPriceForecastDto
            {
                MaterialType = materialType ?? "CARDBOARD",
                MaterialNameVi = "Bìa Carton & Thùng giấy",
                CurrentPrice = 4200m,
                ProjectedPrice7d = 4400m,
                ProjectedChangePct = 4.8m,
                Trend = "UPWARD",
                RecommendationCode = "HOLD_ACCUMULATE",
                RecommendationTitle = "Nên gom thêm 3 - 5 ngày để đón đỉnh giá",
                OptimalTimeWindow = "Trong 3 - 5 ngày tới",
                Timeline = new List<PriceForecastPointDto>
                {
                    new() { Date = "2026-11-23", DayLabel = "Hôm nay", Price = 4200m, IsForecast = false },
                    new() { Date = "2026-11-24", DayLabel = "Mai", Price = 4250m, IsForecast = true },
                    new() { Date = "2026-11-25", DayLabel = "T4", Price = 4350m, IsForecast = true }
                }
            });
        }

        public Task<List<MaterialPriceForecastDto>> GetAllPriceForecastsAsync()
        {
            return Task.FromResult(new List<MaterialPriceForecastDto>
            {
                new() { MaterialType = "CARDBOARD", CurrentPrice = 4200m, Trend = "UPWARD" },
                new() { MaterialType = "PET", CurrentPrice = 9000m, Trend = "STABLE" }
            });
        }
    }

    public class SellerAiTests
    {
        private readonly FakeSellerAiService _aiService;
        private readonly SellerAiController _controller;

        public SellerAiTests()
        {
            _aiService = new FakeSellerAiService();
            _controller = new SellerAiController(_aiService, NullLogger<SellerAiController>.Instance);
        }

        [Fact]
        public async Task Classify_ReturnsBadRequest_WhenFileIsNull()
        {
            var result = await _controller.Classify(null);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<string>>(badRequest.Value);
            Assert.False(apiResponse.Success);
            Assert.Contains("chọn hoặc chụp ảnh", apiResponse.Message);
        }

        [Fact]
        public async Task Classify_ReturnsBadRequest_WhenFileIsInvalidMime()
        {
            var content = "dummy text file";
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            var file = new FormFile(stream, 0, bytes.Length, "file", "test.txt")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };

            var result = await _controller.Classify(file);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<string>>(badRequest.Value);
            Assert.False(apiResponse.Success);
            Assert.Contains("Định dạng ảnh không hỗ trợ", apiResponse.Message);
        }

        [Fact]
        public async Task Classify_ReturnsOk_WhenValidImageProvided()
        {
            var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 }; // JPG header
            var stream = new MemoryStream(bytes);
            var file = new FormFile(stream, 0, bytes.Length, "file", "photo.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };

            _aiService.MockResponse = new SellerAiClassifyResponseDto
            {
                PrimaryClass = "cardboard",
                NameVi = "Bìa Carton & Thùng giấy",
                MaterialType = "CARDBOARD",
                Confidence = 85.5,
                BoxStatus = "YOLO",
                IsRecyclable = true,
                PriceRange = "3.000 - 5.500 đ/kg",
                SuggestedDescription = "Có phế liệu bìa carton cần bán."
            };

            var result = await _controller.Classify(file);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<SellerAiClassifyResponseDto>>(okResult.Value);

            Assert.True(apiResponse.Success);
            Assert.NotNull(apiResponse.Data);
            Assert.Equal("cardboard", apiResponse.Data.PrimaryClass);
            Assert.Equal("Bìa Carton & Thùng giấy", apiResponse.Data.NameVi);
            Assert.Equal(85.5, apiResponse.Data.Confidence);
        }

        [Fact]
        public async Task Classify_SupportsMixedPileMode_WithMultiBoundingBoxes()
        {
            var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
            var stream = new MemoryStream(bytes);
            var file = new FormFile(stream, 0, bytes.Length, "file", "mixed_pile.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };

            _aiService.MockResponse = new SellerAiClassifyResponseDto
            {
                ScanMode = "mixed_pile",
                PrimaryClass = "mixed_pile",
                NameVi = "Đống phế liệu hỗn hợp đa vật thể",
                MaterialType = "MIXED",
                Confidence = 92.0,
                DetectedObjects = new List<AiDetectedObjectDto>
                {
                    new() { Id = "box_1", ClassKey = "cardboard", NameVi = "Bìa Carton", PhysicalWeightPercent = 55.0, Color = "#10b981", BoundingBox = new AiBoundingBoxDto { X1 = 20, Y1 = 30, Width = 250, Height = 200 } },
                    new() { Id = "box_2", ClassKey = "plastic", NameVi = "Chai nhựa PET", PhysicalWeightPercent = 30.0, Color = "#3b82f6", BoundingBox = new AiBoundingBoxDto { X1 = 280, Y1 = 50, Width = 150, Height = 180 } },
                    new() { Id = "box_3", ClassKey = "metal", NameVi = "Lon nhôm", PhysicalWeightPercent = 15.0, Color = "#f59e0b", BoundingBox = new AiBoundingBoxDto { X1 = 150, Y1 = 240, Width = 120, Height = 110 } }
                }
            };

            var result = await _controller.Classify(file, upload: false, mode: "mixed_pile");
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<SellerAiClassifyResponseDto>>(okResult.Value);

            Assert.True(apiResponse.Success);
            Assert.NotNull(apiResponse.Data);
            Assert.Equal("mixed_pile", apiResponse.Data.ScanMode);
            Assert.Equal(3, apiResponse.Data.DetectedObjects.Count);
            Assert.Equal("Bìa Carton", apiResponse.Data.DetectedObjects[0].NameVi);
            Assert.Equal(55.0, apiResponse.Data.DetectedObjects[0].PhysicalWeightPercent);
        }

        [Fact]
        public async Task Classify_Returns503_WhenAiServiceFails()
        {
            var bytes = new byte[] { 0xFF, 0xD8, 0xFF };
            var stream = new MemoryStream(bytes);
            var file = new FormFile(stream, 0, bytes.Length, "file", "photo.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };

            _aiService.ShouldThrow = true;
            _aiService.ErrorMessage = "Dịch vụ AI không phản hồi";

            var result = await _controller.Classify(file);
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(503, statusResult.StatusCode);

            var apiResponse = Assert.IsType<ApiResponse<string>>(statusResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Contains("Dịch vụ AI không phản hồi", apiResponse.Message);
        }

        [Fact]
        public async Task ClassifyBase64_ReturnsBadRequest_WhenEmpty()
        {
            var result = await _controller.ClassifyBase64(new AiClassifyBase64RequestDto { ImageBase64 = "" });
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<string>>(badRequest.Value);
            Assert.False(apiResponse.Success);
        }

        [Fact]
        public async Task GetStatus_ReturnsStatusFromService()
        {
            var result = await _controller.GetStatus();
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.True(apiResponse.Success);
        }

        [Fact]
        public async Task GetClasses_ReturnsClassesFromService()
        {
            var result = await _controller.GetClasses();
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.True(apiResponse.Success);
        }

        [Fact]
        public async Task GetPriceForecast_ReturnsForecastData()
        {
            var result = await _controller.GetPriceForecast("CARDBOARD", 25m);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<MaterialPriceForecastDto>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.NotNull(apiResponse.Data);
            Assert.Equal("CARDBOARD", apiResponse.Data.MaterialType);
            Assert.Equal("UPWARD", apiResponse.Data.Trend);
            Assert.Equal("HOLD_ACCUMULATE", apiResponse.Data.RecommendationCode);
            Assert.NotEmpty(apiResponse.Data.Timeline);
        }

        [Fact]
        public async Task GetAllPriceForecasts_ReturnsMultipleForecasts()
        {
            var result = await _controller.GetAllPriceForecasts();
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<List<MaterialPriceForecastDto>>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.NotNull(apiResponse.Data);
            Assert.Equal(2, apiResponse.Data.Count);
        }

        [Fact]
        public async Task Service_GetPriceForecast_ComputesEconomicLogic_Accurately()
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            // Seed market price
            db.MarketPrices.Add(new MarketPrice
            {
                MaterialType = MaterialType.CARDBOARD,
                PricePerKg = 4000m,
                EffectiveDate = DateTime.UtcNow
            });

            // Seed active factory demand with higher price (creating demand-pull)
            db.FactoryDemands.Add(new FactoryDemand
            {
                Id = Guid.NewGuid(),
                FactoryId = Guid.NewGuid(),
                MaterialType = "CARDBOARD",
                RequiredWeightKg = 15000,
                MinPricePerKg = 4200,
                MaxPricePerKg = 5200, // +30% above market
                IsActive = true,
                Deadline = DateTime.UtcNow.AddDays(10)
            });

            // Seed depot buying prices
            db.DepotBuyingPrices.Add(new DepotBuyingPrice
            {
                DepotId = Guid.NewGuid(),
                MaterialType = "CARDBOARD",
                PricePerKg = 4200m,
                IsActive = true
            });

            await db.SaveChangesAsync();

            var config = new ConfigurationBuilder().Build();
            var service = new SellerAiService(
                new HttpClient(),
                config,
                db,
                null!,
                NullLogger<SellerAiService>.Instance);

            // Test 1: User with small weight (< 20kg) -> should recommend HOLD_ACCUMULATE to optimize shipping
            var forecastSmall = await service.GetPriceForecastAsync("CARDBOARD", 10m);
            Assert.NotNull(forecastSmall);
            Assert.Equal("CARDBOARD", forecastSmall.MaterialType);
            Assert.Equal("HOLD_ACCUMULATE", forecastSmall.RecommendationCode);
            Assert.Contains("20 – 30 kg", forecastSmall.RecommendationTitle);
            Assert.True(forecastSmall.EstimatedProfitIncreasePct > 0);
            Assert.Equal(15, forecastSmall.Timeline.Count); // 7 past + today + 7 future

            // Test 2: User with sufficient weight (25kg) under strong factory demand -> should show UPWARD trend
            var forecastSufficient = await service.GetPriceForecastAsync("CARDBOARD", 25m);
            Assert.NotNull(forecastSufficient);
            Assert.Equal("UPWARD", forecastSufficient.Trend);
            Assert.True(forecastSufficient.ProjectedPrice7d > forecastSufficient.CurrentPrice);
            Assert.Equal(15000, forecastSufficient.FactoryDemandWeightKg);
            Assert.Equal(1, forecastSufficient.ActiveDepotsBuying);

            // Verify timeline formatting and confidence intervals
            var futurePoint = forecastSufficient.Timeline.First(p => p.IsForecast);
            Assert.True(futurePoint.MaxPrice >= futurePoint.Price);
            Assert.True(futurePoint.MinPrice <= futurePoint.Price);
        }
    }
}
