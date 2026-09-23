using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/demands")]
[Authorize(Roles = "FACTORY")]
public class FactoryDemandController(AppDbContext db) : FactoryControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery query, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var items = Db.FactoryDemands.AsNoTracking().Where(x => x.FactoryId == factory.Id).OrderByDescending(x => x.CreatedAt);
        var count = await items.CountAsync(ct);
        return Ok(new { success = true, data = new { items = await items.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct), totalCount = count, query.Page, query.PageSize, totalPages = (int)Math.Ceiling((double)count / query.PageSize) } });
    }

    [HttpPost]
    public async Task<IActionResult> Create(DemandRequest request, CancellationToken ct)
    {
        if (request.MinPricePerKg.HasValue && request.MaxPricePerKg.HasValue && request.MinPricePerKg > request.MaxPricePerKg)
            return BadRequest(new { success = false, message = "Giá tối thiểu không được lớn hơn giá tối đa." });
        if (request.Deadline.HasValue && request.Deadline.Value.Date < DateTime.UtcNow.Date)
            return BadRequest(new { success = false, message = "Hạn nhận hàng không thể ở trong quá khứ." });
        var factory = await CurrentFactory(ct);
        var demand = new FactoryDemand { Id = Guid.NewGuid(), FactoryId = factory.Id, MaterialType = request.MaterialType, QuantityKg = request.QuantityKg, MinPricePerKg = request.MinPricePerKg, PricePerKg = request.MaxPricePerKg, Deadline = request.Deadline, IsActive = request.IsActive, Note = request.Note?.Trim() };
        Db.FactoryDemands.Add(demand);
        await Db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { id = demand.Id }, new { success = true, data = DemandView(demand) });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, DemandRequest request, CancellationToken ct)
    {
        if (request.MinPricePerKg.HasValue && request.MaxPricePerKg.HasValue && request.MinPricePerKg > request.MaxPricePerKg)
            return BadRequest(new { success = false, message = "Giá tối thiểu không được lớn hơn giá tối đa." });
        var factory = await CurrentFactory(ct);
        var demand = await Db.FactoryDemands.SingleOrDefaultAsync(x => x.Id == id && x.FactoryId == factory.Id, ct);
        if (demand is null) return NotFound(new { success = false, message = "Không tìm thấy nhu cầu." });
        demand.MaterialType = request.MaterialType; demand.QuantityKg = request.QuantityKg;
        demand.MinPricePerKg = request.MinPricePerKg; demand.PricePerKg = request.MaxPricePerKg;
        demand.Deadline = request.Deadline; demand.IsActive = request.IsActive; demand.Note = request.Note?.Trim();
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
