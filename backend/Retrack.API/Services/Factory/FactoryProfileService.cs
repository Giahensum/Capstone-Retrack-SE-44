using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Factory;

public class FactoryProfileService(AppDbContext db) : FactoryServiceBase(db), IFactoryProfileService
{
    public async Task<ServiceResult<ProfileResponse>> GetAsync(Guid userId, CancellationToken ct)
    {
        var current = await CurrentFactory(userId, ct);
        var factory = await Db.Factories.Include(x => x.Owner).AsNoTracking()
            .SingleAsync(x => x.Id == current.Id, ct);
        return ServiceResult<ProfileResponse>.Success(data: new ProfileResponse
        {
            Id = factory.Id,
            CompanyName = factory.Name,
            TaxCode = factory.TaxCode,
            Address = factory.Address,
            IndustrialZone = factory.IndustrialZone,
            ContactPhone = factory.ContactPhone ?? factory.Owner?.Phone,
            BusinessLicenseUrl = factory.BusinessLicenseUrl,
            EnvironmentalLicenseUrl = factory.EnvironmentalLicenseUrl,
            CapacityKgPerMonth = factory.CapacityKgPerMonth,
            MinimumPurityPercent = factory.MinimumPurityPercent,
            AcceptedMaterials = factory.AcceptedMaterialsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries),
            Latitude = factory.Latitude,
            Longitude = factory.Longitude,
            User = factory.Owner is null ? null : new ProfileOwnerResponse
            {
                FullName = factory.Owner.FullName,
                Email = factory.Owner.Email,
                Phone = factory.Owner.Phone
            }
        });
    }

    public async Task<ServiceResult<ProfileUpdatedResponse>> UpdateAsync(Guid userId, ProfileRequest request, CancellationToken ct)
    {
        var factory = await Db.Factories.SingleOrDefaultAsync(x => x.OwnerId == userId, ct);
        if (factory is null)
        {
            factory = new Retrack.API.Models.Factory { OwnerId = userId, Name = request.CompanyName.Trim(), Address = request.Address.Trim() };
            Db.Factories.Add(factory);
        }
        factory.Name = request.CompanyName.Trim();
        factory.TaxCode = request.TaxCode?.Trim();
        factory.Address = request.Address.Trim();
        factory.IndustrialZone = request.IndustrialZone?.Trim();
        factory.ContactPhone = request.ContactPhone?.Trim();
        factory.BusinessLicenseUrl = request.BusinessLicenseUrl;
        factory.EnvironmentalLicenseUrl = request.EnvironmentalLicenseUrl;
        factory.CapacityKgPerMonth = request.CapacityKgPerMonth;
        factory.MinimumPurityPercent = request.MinimumPurityPercent;
        factory.AcceptedMaterialsCsv = string.Join(',', request.AcceptedMaterials.Distinct());
        factory.Latitude = request.Latitude;
        factory.Longitude = request.Longitude;
        await Db.SaveChangesAsync(ct);
        return ServiceResult<ProfileUpdatedResponse>.Success(data: new ProfileUpdatedResponse
        {
            Id = factory.Id
        }, message: "Đã lưu hồ sơ nhà máy.");
    }
}
