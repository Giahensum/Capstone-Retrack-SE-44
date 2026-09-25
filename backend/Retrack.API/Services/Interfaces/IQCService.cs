using Retrack.API.DTOs.Factory;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Interfaces;

public interface IQCService
{
    Task<ServiceResult<WeighResponse>> WeighAsync(Guid userId, Guid id, WeighRequest request, CancellationToken ct);
    Task<ServiceResult<QualityResponse>> QualityAsync(Guid userId, Guid id, QualityRequest request, CancellationToken ct);
}
