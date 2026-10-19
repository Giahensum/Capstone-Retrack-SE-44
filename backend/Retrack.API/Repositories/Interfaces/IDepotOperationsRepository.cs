using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
namespace Retrack.API.Repositories.Interfaces;
public interface IDepotOperationsRepository
{
    Task<PagedResult<DepotNoticeDto>> NoticesAsync(Guid ownerId, Guid depotId, DepotQuery query);
    Task<int> UnreadAsync(Guid ownerId, Guid depotId);
    Task<bool> ReadAsync(Guid ownerId, Guid depotId, Guid id);
    Task<PagedResult<DepotDriverEventDto>> EventsAsync(Guid depotId, Guid batchId, DepotQuery query);
    Task<DriverDeliveryEvent?> FindEventAsync(Guid depotId, Guid id);
    Task<PagedResult<IncidentFollowupDto>> FollowupsAsync(Guid eventId, DepotQuery query);
    Task<DepotIncidentFollowup?> FindFollowupAsync(Guid id);
    void AddFollowup(DepotIncidentFollowup row);
}
