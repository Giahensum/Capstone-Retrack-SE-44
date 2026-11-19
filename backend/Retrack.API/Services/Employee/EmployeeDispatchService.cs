using Retrack.API.Repositories.Interfaces;
using Retrack.API.Models;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.Employee;

public sealed record AvailabilityInput(string Status, string? Reason);

public sealed class EmployeeDispatchService(IEmployeeDispatchRepository repository, IStaffUnitOfWork work)
{
    // Giới hạn hỗ trợ từ địa điểm Seller tới tâm vùng nhận, không phải bán kính check-in.
    public const double MaxSupportDistanceMeters = 5000;
    // Các tiến trình điều phối dùng cùng khóa; check-in vẫn khóa bản ghi pickup.
    public Task LockAsync(CancellationToken ct) => repository.LockAsync(ct);

    public async Task<object> MineAsync(Guid userId, CancellationToken ct)
    {
        if (!await repository.HasStaffAsync(userId, null, ct)) throw new DepotForbiddenException();
        return await repository.MineAsync(userId, DateTime.UtcNow, ct);
    }

    public async Task ChangeAvailabilityAsync(Guid userId, Guid shiftId, AvailabilityInput input, CancellationToken ct)
    {
        if (input.Status is not ("AVAILABLE" or "FULL" or "STOPPED"))
            throw new ArgumentException("Trạng thái phải là AVAILABLE, FULL hoặc STOPPED.");
        var reason = input.Reason?.Trim();
        if (input.Status != "AVAILABLE" && (reason == null || reason.Length is < 10 or > 1000))
            throw new ArgumentException("Hãy nhập lý do từ 10 đến 1.000 ký tự.");
        await using var tx = await work.BeginAsync(ct);
        await LockAsync(ct);
        var now = DateTime.UtcNow;
        var shift = await repository.FindShiftAsync(shiftId, userId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy ca của bạn.");
        var area = await repository.AreaAsync(shift.AreaId, ct);
        if (!await repository.HasStaffAsync(userId, area.DepotId, ct)) throw new DepotForbiddenException();
        if (shift.StartsAt > now || shift.EndsAt <= now) throw new DepotConflictException("Ca chưa bắt đầu hoặc đã kết thúc.");
        if (shift.Availability == "STOPPED" && input.Status != "STOPPED")
            throw new DepotConflictException("Ca đã ngừng. Chủ kho cần bố trí ca mới.");
        if (shift.Availability == input.Status) { await tx.CommitAsync(ct); return; }
        shift.Availability = input.Status;
        shift.Reason = input.Status == "AVAILABLE" ? null : reason;
        shift.UpdatedAt = now;
        await work.SaveAsync(ct);
        // Chuyển ngay đơn chưa check-in; đơn đang giữ hàng vẫn thuộc người hiện tại.
        var ids = await repository.UnstartedAsync(area.DepotId, userId, ct);
        if (input.Status != "AVAILABLE")
            foreach (var id in ids)
            {
                await AssignLockedAsync(id, now, reason!, ct);
                // Cập nhật tải công việc trong cùng transaction trước khi chọn người cho đơn tiếp theo.
                await work.SaveAsync(ct);
            }
        var owner = await repository.OwnerIdAsync(area.DepotId, ct);
        repository.AddNotification(new Notification { UserId = owner, DepotId = area.DepotId, Category = "PICKUP", Title = "Cập nhật ca thu gom",
            Message = $"Nhân viên {userId}: {input.Status}. {reason}" });
        await work.SaveAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task DispatchAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        // Ngày nghiệp vụ Việt Nam. Đơn hẹn ngày mai không bị giao từ hôm nay.
        var endDay = now.AddHours(7).Date.AddDays(1).AddHours(-7);
        var ids = await repository.DueAsync(endDay, ct);
        foreach (var id in ids)
        {
            await using var tx = await work.BeginAsync(ct);
            await LockAsync(ct);
            await AssignLockedAsync(id, now, "Phân công tự động theo khu vực và ca", ct);
            await work.SaveAsync(ct);
            await tx.CommitAsync(ct);
            work.ClearTracking();
        }
    }

    private async Task AssignLockedAsync(Guid id, DateTime now, string reason, CancellationToken ct)
    {
        var pickup = await repository.LockPickupAsync(id, ct);
        if (pickup == null) return;
        if (pickup.Status is not ("PENDING" or "SCHEDULED")
            || !string.IsNullOrEmpty(pickup.CheckinImageUrl)
            || await repository.HasCheckInAsync(id, ct)) return;
        // Owner xử lý ngoại lệ (ví dụ thiếu tọa độ). Giữ phân công tay khi ca/membership còn hợp lệ;
        // không giữ khi nhân viên báo đầy/ngừng ca hoặc hết giờ để TV3 tiếp tục điều phối lại.
        if (await repository.KeepManualAsync(pickup, now, ct)) return;
        var areas = await repository.AreasAsync(pickup.TargetDepotId, ct);
        if (pickup.TargetDepotId == null && pickup.Latitude.HasValue && pickup.Longitude.HasValue)
        {
            var depotIds = areas.Select(a => a.DepotId).Distinct().ToArray();
            var depots = await repository.DepotLocationsAsync(depotIds, ct);
            var withinRange = depots.Where(d => d.Latitude.HasValue && d.Longitude.HasValue
                && CollectionValidation.Distance((double)pickup.Latitude, (double)pickup.Longitude,
                    (double)d.Latitude, (double)d.Longitude) <= 20000).Select(d => d.Id).ToHashSet();
            areas = areas.Where(a => withinRange.Contains(a.DepotId)).ToList();
        }
        var home = pickup.Latitude.HasValue && pickup.Longitude.HasValue
            ? areas.Select(a => new { Area = a, Distance = CollectionValidation.Distance((double)pickup.Latitude,
                (double)pickup.Longitude, a.Latitude, a.Longitude) })
                // Kho được chọn vẫn tiếp nhận ngoài vùng; vùng là ưu tiên điều phối, không phải hàng rào nhận đơn.
                .Where(a => pickup.TargetDepotId.HasValue || a.Distance <= a.Area.RadiusMeters).OrderBy(a => a.Distance)
                .ThenBy(a => a.Area.Id).Select(a => a.Area).FirstOrDefault() : null;
        var old = pickup.AcceptedCollectorId;
        EmployeeDispatchShift? chosen = null;
        if (home != null)
        {
            var depotId = home.DepotId;
            // Đơn broadcast trong vùng phục vụ được đưa vào hàng điều phối của kho đó,
            // kể cả lúc chưa có người rảnh, để Owner nhìn thấy và xử lý.
            if (pickup.TargetDepotId == null) { pickup.TargetDepotId = depotId; pickup.UpdatedAt = now; }
            var areaIds = areas.Where(a => a.DepotId == depotId).Select(a => a.Id).ToArray();
            var shifts = await repository.AvailableShiftsAsync(areaIds, depotId, now, ct);
            double DistanceToShift(EmployeeDispatchShift shift)
            {
                var recipientArea = areas.Single(a => a.Id == shift.AreaId);
                return CollectionValidation.Distance((double)pickup.Latitude!.Value,
                    (double)pickup.Longitude!.Value, recipientArea.Latitude, recipientArea.Longitude);
            }
            bool CoversOrSupports(EmployeeDispatchShift shift) => shift.IsAutomatic || shift.AreaId == home.Id
                || DistanceToShift(shift) <= MaxSupportDistanceMeters;
            var counts = await repository.WorkloadAsync(depotId, ct);
            var day = now.AddHours(7).Date.AddHours(-7);
            var excluded = await repository.ExcludedAsync(id, day, ct);
            if (old.HasValue && shifts.Any(s => s.EmployeeId == old && CoversOrSupports(s)
                && (pickup.PreferredDatetime == null || pickup.PreferredDatetime < s.EndsAt)))
            {
                await TouchQueue(id, "ASSIGNED", now, ct);
                return;
            }
            // Không quay lại người từng bàn giao đơn trong ngày, tránh vòng lặp.
            chosen = shifts.Where(s => s.EmployeeId != old && !excluded.Contains(s.EmployeeId)
                && CoversOrSupports(s)
                && (s.IsAutomatic || counts.GetValueOrDefault(s.EmployeeId) < s.MaxPendingOrders)
                && (pickup.PreferredDatetime == null || pickup.PreferredDatetime < s.EndsAt))
                .OrderBy(s => s.AreaId == home.Id ? 0 : 1)
                .ThenBy(s => DistanceToShift(s))
                .ThenBy(s => s.StartsAt)
                .ThenBy(s => s.Id).FirstOrDefault();
            if (chosen != null) pickup.TargetDepotId = depotId;
        }
        var next = chosen?.EmployeeId;
        var waitingReason = home == null ? "Đơn vẫn chờ kho tiếp nhận; cần Owner bố trí vùng/ca hoặc kiểm tra tọa độ."
            : "Chưa có nhân viên cùng kho còn ca và khả năng nhận đơn. Chờ nhân viên sẵn sàng hoặc Owner xử lý ngoại lệ.";
        var priorQueue = await repository.QueueAsync(id, ct);
        if (next != old)
        {
            pickup.AcceptedCollectorId = next;
            pickup.Status = next == null ? "PENDING" : "SCHEDULED";
            pickup.UpdatedAt = now;
            repository.AddEvent(new EmployeeDispatchEvent { PickupId = id, FromEmployeeId = old,
                ToEmployeeId = next, ShiftId = chosen?.Id, Reason = reason, CreatedAt = now });
            if (old.HasValue) Notify(old.Value, id, "Đơn đã được điều phối lại", reason, pickup.TargetDepotId);
            if (next.HasValue)
            {
                Notify(next.Value, id, "Đơn thu gom được phân công", pickup.Address, pickup.TargetDepotId);
                Notify(pickup.SellerId, id, "Đã phân công nhân viên thu gom", "Nhân viên kho sẽ liên hệ để thu gom.", pickup.TargetDepotId);
            }
        }
        if (next == null && priorQueue?.Reason != waitingReason && pickup.TargetDepotId.HasValue)
        {
            var owner = await repository.OwnerIdAsync(pickup.TargetDepotId, ct);
            Notify(owner, id, "Đơn đang chờ điều phối", waitingReason, pickup.TargetDepotId);
        }
        await TouchQueue(id, next == null ? waitingReason : "ASSIGNED", now, ct);
    }

    private async Task TouchQueue(Guid id, string reason, DateTime now, CancellationToken ct)
    {
        var queue = await repository.QueueAsync(id, ct);
        if (queue == null) { queue = new EmployeeDispatchQueue { PickupId = id }; repository.AddQueue(queue); }
        queue.Reason = reason; queue.UpdatedAt = now;
    }
    private void Notify(Guid userId, Guid pickupId, string title, string message, Guid? depotId) =>
        repository.AddNotification(new Notification { UserId = userId, PickupRequestId = pickupId, DepotId = depotId,
            Category = "PICKUP", TargetType = "PICKUP_REQUEST", TargetId = pickupId, Title = title, Message = message });
}

// Polling bền vững từ DB: không phụ thuộc app đang mở, không sửa luồng tạo đơn của Seller.
public sealed class EmployeeDispatchWorker(IServiceScopeFactory scopes, ILogger<EmployeeDispatchWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<EmployeeDailyPlanService>().PrepareAsync(DateTime.UtcNow, stoppingToken);
                await scope.ServiceProvider.GetRequiredService<EmployeeDispatchService>().DispatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Điều phối thu gom chưa thực hiện được; kiểm tra schema và kết nối DB."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
