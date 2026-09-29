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
    public async Task<PagedResult<StaffDto>> ListAsync(Guid depotId, DepotQuery query)
    {
        var source = db.DepotStaffs.AsNoTracking().Where(s => s.DepotId == depotId);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(s => s.User.FullName.Contains(query.Search) || s.User.Email.Contains(query.Search));
        if (!string.IsNullOrEmpty(query.Status)) source = source.Where(s => s.IsActive == (query.Status == "ACTIVE"));
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
    public Task<DepotStaff?> FindAsync(Guid depotId, Guid staffId) => db.DepotStaffs.Include(s => s.User)
        .SingleOrDefaultAsync(s => s.Id == staffId && s.DepotId == depotId);
    public async Task SaveAsync() => await db.SaveChangesAsync();
}
