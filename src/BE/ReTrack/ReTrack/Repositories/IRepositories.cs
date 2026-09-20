using ReTrack.Models;

namespace ReTrack.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByIdAsync(Guid id);
        Task<User> CreateAsync(User user);
        Task<User> UpdateAsync(User user);
        Task<List<User>> GetAllAsync();
    }

    public interface IPickupRequestRepository
    {
        Task<PickupRequest?> GetByIdAsync(Guid id);
        Task<List<PickupRequest>> GetBySellerIdAsync(Guid sellerId);
        Task<List<PickupRequest>> GetByDepotIdAsync(Guid depotId);
        Task<List<PickupRequest>> GetPendingAsync();
        Task<PickupRequest> CreateAsync(PickupRequest request);
        Task<PickupRequest> UpdateAsync(PickupRequest request);
        Task<List<PickupRequest>> GetAllAsync(int page, int pageSize);
        Task<int> CountAsync();
    }
}
