using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public interface IDepotPaymentRepository
{
    Task<PickupRequest?> LockAsync(Guid requestId);
    void AddFee(PlatformTransaction fee);
}
