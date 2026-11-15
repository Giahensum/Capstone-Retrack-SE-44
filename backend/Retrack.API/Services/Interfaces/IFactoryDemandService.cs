using Retrack.API.DTOs.Factory;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Interfaces;

public interface IFactoryDemandService
{
    Task<ServiceResult<PageResponse<DemandResponse>>> ListAsync(Guid userId, PageQuery query, CancellationToken ct);
    Task<ServiceResult<DemandResponse>> CreateAsync(Guid userId, DemandRequest request, CancellationToken ct);
    Task<ServiceResult<DemandResponse>> UpdateAsync(Guid userId, Guid id, DemandRequest request, CancellationToken ct);
    Task<ServiceResult<DemandResponse>> ToggleAsync(Guid userId, Guid id, DemandStatusRequest request, CancellationToken ct);
    Task<ServiceResult> DeleteAsync(Guid userId, Guid id, CancellationToken ct);
}
