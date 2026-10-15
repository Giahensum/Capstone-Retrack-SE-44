using Retrack.API.DTOs;

namespace Retrack.API.Services.Interfaces
{
    public interface ISellerService
    {
        Task<SellerProfileDto?> GetProfileAsync(Guid sellerId);
        Task<bool> UpdateProfileAsync(Guid sellerId, UpdateSellerProfileDto dto);
        Task<(bool Success, string Message, int StatusCode)> CancelRequestAsync(Guid requestId, Guid sellerId);
        Task<(bool Success, string Message, int StatusCode)> RejectWeighAsync(Guid requestId, Guid sellerId);
        Task<List<NearbyDepotDto>> GetNearbyDepotsAsync(decimal? lat, decimal? lng);
        Task<(bool Success, string Message, int StatusCode)> ReviewDepotAsync(Guid requestId, Guid sellerId, CreateReviewDto dto);
        Task<SellerStatsDto> GetStatsAsync(Guid sellerId);
    }
}
