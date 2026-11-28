using Retrack.API.DTOs.Driver;
using Retrack.API.DTOs;

namespace Retrack.API.Repositories.Interfaces;

public sealed record DriverJobScope(Guid DepotId, Guid OwnerId);

public interface IDriverJobRepository
{
    Task<bool> HasActiveStaffAsync(Guid userId, CancellationToken ct);
    Task<PagedResult<DriverJobDto>> ListAsync(Guid userId, bool mine, int page, CancellationToken ct);
    Task<DriverJobDto?> DetailAsync(Guid userId, Guid id, CancellationToken ct);
    Task<DriverJobScope?> ScopeAsync(Guid userId, Guid id, CancellationToken ct);
    Task LockDepotAsync(Guid depotId, CancellationToken ct);
    Task<int> ClaimAsync(Guid userId, Guid id, DateTime now, CancellationToken ct);
    Task<bool> IsAcceptedRetryAsync(Guid userId, Guid id, CancellationToken ct);
}
