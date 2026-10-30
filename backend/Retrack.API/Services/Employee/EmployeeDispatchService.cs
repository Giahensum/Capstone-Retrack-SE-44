using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.Employee;

public sealed record AvailabilityInput(string Status, string? Reason);

public sealed class EmployeeDispatchService(AppDbContext db)
{
    // Các tiến trình điều phối dùng cùng khóa; check-in vẫn khóa bản ghi pickup.
    public async Task LockAsync(CancellationToken ct)
    {
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(62051001)", ct);
    }

    private IQueryable<DepotStaff> Staff(Guid id) => db.DepotStaffs.Where(s => s.UserId == id
        && s.StaffType == "DEPOT_EMPLOYEE" && s.IsActive && s.User.IsActive
        && s.User.Role == "DEPOT_EMPLOYEE" && s.Depot.Owner.IsActive);

    public async Task<object> MineAsync(Guid userId, CancellationToken ct)
    {
        if (!await Staff(userId).AnyAsync(ct)) throw new DepotForbiddenException();
        var now = DateTime.UtcNow;
        return await (from shift in db.EmployeeDispatchShifts.AsNoTracking()
            join area in db.EmployeeCollectionAreas on shift.AreaId equals area.Id
            where shift.EmployeeId == userId && shift.EndsAt > now && shift.StartsAt <= now
                && Staff(userId).Any(s => s.DepotId == area.DepotId)
            orderby shift.StartsAt, shift.Id
            select new { shift.Id, area.Name, area.DepotId, shift.StartsAt, shift.EndsAt,
                shift.Availability, shift.Reason, shift.Vehicle, shift.CapacityKg, shift.MaxPendingOrders })
            .Take(20).ToListAsync(ct);
    }

    public async Task ChangeAvailabilityAsync(Guid userId, Guid shiftId, AvailabilityInput input, CancellationToken ct)
    {
        if (input.Status is not ("AVAILABLE" or "FULL" or "STOPPED"))
            throw new ArgumentException("Trạng thái phải là AVAILABLE, FULL hoặc STOPPED.");
        var reason = input.Reason?.Trim();
        if (input.Status != "AVAILABLE" && (reason == null || reason.Length is < 10 or > 1000))
            throw new ArgumentException("Hãy nhập lý do từ 10 đến 1.000 ký tự.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        var now = DateTime.UtcNow;
        var shift = await db.EmployeeDispatchShifts.SingleOrDefaultAsync(s => s.Id == shiftId && s.EmployeeId == userId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy ca của bạn.");
        var area = await db.EmployeeCollectionAreas.SingleAsync(a => a.Id == shift.AreaId, ct);
        if (!await Staff(userId).AnyAsync(s => s.DepotId == area.DepotId, ct)) throw new DepotForbiddenException();
        if (shift.StartsAt > now || shift.EndsAt <= now) throw new DepotConflictException("Ca chưa bắt đầu hoặc đã kết thúc.");
        if (shift.Availability == "STOPPED" && input.Status != "STOPPED")
            throw new DepotConflictException("Ca đã ngừng. Chủ kho cần bố trí ca mới.");
        if (shift.Availability == input.Status) { await tx.CommitAsync(ct); return; }
        shift.Availability = input.Status;
        shift.Reason = input.Status == "AVAILABLE" ? null : reason;
        shift.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        // Chuyển ngay đơn chưa check-in; đơn đang giữ hàng vẫn thuộc người hiện tại.
        var ids = await db.PickupRequests.Where(p => p.TargetDepotId == area.DepotId
            && p.AcceptedCollectorId == userId && p.Status == "SCHEDULED"
            && !db.PickupCheckIns.Any(c => c.PickupRequestId == p.Id))
            .OrderBy(p => p.Id).Select(p => p.Id).ToListAsync(ct);
        if (input.Status != "AVAILABLE")
            foreach (var id in ids)
            {
                await AssignLockedAsync(id, now, reason!, ct);
                // Cập nhật tải công việc trong cùng transaction trước khi chọn người cho đơn tiếp theo.
                await db.SaveChangesAsync(ct);
            }
        var owner = await db.Depots.Where(d => d.Id == area.DepotId).Select(d => d.OwnerId).SingleAsync(ct);
        db.Notifications.Add(new Notification { UserId = owner, DepotId = area.DepotId, Category = "PICKUP", Title = "Cập nhật ca thu gom",
            Message = $"Nhân viên {userId}: {input.Status}. {reason}" });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task DispatchAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        // Ngày nghiệp vụ Việt Nam. Đơn hẹn ngày mai không bị giao từ hôm nay.
        var endDay = now.AddHours(7).Date.AddDays(1).AddHours(-7);
        var ids = await db.PickupRequests.AsNoTracking()
            .Where(p => (p.Status == "PENDING" || p.Status == "SCHEDULED")
                && (p.TargetDepotId == null || db.EmployeeCollectionAreas.Any(a => a.DepotId == p.TargetDepotId))
                && (p.PreferredDatetime == null || p.PreferredDatetime < endDay)
                && !db.PickupCheckIns.Any(c => c.PickupRequestId == p.Id))
            .OrderBy(p => db.EmployeeDispatchQueues.Where(q => q.PickupId == p.Id)
                .Select(q => (DateTime?)q.UpdatedAt).FirstOrDefault())
            .ThenBy(p => p.PreferredDatetime ?? p.CreatedAt).ThenBy(p => p.Id)
            .Select(p => p.Id).Take(300).ToListAsync(ct);
        foreach (var id in ids)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await LockAsync(ct);
            await AssignLockedAsync(id, now, "Phân công tự động theo khu vực và ca", ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            db.ChangeTracker.Clear();
        }
    }

    private async Task AssignLockedAsync(Guid id, DateTime now, string reason, CancellationToken ct)
    {
        var query = db.Database.IsNpgsql()
            ? db.PickupRequests.FromSqlInterpolated($"SELECT * FROM pickup_requests WHERE id = {id} FOR UPDATE")
            : db.PickupRequests.Where(p => p.Id == id);
        var pickup = await query.SingleOrDefaultAsync(ct);
        if (pickup == null) return;
        await db.Entry(pickup).ReloadAsync(ct);
        if (pickup.Status is not ("PENDING" or "SCHEDULED")
            || !string.IsNullOrEmpty(pickup.CheckinImageUrl)
            || await db.PickupCheckIns.AnyAsync(c => c.PickupRequestId == id, ct)) return;
        // Owner xử lý ngoại lệ (ví dụ thiếu tọa độ). Giữ phân công tay khi ca/membership còn hợp lệ;
        // không giữ khi nhân viên báo đầy/ngừng ca hoặc hết giờ để TV3 tiếp tục điều phối lại.
        if (pickup.AcceptedCollectorId.HasValue && pickup.TargetDepotId.HasValue &&
            await db.EmployeeDispatchQueues.AnyAsync(q => q.PickupId == id && q.Reason == "ASSIGNED_MANUAL", ct) &&
            await db.EmployeeDispatchShifts.AnyAsync(s => s.EmployeeId == pickup.AcceptedCollectorId && s.Availability == "AVAILABLE" && s.StartsAt <= now && s.EndsAt > now &&
                (pickup.PreferredDatetime == null || pickup.PreferredDatetime < s.EndsAt) &&
                db.EmployeeCollectionAreas.Any(a => a.Id == s.AreaId && a.DepotId == pickup.TargetDepotId && a.IsActive), ct) &&
            await db.DepotStaffs.AnyAsync(s => s.DepotId == pickup.TargetDepotId && s.UserId == pickup.AcceptedCollectorId && s.IsActive && s.StaffType == "DEPOT_EMPLOYEE" && s.User.IsActive && s.User.Role == "DEPOT_EMPLOYEE" && s.Depot.Owner.IsActive && s.Depot.VerificationStatus == "APPROVED", ct)) return;
        var areas = await db.EmployeeCollectionAreas.Where(a => a.IsActive
            && (pickup.TargetDepotId == null || a.DepotId == pickup.TargetDepotId)
            && db.Depots.Any(d => d.Id == a.DepotId && d.Owner.IsActive && d.VerificationStatus == "APPROVED"))
            .OrderBy(a => a.Id).ToListAsync(ct);
        if (pickup.TargetDepotId == null && pickup.Latitude.HasValue && pickup.Longitude.HasValue)
        {
            var depotIds = areas.Select(a => a.DepotId).Distinct().ToArray();
            var depots = await db.Depots.Where(d => depotIds.Contains(d.Id))
                .Select(d => new { d.Id, d.Latitude, d.Longitude }).ToListAsync(ct);
            var withinRange = depots.Where(d => d.Latitude.HasValue && d.Longitude.HasValue
                && CollectionValidation.Distance((double)pickup.Latitude, (double)pickup.Longitude,
                    (double)d.Latitude, (double)d.Longitude) <= 20000).Select(d => d.Id).ToHashSet();
            areas = areas.Where(a => withinRange.Contains(a.DepotId)).ToList();
        }
        var home = pickup.Latitude.HasValue && pickup.Longitude.HasValue
            ? areas.Select(a => new { Area = a, Distance = CollectionValidation.Distance((double)pickup.Latitude,
                (double)pickup.Longitude, a.Latitude, a.Longitude) })
                .Where(a => a.Distance <= a.Area.RadiusMeters).OrderBy(a => a.Distance)
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
            var shifts = await db.EmployeeDispatchShifts.Where(s => areaIds.Contains(s.AreaId)
                && s.StartsAt <= now && s.EndsAt > now && s.Availability == "AVAILABLE"
                && db.DepotStaffs.Any(st => st.DepotId == depotId && st.UserId == s.EmployeeId && st.IsActive
                    && st.StaffType == "DEPOT_EMPLOYEE" && st.User.IsActive && st.User.Role == "DEPOT_EMPLOYEE"))
                .OrderBy(s => s.Id).ToListAsync(ct);
            // Giữ phân công ổn định; không chuyển đơn vì một người khác vừa rảnh hơn.
            if (old.HasValue && shifts.Any(s => s.EmployeeId == old))
            {
                await TouchQueue(id, "ASSIGNED", now, ct);
                return;
            }
            var counts = await db.PickupRequests.Where(p => p.TargetDepotId == depotId && p.AcceptedCollectorId != null
                && (p.Status == "SCHEDULED" || p.Status == "IN_PROGRESS"))
                .GroupBy(p => p.AcceptedCollectorId!.Value).Select(g => new { Id = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.Count, ct);
            // Không quay lại người từng bàn giao đơn trong ngày, tránh vòng lặp.
            var day = now.AddHours(7).Date.AddHours(-7);
            var excluded = await db.EmployeeDispatchEvents.Where(e => e.PickupId == id
                && e.FromEmployeeId != null && e.CreatedAt >= day).Select(e => e.FromEmployeeId!.Value).ToListAsync(ct);
            chosen = shifts.Where(s => s.EmployeeId != old && !excluded.Contains(s.EmployeeId)
                && counts.GetValueOrDefault(s.EmployeeId) < s.MaxPendingOrders
                && (pickup.PreferredDatetime == null || pickup.PreferredDatetime < s.EndsAt))
                .OrderBy(s => s.AreaId == home.Id ? 0 : 1)
                .ThenBy(s => counts.GetValueOrDefault(s.EmployeeId) * 1500 +
                    CollectionValidation.Distance(home.Latitude, home.Longitude,
                        areas.Single(a => a.Id == s.AreaId).Latitude, areas.Single(a => a.Id == s.AreaId).Longitude))
                .ThenBy(s => s.Id).FirstOrDefault();
            if (chosen != null) pickup.TargetDepotId = depotId;
        }
        var next = chosen?.EmployeeId;
        var waitingReason = home == null ? "Chưa có khu vực phục vụ phù hợp hoặc tọa độ hợp lệ."
            : "Chưa có nhân viên trong ca, còn khả năng nhận đơn.";
        var priorQueue = await db.EmployeeDispatchQueues.FindAsync([id], ct);
        if (next != old)
        {
            pickup.AcceptedCollectorId = next;
            pickup.Status = next == null ? "PENDING" : "SCHEDULED";
            pickup.UpdatedAt = now;
            db.EmployeeDispatchEvents.Add(new EmployeeDispatchEvent { PickupId = id, FromEmployeeId = old,
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
            var owner = await db.Depots.Where(d => d.Id == pickup.TargetDepotId).Select(d => d.OwnerId).SingleAsync(ct);
            Notify(owner, id, "Đơn đang chờ điều phối", waitingReason, pickup.TargetDepotId);
        }
        await TouchQueue(id, next == null ? waitingReason : "ASSIGNED", now, ct);
    }

    private async Task TouchQueue(Guid id, string reason, DateTime now, CancellationToken ct)
    {
        var queue = await db.EmployeeDispatchQueues.FindAsync([id], ct);
        if (queue == null) { queue = new EmployeeDispatchQueue { PickupId = id }; db.EmployeeDispatchQueues.Add(queue); }
        queue.Reason = reason; queue.UpdatedAt = now;
    }
    private void Notify(Guid userId, Guid pickupId, string title, string message, Guid? depotId) =>
        db.Notifications.Add(new Notification { UserId = userId, PickupRequestId = pickupId, DepotId = depotId,
            Category = "PICKUP", TargetType = "PICKUP_REQUEST", TargetId = pickupId, Title = title, Message = message });
}

// Polling bền vững từ DB: không phụ thuộc app đang mở, không sửa luồng tạo đơn của Seller.
public sealed class EmployeeDispatchWorker(IServiceScopeFactory scopes, ILogger<EmployeeDispatchWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<EmployeeDispatchService>().DispatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Điều phối thu gom chưa thực hiện được; kiểm tra schema và kết nối DB."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
