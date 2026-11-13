using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
namespace Retrack.API.Repositories.Interfaces;

public interface IDepotVerificationRepository
{
    Task LockOwnerAsync(Guid ownerId);
    Task<Depot?> FindDepotAsync(Guid id);
    void AddDepot(Depot depot);
    Task LockDepotAsync(Guid id);
    Task<bool> ActiveAdminAsync(Guid id);
    Task<DepotVerificationSubmission?> FindAsync(Guid id);
    Task<bool> HasPendingAsync(Guid depotId);
    Task<int> RecentCountAsync(Guid depotId);
    void Add(DepotVerificationSubmission submission);
    void AddNotice(Notification notice);
    Task<PagedResult<VerificationSubmissionDto>> ListAsync(Guid? depotId, DepotQuery query);
    Task<(Guid OwnerId, DepotVerificationDocument Document)?> FileAsync(Guid id);
}
