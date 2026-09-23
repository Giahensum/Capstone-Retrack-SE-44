using Retrack.API.Models;
namespace Retrack.API.Repositories.Interfaces;

public interface IBatchOrderRepository : IRepository<BatchOrder>
{
    Task<IEnumerable<BatchOrder>> GetByFactoryIdAsync(Guid factoryId);
}
