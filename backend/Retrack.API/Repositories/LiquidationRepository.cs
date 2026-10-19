using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Liquidation;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class LiquidationRepository(AppDbContext db) : ILiquidationRepository
{
    public Task<bool> IsActiveSellerAsync(Guid id) => db.Users.AnyAsync(x => x.Id == id && x.Role == "SELLER" && x.IsActive);
    public Task<LiquidationLot?> FindAsync(Guid id) => db.LiquidationLots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
    public Task LockCreateAsync(Guid id) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()}, 0))");
    public async Task<LiquidationLot?> LockAsync(Guid id)
    {
        var lot = await db.LiquidationLots.FromSqlInterpolated($"SELECT * FROM liquidation_lots WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync();
        if (lot != null) await db.Entry(lot).ReloadAsync();
        return lot;
    }
    public async Task<PagedResult<LiquidationLot>> ListAsync(Guid userId, Guid depotId, LiquidationQuery query, DateTime now)
    {
        var source = db.LiquidationLots.AsNoTracking();
        source = source.Where(x => x.Seller.IsActive && (x.Status != "CANCELLED" || db.LiquidationParticipations.Any(p => p.LotId == x.Id && p.OwnerId == userId && p.DepotId == depotId)));
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(x => x.Title.Contains(query.Search) || x.PublicArea.Contains(query.Search));
        if (query.ParticipatingOnly) source = source.Where(x => db.LiquidationParticipations.Any(p => p.LotId == x.Id && p.OwnerId == userId && p.DepotId == depotId));
        source = query.Status switch {
            "SURVEY" => source.Where(x => x.Status == "PUBLISHED" && x.SurveyStart <= now && x.SurveyEnd > now),
            "BIDDING" => source.Where(x => x.Status == "PUBLISHED" && x.BiddingStart <= now && x.BiddingEnd > now),
            "UPCOMING" => source.Where(x => x.Status == "PUBLISHED" && now < x.BiddingStart && (now < x.SurveyStart || now >= x.SurveyEnd)),
            "CLOSING" => source.Where(x => x.Status == "PUBLISHED" && now >= x.BiddingEnd),
            "AWARDED" or "NO_SALE" or "CANCELLED" => source.Where(x => x.Status == query.Status),
            _ => source
        };
        var total = await source.CountAsync();
        var offset = (long)(query.Page - 1) * query.PageSize;
        return new() { Page = query.Page, PageSize = query.PageSize, TotalCount = total,
            Items = offset > int.MaxValue ? [] : await source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
                .Skip((int)offset).Take(query.PageSize).ToListAsync() };
    }
    public Task<LiquidationParticipation?> ParticipationAsync(Guid lotId, Guid ownerId) => db.LiquidationParticipations.SingleOrDefaultAsync(x => x.LotId == lotId && x.OwnerId == ownerId);
    public void AddParticipation(LiquidationParticipation participation) => db.LiquidationParticipations.Add(participation);
    public Task<LiquidationBid?> FindBidAsync(Guid id) => db.LiquidationBids.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
    public void AddBid(LiquidationBid bid) => db.LiquidationBids.Add(bid);
    public async Task<PagedResult<MyLiquidationBidDto>> MyBidsAsync(Guid lotId, Guid ownerId, Guid depotId, int page)
    {
        var source = db.LiquidationBids.AsNoTracking().Where(x => x.LotId == lotId && x.OwnerId == ownerId && x.DepotId == depotId);
        var total = await source.CountAsync();
        var offset = (long)(page - 1) * 20;
        return new() { Page = page, PageSize = 20, TotalCount = total,
            Items = offset > int.MaxValue ? [] : await source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((int)offset).Take(20)
                .Select(x => new MyLiquidationBidDto(x.Id, x.Amount, x.CreatedAt)).ToListAsync() };
    }
    public async Task<LiquidationAgreement?> FindAgreementAsync(Guid id)
    {
        var agreement = await db.LiquidationAgreements.Include(x => x.Lot).SingleOrDefaultAsync(x => x.Id == id);
        if (agreement != null) await db.Entry(agreement).ReloadAsync();
        return agreement;
    }
    public Task<LiquidationAgreement?> LotAgreementAsync(Guid lotId) => db.LiquidationAgreements.AsNoTracking().SingleOrDefaultAsync(x => x.LotId == lotId);
    public void AddAgreement(LiquidationAgreement agreement) => db.LiquidationAgreements.Add(agreement);
    public Task<DepotStockReceipt?> AgreementReceiptAsync(Guid id) => db.DepotStockReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.BulkAgreementId == id && x.Status != "VOIDED" && x.Status != "REJECTED");
    public Task<List<Guid>> ExpiredAsync(DateTime now) => db.LiquidationLots.AsNoTracking()
        .Where(x => x.Status == "PUBLISHED" && x.BiddingEnd <= now).OrderBy(x => x.BiddingEnd).ThenBy(x => x.Id).Take(100).Select(x => x.Id).ToListAsync();
}
