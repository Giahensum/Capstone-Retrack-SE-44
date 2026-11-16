using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Services.Driver;

public sealed class DriverNoticeService(IDriverNoticeRepository repository, DriverJobService scope)
{
    public async Task<object> ListAsync(Guid userId, int page, CancellationToken ct)
    {
        if (page is < 1 or > 100000) throw new ArgumentException("Trang không hợp lệ.");
        await scope.RequireAsync(userId, ct);
        return await repository.ListAsync(userId, page, ct);
    }
    public async Task<bool> MarkReadAsync(Guid userId, Guid id, CancellationToken ct)
    {
        await scope.RequireAsync(userId, ct);
        return await repository.MarkReadAsync(userId, id, ct) != 0;
    }
    public async Task<object> LegacyTripsAsync(Guid userId, CancellationToken ct)
    {
        await scope.RequireAsync(userId, ct);
        return await repository.LegacyTripsAsync(userId, ct);
    }
}
