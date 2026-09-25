using Retrack.API.DTOs.Factory;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Interfaces;

public interface IFactoryDashboardService
{
    Task<ServiceResult<DashboardResponse>> GetAsync(Guid userId, CancellationToken ct);
}
