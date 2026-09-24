using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Models.Enums;

namespace Retrack.API.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
public class AdminController(AppDbContext db) : ControllerBase
{
    [HttpGet("market-prices")]
    public async Task<IActionResult> MarketPrices(CancellationToken ct)
    {
        var prices = await db.MarketPrices.AsNoTracking()
            .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        return Ok(new { success = true, data = prices });
    }

    [HttpPut("market-prices/{materialType}")]
    public async Task<IActionResult> UpdateMarketPrice(string materialType, [FromBody] UpdateMarketPriceRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<MaterialType>(materialType, true, out var material) || !Enum.IsDefined(material))
            return BadRequest(new { success = false, message = "Loại vật liệu không hợp lệ." });
        if (request.PricePerKg <= 0)
            return BadRequest(new { success = false, message = "Giá phải lớn hơn 0." });
        if (request.EffectiveDate.Kind != DateTimeKind.Utc)
            return BadRequest(new { success = false, message = "Ngày áp dụng phải ở định dạng UTC." });

        var price = new MarketPrice
        {
            MaterialType = material,
            PricePerKg = request.PricePerKg,
            EffectiveDate = request.EffectiveDate,
            Source = string.IsNullOrWhiteSpace(request.Source) ? "Nhập thủ công bởi Admin" : request.Source.Trim()
        };
        db.MarketPrices.Add(price);
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Đã cập nhật giá tham khảo.", data = new { materialType = material.ToString(), price.PricePerKg, price.EffectiveDate, price.Source } });
    }
}

public record UpdateMarketPriceRequest(decimal PricePerKg, DateTime EffectiveDate, string? Source);
