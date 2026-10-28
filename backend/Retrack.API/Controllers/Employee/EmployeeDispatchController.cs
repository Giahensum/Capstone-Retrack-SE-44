using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Models;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Employee;

namespace Retrack.API.Controllers.Employee;

[ApiController, Authorize(Roles = "DEPOT_EMPLOYEE"), Route("api/employee/dispatch")]
public sealed class EmployeeDispatchController(EmployeeDispatchService dispatch) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("shifts")]
    public async Task<IActionResult> Shifts(CancellationToken ct) =>
        Ok(ApiResponse<object>.Ok(await dispatch.MineAsync(UserId, ct)));
    [HttpPut("shifts/{id:guid}/availability")]
    public async Task<IActionResult> Availability(Guid id, AvailabilityInput input, CancellationToken ct)
    {
        await dispatch.ChangeAvailabilityAsync(UserId, id, input, ct);
        return Ok(ApiResponse<object>.Ok(await dispatch.MineAsync(UserId, ct)));
    }
}

public sealed record AreaInput(string Name, double Latitude, double Longitude, double RadiusMeters, bool IsActive);
public sealed record ShiftInput(Guid AreaId, Guid EmployeeId, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    string Vehicle, decimal CapacityKg, int MaxPendingOrders);

// API bổ sung cho cấu hình điều phối; không thay controller/nghiệp vụ Depot hiện hành.
[ApiController, Authorize(Roles = "DEPOT_OWNER"), Route("api/depot/{depotId:guid}/employee-dispatch")]
public sealed class EmployeeDispatchSetupController(AppDbContext db, EmployeeDispatchService dispatch) : ControllerBase
{
    private async Task Require(Guid depotId, CancellationToken ct)
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (!await db.Depots.AnyAsync(d => d.Id == depotId && d.OwnerId == id && d.Owner.IsActive
            && d.Owner.Role == "DEPOT_OWNER", ct)) throw new DepotForbiddenException();
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid depotId, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        await Require(depotId, ct); EmployeeReportingService.Page(page, 20);
        var areas = db.EmployeeCollectionAreas.Where(a => a.DepotId == depotId);
        var waiting = db.EmployeeDispatchQueues.Where(q => q.Reason != "ASSIGNED"
            && db.PickupRequests.Any(p => p.Id == q.PickupId && p.TargetDepotId == depotId && p.Status == "PENDING"));
        return Ok(ApiResponse<object>.Ok(new {
            areas = await areas.OrderBy(a => a.Name).ThenBy(a => a.Id).Take(100).ToListAsync(ct),
            shifts = await db.EmployeeDispatchShifts.Where(s => areas.Any(a => a.Id == s.AreaId))
                .OrderByDescending(s => s.StartsAt).ThenBy(s => s.Id).Skip((page - 1) * 20).Take(20).ToListAsync(ct),
            waiting = await waiting.OrderBy(q => q.UpdatedAt).ThenBy(q => q.PickupId)
                .Skip((page - 1) * 20).Take(20).ToListAsync(ct),
            waitingCount = await waiting.CountAsync(ct), page, pageSize = 20
        }));
    }

    [HttpPut("areas/{id:guid}")]
    public async Task<IActionResult> Area(Guid depotId, Guid id, AreaInput input, CancellationToken ct)
    {
        await Require(depotId, ct);
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 120
            || !double.IsFinite(input.Latitude) || Math.Abs(input.Latitude) > 90
            || !double.IsFinite(input.Longitude) || Math.Abs(input.Longitude) > 180
            || !double.IsFinite(input.RadiusMeters) || input.RadiusMeters is < 100 or > 20000)
            throw new ArgumentException("Tên, tọa độ hoặc bán kính khu vực (100–20.000 m) không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await dispatch.LockAsync(ct);
        var area = await db.EmployeeCollectionAreas.FindAsync([id], ct);
        if (area != null && area.DepotId != depotId) throw new KeyNotFoundException("Không tìm thấy khu vực.");
        if (area == null) { area = new() { Id = id, DepotId = depotId }; db.EmployeeCollectionAreas.Add(area); }
        area.Name = input.Name.Trim(); area.Latitude = input.Latitude; area.Longitude = input.Longitude;
        area.RadiusMeters = input.RadiusMeters; area.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Ok(ApiResponse<object>.Ok(area));
    }

    [HttpPost("shifts")]
    public async Task<IActionResult> Shift(Guid depotId, ShiftInput input, CancellationToken ct)
    {
        await Require(depotId, ct);
        if (input.EndsAt <= input.StartsAt || input.EndsAt - input.StartsAt > TimeSpan.FromHours(24)
            || input.EndsAt <= DateTimeOffset.UtcNow || input.StartsAt > DateTimeOffset.UtcNow.AddDays(60)
            || input.Vehicle is not ("motorcycle" or "car" or "truck")
            || input.CapacityKg is <= 0 or > 20000 || input.MaxPendingOrders is < 1 or > 50)
            throw new ArgumentException("Ca tối đa 24 giờ, phương tiện, tải trọng hoặc giới hạn đơn không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await dispatch.LockAsync(ct);
        if (!await db.EmployeeCollectionAreas.AnyAsync(a => a.Id == input.AreaId && a.DepotId == depotId && a.IsActive, ct)
            || !await db.DepotStaffs.AnyAsync(s => s.DepotId == depotId && s.UserId == input.EmployeeId && s.IsActive
                && s.StaffType == "DEPOT_EMPLOYEE" && s.User.IsActive && s.User.Role == "DEPOT_EMPLOYEE", ct))
            throw new ArgumentException("Khu vực hoặc nhân viên không thuộc kho đang hoạt động.");
        var start = input.StartsAt.UtcDateTime; var end = input.EndsAt.UtcDateTime;
        if (await db.EmployeeDispatchShifts.AnyAsync(s => s.EmployeeId == input.EmployeeId
            && s.Availability != "STOPPED" && s.StartsAt < end && s.EndsAt > start, ct))
            throw new DepotConflictException("Nhân viên đã có ca trùng giờ.");
        var shift = new EmployeeDispatchShift { AreaId = input.AreaId, EmployeeId = input.EmployeeId,
            StartsAt = start, EndsAt = end, Vehicle = input.Vehicle,
            CapacityKg = input.CapacityKg, MaxPendingOrders = input.MaxPendingOrders };
        db.EmployeeDispatchShifts.Add(shift);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Ok(ApiResponse<object>.Ok(shift));
    }

    [HttpGet("pickups/{id:guid}/history")]
    public async Task<IActionResult> History(Guid depotId, Guid id, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        await Require(depotId, ct); EmployeeReportingService.Page(page, 20);
        if (!await db.PickupRequests.AnyAsync(p => p.Id == id && p.TargetDepotId == depotId, ct))
            throw new KeyNotFoundException("Không tìm thấy đơn thuộc kho.");
        return Ok(ApiResponse<object>.Ok(await db.EmployeeDispatchEvents.Where(e => e.PickupId == id)
            .OrderByDescending(e => e.CreatedAt).ThenBy(e => e.Id).Skip((page - 1) * 20).Take(20).ToListAsync(ct)));
    }
}
