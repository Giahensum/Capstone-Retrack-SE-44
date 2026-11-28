namespace Retrack.API.Repositories.Interfaces;

using Retrack.API.Models;

public interface IFactoryRepository
{
    Task<Factory?> GetByOwnerIdAsync(Guid ownerId);
    Task AddAsync(Factory factory);
}
