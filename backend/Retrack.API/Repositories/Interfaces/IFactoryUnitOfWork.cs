namespace Retrack.API.Repositories.Interfaces;

public interface IFactoryUnitOfWork
{
    Task<IFactoryTransaction> BeginAsync(CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}

public interface IFactoryTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
