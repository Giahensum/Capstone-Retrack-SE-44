using Retrack.API.DTOs.Employee;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Employee;

public sealed record EmployeePoolResult(List<PickupPoolItemDto> Items, bool Automatic, int Total);

public sealed class EmployeePickupService(IEmployeePickupRepository repository, EmployeeReportingService reports,
    IStaffUnitOfWork work, INotificationService notify)
{
    private async Task<Guid> GetDepotIdAsync(Guid userId, CancellationToken ct) =>
        await repository.PrimaryDepotAsync(userId, ct)
            ?? throw new UnauthorizedAccessException("Tài khoản không liên kết với kho đang hoạt động.");
    public async Task<EmployeeDashboardDto> DashboardAsync(Guid userId, CancellationToken ct)
    {
        await GetDepotIdAsync(userId, ct);
        return await reports.DashboardAsync(userId, ct);
    }
    public async Task<EmployeePoolResult> PoolAsync(Guid userId, int page, CancellationToken ct)
    {
        var depotId = await GetDepotIdAsync(userId, ct);
        if (page is < 1 or > 100000) throw new ArgumentException("Trang không hợp lệ.");
        if (await repository.HasAreasAsync(depotId, ct))
        {
            var result = await repository.AssignedAsync(userId, page, ct);
            return new(result.Items, true, result.Total);
        }
        var depot = await repository.CoordinatesAsync(depotId, ct);
        var list = await repository.ManualPoolAsync(depotId, ct);
        var filtered = list.Where(p => p.TargetDepotId != null || WithinRange(depot, p.Latitude, p.Longitude))
            .Select(p => p.Item).ToList();
        return new(filtered, false, filtered.Count);
    }
    public async Task<object> ActiveAsync(Guid userId, int page, CancellationToken ct)
    {
        if (page is < 1 or > 100000) throw new ArgumentException("Trang không hợp lệ.");
        await GetDepotIdAsync(userId, ct);
        return await repository.ActiveAsync(userId, page, ct);
    }
    public async Task<EmployeePickupDetailDto> DetailAsync(Guid userId, Guid pickupId, CancellationToken ct)
    {
        var depotId = await GetDepotIdAsync(userId, ct);
        var item = await repository.DetailAsync(userId, depotId, pickupId, ct)
            ?? throw new KeyNotFoundException("Đơn không tồn tại hoặc không còn thuộc phạm vi của bạn.");
        if (item.TargetDepotId == null)
        {
            var depot = await repository.CoordinatesAsync(depotId, ct);
            if (!HasCoordinates(depot, item.Latitude, item.Longitude))
                throw new KeyNotFoundException("Đơn không thuộc phạm vi của bạn.");
            if (!WithinRange(depot, item.Latitude, item.Longitude))
                throw new KeyNotFoundException("Đơn ngoài bán kính phục vụ 20 km của kho.");
        }
        return item.Detail;
    }
    public async Task<PickupPoolItemDto> AcceptAsync(Guid userId, Guid pickupId, CancellationToken ct)
    {
        var depotId = await GetDepotIdAsync(userId, ct);
        if (await repository.HasAreasAsync(depotId, ct))
            throw new DepotConflictException("Kho đang phân công tự động. Hãy mở danh sách đơn được giao cho bạn.");
        if (!await repository.IsApprovedAsync(depotId, ct))
            throw new DepotConflictException("Kho chưa được Admin xác minh hồ sơ để nhận giao dịch mới.");
        var pickup = await repository.FindAvailableAsync(depotId, pickupId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn thu gom.");
        if (pickup.TargetDepotId == null)
        {
            var depot = await repository.CoordinatesAsync(depotId, ct);
            if (!HasCoordinates(depot, pickup.Latitude, pickup.Longitude))
                throw new ArgumentException("Không thể xác định khoảng cách giữa kho và địa chỉ thu gom.");
            if (!WithinRange(depot, pickup.Latitude, pickup.Longitude))
                throw new ArgumentException("Đơn hàng nằm ngoài bán kính phục vụ 20 km của kho.");
        }
        await using var transaction = await work.BeginAsync(ct);
        if (await repository.ClaimAsync(userId, depotId, pickupId, DateTime.UtcNow, ct) == 0)
            throw new DepotConflictException("Đơn đã được nhận bởi nhân viên khác hoặc không còn khả dụng.");
        // Giữ cùng context/transaction với dịch vụ thông báo hiện có.
        await notify.SendPickupAsync(pickup.SellerId, pickup.Id, "Đơn thu gom đã được nhận",
            "Nhân viên kho đã nhận đơn của bạn và sẽ đến sớm.");
        await transaction.CommitAsync(ct);
        return new(pickup.Id, pickup.Seller.FullName, pickup.Seller.Phone, pickup.Address,
            pickup.Latitude, pickup.Longitude, pickup.PreferredDatetime, pickup.Description, pickup.RequestImageUrl, pickup.CreatedAt);
    }
    private static bool HasCoordinates(DepotCoordinates? depot, decimal? latitude, decimal? longitude) =>
        depot?.Latitude.HasValue == true && depot.Longitude.HasValue && latitude.HasValue && longitude.HasValue;
    private static bool WithinRange(DepotCoordinates? depot, decimal? latitude, decimal? longitude) =>
        HasCoordinates(depot, latitude, longitude) && GeoUtils.CalculateDistanceKm((double)depot!.Latitude!.Value,
            (double)depot.Longitude!.Value, (double)latitude!.Value, (double)longitude!.Value) <= 20.0;
}
