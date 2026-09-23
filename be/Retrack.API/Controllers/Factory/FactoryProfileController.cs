using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using FactoryEntity = Retrack.API.Models.Factory;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/profile")]
[Authorize(Roles = "FACTORY")]
public class FactoryProfileController(AppDbContext db) : FactoryControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var factory = await Db.Factories.AsNoTracking().Include(x => x.User)
            .SingleOrDefaultAsync(x => x.UserId == CurrentUserId, ct);
        if (factory is null) return NotFound(new { success = false, message = "Chưa có hồ sơ nhà máy." });
        return Ok(new { success = true, data = new
        {
            factory.Id, factory.CompanyName, factory.TaxCode, factory.Address, factory.IndustrialZone,
            factory.ContactPhone, factory.BusinessLicenseUrl, factory.EnvironmentalLicenseUrl,
            factory.CapacityKgPerMonth, factory.MinimumPurityPercent,
            acceptedMaterials = factory.AcceptedMaterials.Select(x => x.ToString()),
            factory.Latitude, factory.Longitude, user = new { factory.User.FullName, factory.User.Email, factory.User.Phone }
        }});
    }

    [HttpPut]
    public async Task<IActionResult> Update(ProfileRequest request, CancellationToken ct)
    {
        var factory = await Db.Factories.SingleOrDefaultAsync(x => x.UserId == CurrentUserId, ct);
        if (factory is null)
        {
            factory = new FactoryEntity { Id = Guid.NewGuid(), UserId = CurrentUserId };
            Db.Factories.Add(factory);
        }
        factory.CompanyName = request.CompanyName.Trim();
        factory.TaxCode = request.TaxCode?.Trim();
        factory.Address = request.Address.Trim();
        factory.IndustrialZone = request.IndustrialZone?.Trim();
        factory.ContactPhone = request.ContactPhone?.Trim();
        factory.BusinessLicenseUrl = request.BusinessLicenseUrl;
        factory.EnvironmentalLicenseUrl = request.EnvironmentalLicenseUrl;
        factory.Latitude = request.Latitude;
        factory.Longitude = request.Longitude;
        factory.CapacityKgPerMonth = request.CapacityKgPerMonth;
        factory.MinimumPurityPercent = request.MinimumPurityPercent;
        factory.AcceptedMaterials = request.AcceptedMaterials.Distinct().ToArray();
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Đã lưu hồ sơ nhà máy.", data = new { factory.Id } });
    }
}
