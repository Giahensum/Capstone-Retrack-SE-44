using Retrack.API.Models;
using Retrack.API.Models.Enums;
namespace Retrack.API.Repositories.Interfaces;

public interface IPickupRequestRepository : IRepository<PickupRequest>
{
    Task<IEnumerable<PickupRequest>> GetBySellerIdAsync(Guid sellerId);
    Task<IEnumerable<PickupRequest>> GetByDepotIdAsync(Guid depotId, PickupRequestStatus? status = null);
}


