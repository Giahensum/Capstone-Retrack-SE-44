using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/profile")]
[Authorize(Roles = "FACTORY")]
public class FactoryProfileController(Retrack.API.Data.AppDbContext db) : FactoryControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var current = await CurrentFactory(ct);
        var factory = await Db.Factories.Include(x => x.Owner).AsNoTracking()
            .SingleAsync(x => x.Id == current.Id, ct);
        return Ok(new { success = true, data = new
        {
            factory.Id, companyName = factory.Name, factory.TaxCode, factory.Address,
            factory.IndustrialZone, contactPhone = factory.ContactPhone ?? factory.Owner?.Phone,
            factory.BusinessLicenseUrl, environmentalLicenseUrl = factory.EnvironmentalLicenseUrl,
            capacityKgPerMonth = factory.CapacityKgPerMonth, minimumPurityPercent = factory.MinimumPurityPercent,
            acceptedMaterials = factory.AcceptedMaterialsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries),
            factory.Latitude, factory.Longitude,
            user = factory.Owner is null ? null : new { factory.Owner.FullName, factory.Owner.Email, factory.Owner.Phone }
        }});
    }

    [HttpPut]
    public async Task<IActionResult> Update(ProfileRequest request, CancellationToken ct)
    {
        var factory = await Db.Factories.SingleOrDefaultAsync(x => x.OwnerId == CurrentUserId, ct);
        if (factory is null)
        {
            factory = new Retrack.API.Models.Factory { OwnerId = CurrentUserId, Name = request.CompanyName.Trim(), Address = request.Address.Trim() };
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
        return Ok(new { success = true, message = "Đã lưu hồ sơ nhà máy.", data = new { factory.Id } });
    }
}
