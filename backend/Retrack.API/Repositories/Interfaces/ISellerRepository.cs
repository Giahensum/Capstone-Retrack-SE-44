using Retrack.API.DTOs;
using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces
{
    public interface ISellerRepository
    {
        Task<User?> GetSellerProfileAsync(Guid sellerId);
        Task UpdateSellerProfileAsync(User user);
        Task<PickupRequest?> GetPickupRequestByIdAsync(Guid requestId);
        Task<PickupRequest?> LockPickupAsync(Guid requestId);
        void AddNotice(Notification notice);
        Task UpdatePickupRequestAsync(PickupRequest request);
        Task<bool> HasSellerReviewedRequestAsync(Guid requestId);
        Task AddReviewAsync(SellerDepotReview review);
        Task<List<NearbyDepotDto>> GetNearbyDepotsAsync(decimal? lat, decimal? lng);
        Task<SellerStatsDto> GetSellerStatsAsync(Guid sellerId);
        Task SaveChangesAsync();
    }
}
