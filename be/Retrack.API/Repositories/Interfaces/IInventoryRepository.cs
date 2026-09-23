using Retrack.API.Models;
namespace Retrack.API.Repositories.Interfaces;

public interface IInventoryRepository : IRepository<InventoryBatch>
{
    Task<IEnumerable<InventoryBatch>> GetByDepotIdAsync(Guid depotId);
}
