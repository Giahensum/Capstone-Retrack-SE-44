using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/demands")]
[Authorize(Roles = "FACTORY")]
public class FactoryDemandController(Retrack.API.Data.AppDbContext db) : FactoryControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery query, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var source = Db.FactoryDemands.AsNoTracking().Where(x => x.FactoryId == factory.Id).OrderByDescending(x => x.CreatedAt);
        var count = await source.CountAsync(ct);
        var items = await source.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return Ok(new { success = true, data = new { items = items.Select(DemandView), totalCount = count, query.Page, query.PageSize, totalPages = (int)Math.Ceiling((double)count / query.PageSize) } });
    }

    [HttpPost]
    public async Task<IActionResult> Create(DemandRequest request, CancellationToken ct)
    {
        if (request.MinPricePerKg > request.MaxPricePerKg && request.MaxPricePerKg.HasValue)
            return BadRequest(new { success = false, message = "Giá tối thiểu không được lớn hơn giá tối đa." });
        var factory = await CurrentFactory(ct);
        var demand = new FactoryDemand
        {
            FactoryId = factory.Id, MaterialType = request.MaterialType.ToString(),
            RequiredWeightKg = request.QuantityKg, MinPricePerKg = request.MinPricePerKg,
            MaxPricePerKg = request.MaxPricePerKg, Deadline = request.Deadline ?? DateTime.UtcNow.AddDays(7),
            IsActive = request.IsActive, Note = request.Note?.Trim(), UpdatedAt = DateTime.UtcNow
        };
        Db.FactoryDemands.Add(demand);
        await Db.SaveChangesAsync(ct);
        return Created($"/api/factory/demands/{demand.Id}", new { success = true, data = DemandView(demand) });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, DemandRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var demand = await Db.FactoryDemands.SingleOrDefaultAsync(x => x.Id == id && x.FactoryId == factory.Id, ct);
        if (demand is null) return NotFound(new { success = false, message = "Không tìm thấy nhu cầu." });
        demand.MaterialType = request.MaterialType.ToString(); demand.RequiredWeightKg = request.QuantityKg;
        demand.MinPricePerKg = request.MinPricePerKg; demand.MaxPricePerKg = request.MaxPricePerKg;
        demand.Deadline = request.Deadline ?? DateTime.UtcNow.AddDays(7); demand.IsActive = request.IsActive;
        demand.Note = request.Note?.Trim();
        demand.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = DemandView(demand) });
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> Toggle(Guid id, [FromBody] DemandStatusRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var demand = await Db.FactoryDemands.SingleOrDefaultAsync(x => x.Id == id && x.FactoryId == factory.Id, ct);
        if (demand is null) return NotFound(new { success = false, message = "Không tìm thấy nhu cầu." });
        demand.IsActive = request.IsActive;
        demand.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = DemandView(demand) });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var demand = await Db.FactoryDemands.SingleOrDefaultAsync(x => x.Id == id && x.FactoryId == factory.Id, ct);
        if (demand is null) return NotFound(new { success = false, message = "Không tìm thấy nhu cầu." });
        Db.FactoryDemands.Remove(demand);
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Đã xóa nhu cầu." });
    }
}

public class DemandStatusRequest { public bool IsActive { get; set; } }
