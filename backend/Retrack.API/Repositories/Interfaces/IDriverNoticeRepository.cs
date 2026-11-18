namespace Retrack.API.Repositories.Interfaces;

public interface IDriverNoticeRepository
{
    Task<object> ListAsync(Guid userId, int page, CancellationToken ct);
    Task<int> MarkReadAsync(Guid userId, Guid id, CancellationToken ct);
    Task<object> LegacyTripsAsync(Guid userId, CancellationToken ct);
}
