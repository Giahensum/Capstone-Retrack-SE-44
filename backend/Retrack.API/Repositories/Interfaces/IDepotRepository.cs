using Retrack.API.Models;
namespace Retrack.API.Repositories.Interfaces;

public interface IDepotRepository : IRepository<Depot>
{
    Task<Depot?> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<Depot>> GetNearbyAsync(decimal lat, decimal lng, double radiusKm);
}
