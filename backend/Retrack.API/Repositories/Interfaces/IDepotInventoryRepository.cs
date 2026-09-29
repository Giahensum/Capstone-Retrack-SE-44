namespace Retrack.API.Repositories.Interfaces;

public interface IDepotInventoryRepository
{
    Task<List<DepotReceivedWeight>> ReceivedAsync(Guid depotId);
    Task<List<DepotAllocatedWeight>> AllocatedAsync(Guid depotId);
}
