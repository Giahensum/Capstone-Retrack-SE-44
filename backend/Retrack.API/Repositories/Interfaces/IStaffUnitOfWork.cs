namespace Retrack.API.Repositories.Interfaces;

/// <summary>Giữ chung context, transaction và tracking cho nghiệp vụ Employee/Driver.</summary>
public interface IStaffUnitOfWork
{
    Task<IStaffTransaction> BeginAsync(CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    void ClearTracking();
}

public interface IStaffTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
