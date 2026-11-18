using Retrack.API.DTOs.Employee;
using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public sealed record DepotCoordinates(decimal? Latitude, decimal? Longitude);
public sealed record PickupPoolCandidate(PickupPoolItemDto Item, Guid? TargetDepotId, decimal? Latitude, decimal? Longitude);
public sealed record PickupDetailCandidate(EmployeePickupDetailDto Detail, Guid? TargetDepotId, decimal? Latitude, decimal? Longitude);

public interface IEmployeePickupRepository
{
    Task<Guid?> PrimaryDepotAsync(Guid userId, CancellationToken ct);
    Task<bool> HasAreasAsync(Guid depotId, CancellationToken ct);
    Task<bool> IsApprovedAsync(Guid depotId, CancellationToken ct);
    Task<DepotCoordinates?> CoordinatesAsync(Guid depotId, CancellationToken ct);
    Task<(List<PickupPoolItemDto> Items, int Total)> AssignedAsync(Guid userId, int page, CancellationToken ct);
    Task<List<PickupPoolCandidate>> ManualPoolAsync(Guid depotId, CancellationToken ct);
    Task<object> ActiveAsync(Guid userId, int page, CancellationToken ct);
    Task<PickupDetailCandidate?> DetailAsync(Guid userId, Guid depotId, Guid pickupId, CancellationToken ct);
    Task<PickupRequest?> FindAvailableAsync(Guid depotId, Guid pickupId, CancellationToken ct);
    Task<int> ClaimAsync(Guid userId, Guid depotId, Guid pickupId, DateTime now, CancellationToken ct);
}
