using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.DTOs;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.Driver;

public sealed record JobPlace(string Name, string Address, decimal? Latitude, decimal? Longitude);
public sealed record DriverJobDto(Guid Id, Guid BatchId, string? BatchCode, string MaterialType,
    decimal WeightKg, string Status, bool IsMine, JobPlace Depot, JobPlace Factory, DateTime CreatedAt);

public sealed class DriverJobService(AppDbContext db)
{
    private IQueryable<DepotStaff> Staff(Guid userId) => db.DepotStaffs.Where(s => s.UserId == userId
        && s.StaffType == "DRIVER" && s.IsActive && s.User.IsActive && s.User.Role == "DRIVER"
        && s.Depot.Owner.IsActive && s.Depot.Owner.Role == "DEPOT_OWNER");
    public async Task RequireAsync(Guid userId, CancellationToken ct)
    {
        if (!await Staff(userId).AnyAsync(ct)) throw new DepotForbiddenException();
    }
    private IQueryable<TransportJob> Scoped(Guid userId)
    {
        var staff = Staff(userId);
        return db.TransportJobs.Where(j => staff.Any(s => s.DepotId == j.Batch.DepotId));
    }
    private static IQueryable<TransportJob> Ready(IQueryable<TransportJob> jobs) => jobs.Where(j =>
        (j.Batch.Status == "TRANSPORT_READY" || j.Batch.Status == "ACCEPTED" || j.Batch.Status == "READY_FOR_PICKUP")
        && j.Batch.DeclaredWeightKg > 0 && j.Batch.FactoryReceivedAt == null
        && j.Batch.TargetFactory != null && j.Batch.TargetFactory.Owner != null && j.Batch.TargetFactory.Owner.IsActive);
    private static IQueryable<DriverJobDto> Project(IQueryable<TransportJob> jobs, Guid userId) => jobs.Select(j => new DriverJobDto(
        j.Id, j.BatchId, j.Batch.Code, j.Batch.MaterialType, j.Batch.DeclaredWeightKg, j.Status, j.DriverId == userId,
        new(j.Batch.Depot.Name, j.Batch.Depot.Address, j.Batch.Depot.Latitude, j.Batch.Depot.Longitude),
        new(j.Batch.TargetFactory!.Name, j.Batch.TargetFactory.Address, j.Batch.TargetFactory.Latitude, j.Batch.TargetFactory.Longitude), j.CreatedAt));
    public async Task<PagedResult<DriverJobDto>> ListAsync(Guid userId, bool mine, int page, CancellationToken ct)
    {
        if (page is < 1 or > 100000) throw new ArgumentException("Trang không hợp lệ.");
        await RequireAsync(userId, ct);
        var source = Scoped(userId).AsNoTracking();
        source = mine ? source.Where(j => j.DriverId == userId) : Ready(source).Where(j => j.DriverId == null && j.Status == "PENDING");
        return new() { Page = page, PageSize = 20, TotalCount = await source.CountAsync(ct),
            Items = await Project(source.OrderByDescending(j => j.CreatedAt).ThenBy(j => j.Id).Skip((page - 1) * 20).Take(20), userId).ToListAsync(ct) };
    }
    public async Task<DriverJobDto> DetailAsync(Guid userId, Guid id, CancellationToken ct)
    {
        await RequireAsync(userId, ct);
        var source = Scoped(userId).AsNoTracking().Where(j => j.Id == id);
        var ready = Ready(source);
        var visible = source.Where(j => j.DriverId == userId || (j.DriverId == null && j.Status == "PENDING" && ready.Any(r => r.Id == j.Id)));
        return await Project(visible, userId).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Chuyến không còn khả dụng hoặc không thuộc phạm vi của bạn.");
    }
    public async Task<DriverJobDto> AcceptAsync(Guid userId, Guid id, CancellationToken ct)
    {
        await RequireAsync(userId, ct);
        var meta = await Scoped(userId).Where(j => j.Id == id).Select(j => new { j.Batch.DepotId, j.Batch.Depot.OwnerId }).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Không tìm thấy chuyến trong kho của bạn.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Cùng thứ tự khóa depot với nghiệp vụ phân bổ/hủy lô của nhóm.
        if (db.Database.IsNpgsql())
            await db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {meta.DepotId} FOR UPDATE").LoadAsync(ct);
        var changed = await Ready(Scoped(userId)).Where(j => j.Id == id && j.Status == "PENDING" && j.DriverId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.DriverId, (Guid?)userId).SetProperty(j => j.Status, "ACCEPTED")
                .SetProperty(j => j.UpdatedAt, DateTime.UtcNow), ct);
        if (changed == 0)
        {
            if (!await Ready(Scoped(userId)).AnyAsync(j => j.Id == id && j.DriverId == userId && j.Status == "ACCEPTED", ct))
                throw new DepotConflictException("Chuyến đã được nhận hoặc lô không còn sẵn sàng vận chuyển.");
        }
        else
        {
            db.Notifications.Add(new Notification { UserId = meta.OwnerId, TransportJobId = id,
                Title = "Tài xế đã nhận chuyến", Message = "Một tài xế của kho đã nhận chuyến vận chuyển. Chưa xác nhận lấy hàng." });
            await db.SaveChangesAsync(ct);
        }
        var result = await DetailAsync(userId, id, ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
