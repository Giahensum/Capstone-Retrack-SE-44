using Retrack.API.DTOs;

namespace Retrack.API.Services.Interfaces
{
    public interface ISellerAiService
    {
        Task<SellerAiClassifyResponseDto> ClassifyAsync(Stream imageStream, string fileName, string contentType, bool uploadToCloud = false);
        Task<SellerAiClassifyResponseDto> ClassifyBase64Async(string imageBase64, bool uploadToCloud = false);
        Task<object> GetStatusAsync();
        Task<object> GetClassesAsync();
    }
}
