using Retrack.API.DTOs.Factory;

namespace Retrack.API.Repositories.Interfaces;

public interface IFactoryReportRepository
{
    Task<DashboardResponse> DashboardAsync(Guid factoryId, DateTime periodStart, DateTime monthStart, CancellationToken ct);
}
