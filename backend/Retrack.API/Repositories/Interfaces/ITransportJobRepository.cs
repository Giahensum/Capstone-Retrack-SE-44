using Retrack.API.Models;
namespace Retrack.API.Repositories.Interfaces;

public interface ITransportJobRepository : IRepository<TransportJob>
{
    Task<IEnumerable<TransportJob>> GetAvailableJobsAsync(Guid depotId);
}


