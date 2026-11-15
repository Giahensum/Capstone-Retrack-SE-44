namespace Retrack.API.Repositories.Interfaces;

/// <summary>Các repository trong cùng request dùng chung một context và giao dịch.</summary>
public interface IDepotUnitOfWork
{
    Task<IDepotTransaction> BeginAsync();
    Task SaveAsync();
}

public interface IDepotTransaction : IAsyncDisposable
{
    Task CommitAsync();
}
