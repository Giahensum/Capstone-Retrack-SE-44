using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public interface IFactoryProfileRepository
{
    Task<Retrack.API.Models.Factory?> FindByOwnerAsync(Guid ownerId, CancellationToken ct);
    void Add(Retrack.API.Models.Factory factory);
    Task<User?> FindOwnerAsync(Guid ownerId, CancellationToken ct);
}
