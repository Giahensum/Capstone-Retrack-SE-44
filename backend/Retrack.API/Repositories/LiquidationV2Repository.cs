using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

/// <summary>Truy vấn/khóa phiên nhiều hạng mục. Khóa phiên (FOR UPDATE trên liquidation_lots) tuần tự hóa mọi lượt giá và lần đóng phiên.</summary>
public sealed class LiquidationV2Repository(AppDbContext db) : ILiquidationV2Repository
{
    public Task<bool> IsActiveSellerAsync(Guid id) => db.Users.AnyAsync(x => x.Id == id && x.Role == "SELLER" && x.IsActive);
    public Task LockOperationAsync(Guid id) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()}, 0))");
    public Task<LiquidationLot?> FindLotAsync(Guid id) => db.LiquidationLots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.FormatVersion == 2);
    public async Task<LiquidationLot?> LockLotAsync(Guid id)
    {
        var lot = await db.LiquidationLots.FromSqlInterpolated($"SELECT * FROM liquidation_lots WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync();
        if (lot == null) return null;
        await db.Entry(lot).ReloadAsync();
        return lot.FormatVersion == 2 ? lot : null;
    }
    // Gọi sau LockLotAsync: các dòng hạng mục được đọc lại trong cùng transaction để cập nhật giá cao nhất.
    public async Task<List<LiquidationItem>> ItemsAsync(Guid lotId)
    {
        var items = await db.LiquidationItems.Where(x => x.LotId == lotId).OrderBy(x => x.Position).ToListAsync();
        foreach (var item in items) await db.Entry(item).ReloadAsync();
        return items;
    }
    public Task<LiquidationParticipation?> ParticipationAsync(Guid lotId, Guid ownerId) =>
        db.LiquidationParticipations.SingleOrDefaultAsync(x => x.LotId == lotId && x.OwnerId == ownerId);
    public Task<List<Guid>> ParticipationItemIdsAsync(Guid participationId) =>
        db.LiquidationParticipationItems.AsNoTracking().Where(x => x.ParticipationId == participationId).Select(x => x.ItemId).ToListAsync();
    public Task<List<LiquidationParticipation>> ParticipantsAsync(Guid lotId) =>
        db.LiquidationParticipations.AsNoTracking().Where(x => x.LotId == lotId).ToListAsync();
    public Task<Dictionary<Guid, int>> ItemParticipantCountsAsync(Guid lotId) =>
        db.LiquidationParticipationItems.AsNoTracking().Where(x => x.Participation.LotId == lotId)
            .GroupBy(x => x.ItemId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
    public Task<int> ParticipantCountAsync(Guid lotId) => db.LiquidationParticipations.CountAsync(x => x.LotId == lotId);
    public async Task<List<LiquidationLatestBid>> MyLatestBidsAsync(Guid lotId, Guid ownerId, Guid depotId) =>
        (await db.LiquidationItemBids.AsNoTracking().Where(x => x.LotId == lotId && x.OwnerId == ownerId && x.DepotId == depotId)
            .Select(x => new { x.ItemId, x.UnitPrice, x.CreatedAt }).ToListAsync())
        .GroupBy(x => x.ItemId).Select(g => g.OrderByDescending(x => x.UnitPrice).ThenByDescending(x => x.CreatedAt).First())
        .Select(x => new LiquidationLatestBid(x.ItemId, x.UnitPrice, x.CreatedAt)).ToList();
    public Task<LiquidationItemBid?> FindBidAsync(Guid id) => db.LiquidationItemBids.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
    public Task<List<LiquidationItemBid>> BidsByIdsAsync(IEnumerable<Guid> ids)
    {
        var list = ids.ToList();
        return db.LiquidationItemBids.AsNoTracking().Where(x => list.Contains(x.Id)).ToListAsync();
    }
    public async Task<PagedResult<LiquidationItemBid>> ItemBidsAsync(Guid itemId, int page, Guid? ownerId = null, Guid? depotId = null)
    {
        const int size = 20;
        var source = db.LiquidationItemBids.AsNoTracking().Where(x => x.ItemId == itemId);
        if (ownerId.HasValue) source = source.Where(x => x.OwnerId == ownerId && x.DepotId == depotId);
        var total = await source.CountAsync();
        var offset = (long)(page - 1) * size;
        return new() { Page = page, PageSize = size, TotalCount = total, Items = offset > int.MaxValue ? [] :
            await source.OrderByDescending(x => x.UnitPrice).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip((int)offset).Take(size).ToListAsync() };
    }
    public Task<int> CountOpenLotsAsync(Guid sellerId) => db.LiquidationLots.CountAsync(x => x.SellerId == sellerId && x.Status == "PUBLISHED");

    public async Task<PagedResult<LiquidationV2ListRow>> DepotListAsync(Guid ownerId, Guid depotId, string? view, string? search, int page, int pageSize)
    {
        var source = db.LiquidationLots.AsNoTracking().Where(x => x.FormatVersion == 2);
        var mine = db.LiquidationParticipations.Where(p => p.OwnerId == ownerId && p.DepotId == depotId);
        var bought = db.LiquidationAgreements.Where(a => a.FormatVersion == 2 && a.BuyerId == ownerId && a.DepotId == depotId);
        var done = bought.Where(a => a.Resolution == "CANCELLED" || a.OwnerCollectedAt != null);
        source = view switch
        {
            "OPEN" => source.Where(x => x.Status == "PUBLISHED" && x.Seller.IsActive),
            "PURCHASES" => source.Where(x => bought.Any(a => a.LotId == x.Id) && !done.Any(a => a.LotId == x.Id)),
            "HISTORY" => source.Where(x => x.Status != "PUBLISHED" && (done.Any(a => a.LotId == x.Id) ||
                mine.Any(p => p.LotId == x.Id) && !bought.Any(a => a.LotId == x.Id))),
            _ => source.Where(x => x.Seller.IsActive && (x.Status != "CANCELLED" || mine.Any(p => p.LotId == x.Id)))
        };
        if (!string.IsNullOrWhiteSpace(search)) source = source.Where(x => x.Title.Contains(search) || x.PublicArea.Contains(search));
        var total = await source.CountAsync();
        var offset = (long)(page - 1) * pageSize;
        var ordered = view == "OPEN" ? source.OrderBy(x => x.BiddingEnd).ThenBy(x => x.Id) : source.OrderByDescending(x => x.BiddingEnd).ThenBy(x => x.Id);
        var rows = offset > int.MaxValue ? [] : await ordered.Skip((int)offset).Take(pageSize).Select(x => new
        {
            Lot = x,
            ItemCount = db.LiquidationItems.Count(i => i.LotId == x.Id),
            Names = db.LiquidationItems.Where(i => i.LotId == x.Id).OrderBy(i => i.Position).Select(i => i.Name).Take(4).ToList(),
            Registered = mine.Any(p => p.LotId == x.Id),
            AgreementId = bought.Where(a => a.LotId == x.Id).Select(a => (Guid?)a.Id).FirstOrDefault(),
            RegisteredItems = db.LiquidationParticipationItems.Count(pi => pi.Participation.LotId == x.Id && pi.Participation.OwnerId == ownerId && pi.Participation.DepotId == depotId),
            Won = db.LiquidationAgreementItems.Count(ai => ai.Agreement.LotId == x.Id && ai.Agreement.BuyerId == ownerId && ai.Agreement.DepotId == depotId),
        }).ToListAsync();
        return new() { Page = page, PageSize = pageSize, TotalCount = total,
            Items = rows.Select(r => new LiquidationV2ListRow(r.Lot, r.ItemCount, [.. r.Names], r.Registered, r.AgreementId, r.RegisteredItems, r.Won, 0)).ToList() };
    }

    public async Task<PagedResult<LiquidationV2ListRow>> SellerListAsync(Guid sellerId, string? search, int page, int pageSize)
    {
        var source = db.LiquidationLots.AsNoTracking().Where(x => x.FormatVersion == 2 && x.SellerId == sellerId);
        if (!string.IsNullOrWhiteSpace(search)) source = source.Where(x => x.Title.Contains(search) || x.PublicArea.Contains(search));
        var total = await source.CountAsync();
        var offset = (long)(page - 1) * pageSize;
        var rows = offset > int.MaxValue ? [] : await source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((int)offset).Take(pageSize).Select(x => new
        {
            Lot = x,
            ItemCount = db.LiquidationItems.Count(i => i.LotId == x.Id),
            Names = db.LiquidationItems.Where(i => i.LotId == x.Id).OrderBy(i => i.Position).Select(i => i.Name).Take(4).ToList(),
            Won = db.LiquidationItems.Count(i => i.LotId == x.Id && i.Status == "WON"),
            Participants = db.LiquidationParticipations.Count(p => p.LotId == x.Id),
        }).ToListAsync();
        return new() { Page = page, PageSize = pageSize, TotalCount = total,
            Items = rows.Select(r => new LiquidationV2ListRow(r.Lot, r.ItemCount, [.. r.Names], false, null, 0, r.Won, r.Participants)).ToList() };
    }

    public Task<List<Guid>> ExpiredAsync(DateTime now) => db.LiquidationLots.AsNoTracking()
        .Where(x => x.FormatVersion == 2 && x.Status == "PUBLISHED" && x.BiddingEnd <= now)
        .OrderBy(x => x.BiddingEnd).ThenBy(x => x.Id).Take(100).Select(x => x.Id).ToListAsync();

    public async Task<LiquidationAgreement?> FindAgreementAsync(Guid id)
    {
        var agreement = await db.LiquidationAgreements.Include(x => x.Lot).Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id && x.FormatVersion == 2);
        if (agreement != null) await db.Entry(agreement).ReloadAsync();
        return agreement;
    }
    public Task<LiquidationAgreement?> MyAgreementAsync(Guid lotId, Guid ownerId, Guid depotId) => db.LiquidationAgreements.AsNoTracking()
        .SingleOrDefaultAsync(x => x.FormatVersion == 2 && x.LotId == lotId && x.BuyerId == ownerId && x.DepotId == depotId);
    public Task<List<LiquidationAgreement>> LotAgreementsAsync(Guid lotId) => db.LiquidationAgreements.AsNoTracking()
        .Include(x => x.Lot).Include(x => x.Items).Where(x => x.FormatVersion == 2 && x.LotId == lotId).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync();
    public Task<DepotStockReceipt?> ReceiptAsync(Guid agreementId) => db.DepotStockReceipts.AsNoTracking()
        .SingleOrDefaultAsync(x => x.BulkAgreementId == agreementId && x.Status != "VOIDED" && x.Status != "REJECTED");
    public Task<Dictionary<Guid, string>> DepotNamesAsync(IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList();
        return db.Depots.AsNoTracking().Where(x => list.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
    }
    public Task<User?> UserAsync(Guid id) => db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);

    public async Task<PagedResult<LiquidationDisputeRow>> DisputesAsync(bool openOnly, int page, int pageSize)
    {
        var source = db.LiquidationAgreements.AsNoTracking().Where(x => x.FormatVersion == 2 && x.AbandonReportedAt != null);
        if (openOnly) source = source.Where(x => x.Resolution == null);
        var total = await source.CountAsync();
        var offset = (long)(page - 1) * pageSize;
        var rows = offset > int.MaxValue ? [] : await source.OrderBy(x => x.Resolution != null).ThenByDescending(x => x.AbandonReportedAt).ThenBy(x => x.Id)
            .Skip((int)offset).Take(pageSize)
            .Select(x => new { A = x, x.Lot.Title, Seller = x.Lot.Seller.FullName, x.Lot.SellerId, Depot = x.Depot.Name }).ToListAsync();
        return new() { Page = page, PageSize = pageSize, TotalCount = total,
            Items = rows.Select(r => new LiquidationDisputeRow(r.A, r.Title, r.Seller, r.Depot, r.SellerId)).ToList() };
    }

    public void AddLot(LiquidationLot lot) => db.LiquidationLots.Add(lot);
    public void AddItem(LiquidationItem item) => db.LiquidationItems.Add(item);
    public void AddParticipation(LiquidationParticipation participation) => db.LiquidationParticipations.Add(participation);
    public void AddParticipationItem(LiquidationParticipationItem item) => db.LiquidationParticipationItems.Add(item);
    public void AddBid(LiquidationItemBid bid) => db.LiquidationItemBids.Add(bid);
    public void AddAgreement(LiquidationAgreement agreement) => db.LiquidationAgreements.Add(agreement);
    public async Task<LiquidationHandover?> HandoverAsync(Guid agreementId, int revision)
    {
        var row=await db.LiquidationHandovers.Include(x=>x.Lines).SingleOrDefaultAsync(x=>x.AgreementId==agreementId && x.Revision==revision);
        if(row!=null)await db.Entry(row).ReloadAsync();
        return row;
    }
    public Task<LiquidationHandover?> HandoverOperationAsync(Guid id) => db.LiquidationHandovers.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id);
    public void AddHandover(LiquidationHandover handover) => db.LiquidationHandovers.Add(handover);
    public void AddNotice(Notification notice) => db.Notifications.Add(notice);
}
