using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Services.Driver;

public static class DriverJobNotices
{
    // Repository chỉ thêm thông báo vào tracking; bên gọi lưu trong transaction hiện tại.
    public static Task QueueAsync(IDriverDeliveryRepository repository, Guid depotId, Guid jobId, CancellationToken ct = default) =>
        repository.QueueAvailableAsync(depotId, jobId, ct);
}
