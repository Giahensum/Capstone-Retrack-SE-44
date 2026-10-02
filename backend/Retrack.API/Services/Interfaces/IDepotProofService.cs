namespace Retrack.API.Services.Interfaces;

public interface IDepotProofService
{
    Task<string> UploadAsync(Guid ownerId, Guid depotId, IFormFile file);
}
