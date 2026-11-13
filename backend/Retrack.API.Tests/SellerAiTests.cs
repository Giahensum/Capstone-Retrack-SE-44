using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Retrack.API.Controllers.Seller;
using Retrack.API.DTOs;
using Retrack.API.Services.Interfaces;
using Xunit;

namespace Retrack.API.Tests
{
    public class FakeSellerAiService : ISellerAiService
    {
        public SellerAiClassifyResponseDto? MockResponse { get; set; }
        public bool ShouldThrow { get; set; }
        public string ErrorMessage { get; set; } = "Dịch vụ AI không phản hồi";

        public Task<SellerAiClassifyResponseDto> ClassifyAsync(Stream imageStream, string fileName, string contentType, bool uploadToCloud = false)
        {
            if (ShouldThrow) throw new InvalidOperationException(ErrorMessage);
            return Task.FromResult(MockResponse ?? new SellerAiClassifyResponseDto());
        }

        public Task<SellerAiClassifyResponseDto> ClassifyBase64Async(string imageBase64, bool uploadToCloud = false)
        {
            if (ShouldThrow) throw new InvalidOperationException(ErrorMessage);
            return Task.FromResult(MockResponse ?? new SellerAiClassifyResponseDto());
        }

        public Task<object> GetStatusAsync()
        {
            return Task.FromResult<object>(new { status = "healthy", model = "EfficientNet-B4" });
        }

        public Task<object> GetClassesAsync()
        {
            return Task.FromResult<object>(new { classes = new[] { "cardboard", "plastic" } });
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
    }
}
