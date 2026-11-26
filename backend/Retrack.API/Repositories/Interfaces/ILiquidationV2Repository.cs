using Retrack.API.DTOs;
using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public sealed record LiquidationV2ListRow(LiquidationLot Lot, int ItemCount, string[] ItemNames, bool Registered, Guid? AgreementId,
    int RegisteredItemCount, int WonItemCount, int ParticipantCount);
public sealed record LiquidationLatestBid(Guid ItemId, decimal UnitPrice, DateTime CreatedAt);
public sealed record LiquidationDisputeRow(LiquidationAgreement Agreement, string Title, string SellerName, string DepotName, Guid SellerId);

public interface ILiquidationV2Repository
{
    Task<bool> IsActiveSellerAsync(Guid id);
    Task LockOperationAsync(Guid id);
    Task<LiquidationLot?> FindLotAsync(Guid id);
    Task<LiquidationLot?> LockLotAsync(Guid id);
    Task<List<LiquidationItem>> ItemsAsync(Guid lotId);
    Task<LiquidationParticipation?> ParticipationAsync(Guid lotId, Guid ownerId);
    Task<List<Guid>> ParticipationItemIdsAsync(Guid participationId);
    Task<List<LiquidationParticipation>> ParticipantsAsync(Guid lotId);
    Task<Dictionary<Guid, int>> ItemParticipantCountsAsync(Guid lotId);
    Task<int> ParticipantCountAsync(Guid lotId);
    Task<List<LiquidationLatestBid>> MyLatestBidsAsync(Guid lotId, Guid ownerId, Guid depotId);
    Task<LiquidationItemBid?> FindBidAsync(Guid id);
    Task<List<LiquidationItemBid>> BidsByIdsAsync(IEnumerable<Guid> ids);
    Task<PagedResult<LiquidationItemBid>> ItemBidsAsync(Guid itemId, int page, Guid? ownerId = null, Guid? depotId = null);
    Task<int> CountOpenLotsAsync(Guid sellerId);
    Task<PagedResult<LiquidationV2ListRow>> DepotListAsync(Guid ownerId, Guid depotId, string? view, string? search, int page, int pageSize);
    Task<PagedResult<LiquidationV2ListRow>> SellerListAsync(Guid sellerId, string? search, int page, int pageSize);
    Task<List<Guid>> ExpiredAsync(DateTime now);
    Task<LiquidationAgreement?> FindAgreementAsync(Guid id);
    Task<LiquidationAgreement?> MyAgreementAsync(Guid lotId, Guid ownerId, Guid depotId);
    Task<List<LiquidationAgreement>> LotAgreementsAsync(Guid lotId);
    Task<DepotStockReceipt?> ReceiptAsync(Guid agreementId);
    Task<Dictionary<Guid, string>> DepotNamesAsync(IEnumerable<Guid> ids);
    Task<User?> UserAsync(Guid id);
    Task<PagedResult<LiquidationDisputeRow>> DisputesAsync(bool openOnly, int page, int pageSize);
    void AddLot(LiquidationLot lot);
    void AddItem(LiquidationItem item);
    void AddParticipation(LiquidationParticipation participation);
    void AddParticipationItem(LiquidationParticipationItem item);
    void AddBid(LiquidationItemBid bid);
    void AddAgreement(LiquidationAgreement agreement);
    void AddNotice(Notification notice);
}
