namespace Retrack.API.Services.Interfaces;

public interface IStaffService
{
    Task<Retrack.API.DTOs.PagedResult<Retrack.API.DTOs.Depot.StaffDto>> ListAsync(Guid ownerId, Guid depotId, Retrack.API.DTOs.Depot.DepotQuery query);
    Task<Retrack.API.DTOs.Depot.StaffDto> CreateAsync(Guid ownerId, Guid depotId, Retrack.API.DTOs.Depot.CreateStaffDto dto);
    Task<Retrack.API.DTOs.Depot.StaffDto> UpdateAsync(Guid ownerId, Guid depotId, Guid staffId, Retrack.API.DTOs.Depot.UpdateStaffDto dto);
}


