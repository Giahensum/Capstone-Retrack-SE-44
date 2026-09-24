using Retrack.API.DTOs.Factory;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Interfaces;

public interface IFactoryProfileService
{
    Task<ServiceResult<ProfileResponse>> GetAsync(Guid userId, CancellationToken ct);
    Task<ServiceResult<ProfileUpdatedResponse>> UpdateAsync(Guid userId, ProfileRequest request, CancellationToken ct);
}
