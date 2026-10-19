using System.ComponentModel.DataAnnotations;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
namespace Retrack.API.Services.Depot;

public sealed partial class DepotService
{
    public async Task<DepotProfileDto> GetProfileAsync(Guid ownerId, Guid depotId)
    {
        await RequireOwnerAsync(ownerId, depotId);
        return await owners.ProfileAsync(depotId);
    }
    public async Task<DepotProfileDto> UpdateProfileAsync(Guid ownerId, Guid depotId, UpdateDepotProfileDto dto)
    {
        await RequireOwnerAsync(ownerId, depotId);
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        await using var tx = await owners.LockProfileAsync(depotId);
        var depot = await owners.FindAsync(depotId);
        var identityChanged = depot.Name != dto.Name.Trim() || depot.Address != dto.Address.Trim() || depot.TaxCode != dto.TaxCode;
        if (identityChanged && depot.VerificationStatus == "SUBMITTED") throw new DepotConflictException("Kho có hồ sơ chờ duyệt; chưa thể thay đổi tên, địa chỉ hoặc mã số thuế.");
        if (identityChanged && depot.VerificationStatus == "APPROVED") depot.VerificationStatus = "UNVERIFIED";
        depot.Name = dto.Name.Trim(); depot.Address = dto.Address.Trim(); depot.TaxCode = dto.TaxCode;
        depot.ContactPhone = dto.ContactPhone; depot.Description = dto.Description;
        depot.Latitude = dto.Latitude; depot.Longitude = dto.Longitude;
        await owners.SaveAsync();
        await tx.CommitAsync();
        return await GetProfileAsync(ownerId, depotId);
    }
    public async Task<PagedResult<FactoryPartnerDto>> GetFactoriesAsync(Guid ownerId, Guid depotId, FactorySearchQuery query)
    {
        await RequireOwnerAsync(ownerId, depotId);
        if (!string.IsNullOrEmpty(query.Status) && query.Status is not ("APPROVED" or "PENDING" or "BLOCKED" or "DECLINED"))
            throw new ArgumentException("Trạng thái đối tác không hợp lệ.");
        if (!string.IsNullOrWhiteSpace(query.MaterialType))
            query.MaterialType = Retrack.API.Services.Shared.MaterialCatalog.RequireCode(query.MaterialType);
        return await owners.FactoriesAsync(depotId, query);
    }
    public async Task<PagedResult<DemandDto>> GetDemandsAsync(Guid ownerId, Guid depotId, DepotQuery query)
    {
        await RequireOwnerAsync(ownerId, depotId);
        return await owners.DemandsAsync(depotId, query, DateTime.UtcNow);
    }
}
