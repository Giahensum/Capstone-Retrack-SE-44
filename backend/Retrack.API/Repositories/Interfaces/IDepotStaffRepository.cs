using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public interface IDepotStaffRepository
{
    Task<PagedResult<StaffDto>> ListAsync(Guid depotId, DepotQuery query);
    Task<bool> EmailExistsAsync(string email);
    Task<bool> TryCreateAsync(DepotStaff staff);
    Task<DepotStaff?> FindAsync(Guid depotId, Guid staffId);
    Task SaveAsync();
}
