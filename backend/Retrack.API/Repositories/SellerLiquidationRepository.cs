using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Seller;
using Retrack.API.DTOs.Liquidation;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class SellerLiquidationRepository(AppDbContext db) : ISellerLiquidationRepository
{
    private const int BidPageSize = 20;
    public Task<bool> IsActiveSellerAsync(Guid id) => db.Users.AnyAsync(x => x.Id == id && x.Role == "SELLER" && x.IsActive);
    public Task LockCreateAsync(Guid id) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()}, 0))");
    public async Task<LiquidationLot?> LockLotAsync(Guid id)
    {
        var lot = await db.LiquidationLots.FromSqlInterpolated($"SELECT * FROM liquidation_lots WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync();
        if (lot != null) await db.Entry(lot).ReloadAsync();
        return lot;
    }
    public Task<LiquidationLot?> FindLotAsync(Guid id) => db.LiquidationLots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
    public Task<int> CountOpenLotsAsync(Guid sellerId) => db.LiquidationLots.CountAsync(x => x.SellerId == sellerId && x.Status == "PUBLISHED");
    public void AddLot(LiquidationLot lot) => db.LiquidationLots.Add(lot);
    public void AddNotice(Notification notice) => db.Notifications.Add(notice);
    public Task<int> ParticipantCountAsync(Guid lotId) => db.LiquidationParticipations.CountAsync(x => x.LotId == lotId);
    public Task<List<LiquidationParticipation>> ParticipantsAsync(Guid lotId) => db.LiquidationParticipations.AsNoTracking().Where(x => x.LotId == lotId).ToListAsync();
    public async Task<PagedResult<SellerLotRow>> ListAsync(Guid sellerId, SellerLiquidationQuery query, DateTime now)
    {
        var source = db.LiquidationLots.AsNoTracking().Where(x => x.SellerId == sellerId);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(x => x.Title.Contains(query.Search) || x.PublicArea.Contains(query.Search));
        source = query.Status switch
        {
            "SURVEY" => source.Where(x => x.Status == "PUBLISHED" && x.SurveyStart <= now && x.SurveyEnd > now),
            "BIDDING" => source.Where(x => x.Status == "PUBLISHED" && x.BiddingStart <= now && x.BiddingEnd > now),
            "UPCOMING" => source.Where(x => x.Status == "PUBLISHED" && now < x.BiddingStart && (now < x.SurveyStart || now >= x.SurveyEnd)),
            "CLOSING" => source.Where(x => x.Status == "PUBLISHED" && now >= x.BiddingEnd),
            "AWARDED" or "NO_SALE" or "CANCELLED" => source.Where(x => x.Status == query.Status),
            _ => source
        };
        var total = await source.CountAsync();
        var offset = (long)(query.Page - 1) * query.PageSize;
        var items = offset > int.MaxValue ? [] : await source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((int)offset).Take(query.PageSize)
            .Select(x => new SellerLotRow(x, db.LiquidationParticipations.Count(p => p.LotId == x.Id))).ToListAsync();
        return new() { Page = query.Page, PageSize = query.PageSize, TotalCount = total, Items = items };
    }
    public async Task<PagedResult<SellerLiquidationBidDto>> BidsAsync(Guid lotId, Guid? leadingBidId, int page)
    {
        var source = db.LiquidationBids.AsNoTracking().Where(x => x.LotId == lotId);
        var total = await source.CountAsync();
        var offset = (long)(page - 1) * BidPageSize;
        var items = offset > int.MaxValue ? [] : await (from b in source
                                                         join d in db.Depots.AsNoTracking() on b.DepotId equals d.Id
                                                         orderby b.Amount descending, b.CreatedAt, b.Id
                                                         select new SellerLiquidationBidDto(b.Id, d.Name, b.Amount, b.CreatedAt, leadingBidId == b.Id))
            .Skip((int)offset).Take(BidPageSize).ToListAsync();
        return new() { Page = page, PageSize = BidPageSize, TotalCount = total, Items = items };
    }
    public Task<LiquidationAgreement?> LotAgreementAsync(Guid lotId) => db.LiquidationAgreements.AsNoTracking().SingleOrDefaultAsync(x => x.LotId == lotId);
    public async Task<LiquidationAgreement?> FindAgreementAsync(Guid id)
    {
        var agreement = await db.LiquidationAgreements.Include(x => x.Lot).SingleOrDefaultAsync(x => x.Id == id);
        if (agreement != null) await db.Entry(agreement).ReloadAsync();
        return agreement;
    }
    public Task<ContractPartiesDto> ContractPartiesAsync(LiquidationAgreement agreement) => db.ContractPartiesAsync(agreement);
    public async Task<string> DepotNameAsync(Guid depotId) => await db.Depots.AsNoTracking().Where(x => x.Id == depotId).Select(x => x.Name).SingleOrDefaultAsync() ?? "";
}
