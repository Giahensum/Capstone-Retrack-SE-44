using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Liquidation;
using Retrack.API.Models;

namespace Retrack.API.Repositories;

// Thông tin các bên ghi trên hợp đồng, lấy từ hồ sơ tài khoản/kho; chỉ dùng cho hai bên của thỏa thuận.
internal static class LiquidationContractQueries
{
    public static async Task<ContractPartiesDto> ContractPartiesAsync(this AppDbContext db, LiquidationAgreement agreement)
    {
        var seller = await db.Users.AsNoTracking().Where(u => u.Id == agreement.Lot.SellerId)
            .Select(u => new { u.FullName, u.Phone }).SingleAsync();
        var depot = await db.Depots.AsNoTracking().Where(d => d.Id == agreement.DepotId)
            .Select(d => new { d.Name, d.Address, d.TaxCode, d.ContactPhone, d.OwnerId }).SingleAsync();
        var owner = await db.Users.AsNoTracking().Where(u => u.Id == depot.OwnerId)
            .Select(u => new { u.FullName, u.Phone }).SingleAsync();
        return new(seller.FullName, seller.Phone, owner.FullName, depot.ContactPhone ?? owner.Phone, depot.Name, depot.Address, depot.TaxCode);
    }
}
