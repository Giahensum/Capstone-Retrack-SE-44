using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Employee;

[ApiController]
[Authorize(Roles = "DEPOT_EMPLOYEE")]
public class EmployeePickupController(AppDbContext db, INotificationService notify) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");

    private IQueryable<Guid> ActiveDepotIds() => db.DepotStaffs.Where(s => s.UserId == UserId
        && s.IsActive && s.StaffType == "DEPOT_EMPLOYEE" && s.User.IsActive
        && s.User.Role == "DEPOT_EMPLOYEE" && s.Depot.Owner.IsActive).Select(s => s.DepotId);

    private async Task<Guid> GetDepotIdAsync(CancellationToken ct)
    {
        var depotId = await db.DepotStaffs.AsNoTracking()
            .Where(s => s.UserId == UserId && s.StaffType == "DEPOT_EMPLOYEE" && s.IsActive
                && s.User.IsActive && s.User.Role == "DEPOT_EMPLOYEE" && s.Depot.Owner.IsActive)
            .OrderBy(s => s.Id).Select(s => (Guid?)s.DepotId).FirstOrDefaultAsync(ct);
        return depotId ?? throw new UnauthorizedAccessException("Tài khoản không liên kết với kho đang hoạt động.");
    }

    [HttpGet("/api/employee/dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        await GetDepotIdAsync(ct);
        var result = await new Retrack.API.Services.Employee.EmployeeReportingService(db).DashboardAsync(UserId, ct);
        return Ok(ApiResponse<EmployeeDashboardDto>.Ok(result));
    }

    [HttpGet("/api/employee/pickup-pool")]
    public async Task<IActionResult> Pool(CancellationToken ct)
    {
        var depotId = await GetDepotIdAsync(ct);
        var list = await db.PickupRequests.AsNoTracking()
            .Where(p => p.TargetDepotId == depotId && p.Status == "PENDING" && p.AcceptedCollectorId == null)
            .OrderBy(p => p.PreferredDatetime == null).ThenBy(p => p.PreferredDatetime)
            .ThenBy(p => p.CreatedAt).ThenBy(p => p.Id)
            .Select(p => new PickupPoolItemDto(p.Id, p.Seller.FullName, p.Seller.Phone, p.Address,
                p.Latitude, p.Longitude, p.PreferredDatetime, p.Description, p.RequestImageUrl, p.CreatedAt))
            .ToListAsync(ct);
        return Ok(ApiResponse<List<PickupPoolItemDto>>.Ok(list));
    }

    [HttpGet("/api/employee/pickups/active")]
    public async Task<IActionResult> Active([FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (page < 1 || page > 100000) return BadRequest(ApiResponse<object>.Fail("Trang không hợp lệ."));
        await GetDepotIdAsync(ct);
        var userId = UserId;
        var depots = ActiveDepotIds();
        var query = db.PickupRequests.AsNoTracking().Where(p => p.TargetDepotId != null && depots.Contains(p.TargetDepotId.Value)
            && p.AcceptedCollectorId == userId && (p.Status == "SCHEDULED" || p.Status == "IN_PROGRESS" || p.Status == "SELLER_CONFIRMED"));
        var items = await query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).Skip((page - 1) * 20).Take(20)
            .Select(p => new ActivePickupSummaryDto(p.Id, p.Seller.FullName, p.Seller.Phone,
                p.Address, p.Status, p.PreferredDatetime)).ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { items, page, pageSize = 20, total = await query.CountAsync(ct) }));
    }

    [HttpGet("/api/employee/pickup/{pickupId:guid}")]
    public async Task<IActionResult> Detail(Guid pickupId, CancellationToken ct)
    {
        await GetDepotIdAsync(ct);
        var userId = UserId;
        var depots = ActiveDepotIds();
        var item = await db.PickupRequests.AsNoTracking()
            .Where(p => p.Id == pickupId && p.TargetDepotId != null && depots.Contains(p.TargetDepotId.Value)
                && ((p.Status == "PENDING" && p.AcceptedCollectorId == null) || p.AcceptedCollectorId == userId))
            .Select(p => new EmployeePickupDetailDto(p.Id, p.Seller.FullName, p.Seller.Phone, p.Address,
                p.Latitude, p.Longitude, p.PreferredDatetime, p.Description, p.RequestImageUrl, p.CreatedAt,
                p.Status, p.AcceptedCollectorId == userId)).FirstOrDefaultAsync(ct);
        return item == null
            ? NotFound(ApiResponse<object>.Fail("Đơn không tồn tại hoặc không còn thuộc phạm vi của bạn."))
            : Ok(ApiResponse<EmployeePickupDetailDto>.Ok(item));
    }

    [HttpPost("/api/employee/pickup/{pickupId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid pickupId, CancellationToken ct)
    {
        var depotId = await GetDepotIdAsync(ct);
        var userId = UserId;
        var pickup = await db.PickupRequests.AsNoTracking().Include(p => p.Seller)
            .SingleOrDefaultAsync(p => p.Id == pickupId && p.TargetDepotId == depotId, ct);
        // Do not reveal another depot's orders, even if the caller knows their UUID.
        if (pickup == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy đơn thu gom."));

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        // Exactly one concurrent claimant can win this conditional SQL UPDATE.
        var changed = await db.PickupRequests
            .Where(p => p.Id == pickupId && p.TargetDepotId == depotId
                && p.Status == "PENDING" && p.AcceptedCollectorId == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.AcceptedCollectorId, (Guid?)userId)
                .SetProperty(p => p.Status, "SCHEDULED")
                .SetProperty(p => p.UpdatedAt, now), ct);
        if (changed == 0)
            return Conflict(ApiResponse<object>.Fail("Đơn đã được nhận bởi nhân viên khác hoặc không còn khả dụng."));

        // The notification service shares this scoped DbContext and transaction.
        await notify.SendAsync(pickup.SellerId, "Đơn thu gom đã được nhận",
            "Nhân viên kho đã nhận đơn của bạn và sẽ đến sớm.");
        await transaction.CommitAsync(ct);
        return Ok(ApiResponse<PickupPoolItemDto>.Ok(Map(pickup), "Đã nhận đơn thành công."));
    }

    private static PickupPoolItemDto Map(PickupRequest p) => new(p.Id, p.Seller.FullName, p.Seller.Phone,
        p.Address, p.Latitude, p.Longitude, p.PreferredDatetime, p.Description, p.RequestImageUrl, p.CreatedAt);
}


