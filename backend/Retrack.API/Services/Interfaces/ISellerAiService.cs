using Retrack.API.DTOs;

namespace Retrack.API.Services.Interfaces
{
    public interface ISellerAiService
    {
        Task<SellerAiClassifyResponseDto> ClassifyAsync(Stream imageStream, string fileName, string contentType, bool uploadToCloud = false, string mode = "single");
        Task<SellerAiClassifyResponseDto> ClassifyBase64Async(string imageBase64, bool uploadToCloud = false, string mode = "single");
        Task<object> GetStatusAsync();
        Task<object> GetClassesAsync();
        Task<MaterialPriceForecastDto> GetPriceForecastAsync(string? materialType, decimal? userWeightKg = null);
        Task<List<MaterialPriceForecastDto>> GetAllPriceForecastsAsync();
    }
}
