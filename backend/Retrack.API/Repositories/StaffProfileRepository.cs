using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;

namespace Retrack.API.Repositories;

public interface IStaffProfileRepository
{
    Task<DepotStaff?> FindActiveAsync(Guid userId, string role, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}

public sealed class StaffProfileRepository(AppDbContext db) : IStaffProfileRepository
{
    public Task<DepotStaff?> FindActiveAsync(Guid userId, string role, CancellationToken ct) =>
        db.DepotStaffs.Include(s => s.User).Include(s => s.Depot)
            .Where(s => s.UserId == userId && s.StaffType == role && s.IsActive
                && s.User.IsActive && s.User.Role == role)
            .OrderBy(s => s.Id).FirstOrDefaultAsync(ct);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
