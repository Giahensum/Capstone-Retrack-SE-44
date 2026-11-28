namespace Retrack.API.Repositories.Interfaces;

public interface IFactoryAttachmentRepository
{
    Task<bool> DepotMayReadBatchProofAsync(Guid actorId, Guid ownerId, string url, CancellationToken ct);
}
