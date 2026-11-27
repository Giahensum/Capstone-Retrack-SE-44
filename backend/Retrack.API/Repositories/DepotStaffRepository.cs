using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class DepotStaffRepository(AppDbContext db) : IDepotStaffRepository
{
    // Tạo/kích hoạt nhân sự (kể cả Admin) phải dùng cùng khóa dòng kho.
    public async Task<IDepotTransaction> LockAsync(params Guid[] depotIds)
    {
        if (!db.Database.IsRelational()) return new MemoryTransaction();
        var tx = await new DepotUnitOfWork(db).BeginAsync();
        try
        {
            if (db.Database.IsNpgsql()) foreach (var id in depotIds.Distinct().Order())
                await db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {id} FOR UPDATE").LoadAsync();
            return tx;
        }
        catch { await tx.DisposeAsync(); throw; }
    }
    private sealed class MemoryTransaction : IDepotTransaction
    {
        public Task CommitAsync() => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    public Task<int> CountActiveAsync(Guid depotId) => db.DepotStaffs.CountAsync(s => s.DepotId == depotId && s.IsActive && s.User.IsActive);
    public async Task<PagedResult<StaffDto>> ListAsync(Guid depotId, DepotQuery query)
    {
        var source = db.DepotStaffs.AsNoTracking().Where(s => s.DepotId == depotId);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(s => s.User.FullName.Contains(query.Search) || s.User.Email.Contains(query.Search));
        if (!string.IsNullOrEmpty(query.Status)) source = source.Where(s => (s.IsActive && s.User.IsActive) == (query.Status == "ACTIVE"));
        return await DepotRepositoryPage.ReadAsync(source.OrderBy(s => s.User.FullName).ThenBy(s => s.Id)
            .Select(s => new StaffDto(s.Id, s.UserId, s.User.FullName, s.User.Email, s.User.Phone, s.StaffType, s.IsActive && s.User.IsActive)), query);
    }
    public Task<bool> EmailExistsAsync(string email) => db.Users.AnyAsync(u => u.Email.ToLower() == email);
    public async Task<bool> TryCreateAsync(DepotStaff staff)
    {
        db.DepotStaffs.Add(staff);
        // Một SaveChanges lưu nguyên tử tài khoản và liên kết kho.
        try { await db.SaveChangesAsync(); return true; }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return false; }
    }
    public async Task<DepotStaff?> FindAsync(Guid depotId, Guid staffId)
    {
        var staff = await db.DepotStaffs.Include(s => s.User).SingleOrDefaultAsync(s => s.Id == staffId && s.DepotId == depotId);
        if (staff != null)
        {
            await db.Entry(staff).ReloadAsync();
            await db.Entry(staff.User).ReloadAsync();
        }
        return staff;
    }
    public async Task SaveAsync() => await db.SaveChangesAsync();
}
