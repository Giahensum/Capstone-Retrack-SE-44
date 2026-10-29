using Retrack.API.DTOs;
using Retrack.API.DTOs.Seller;
using Retrack.API.DTOs.Liquidation;
using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public record SellerLotRow(LiquidationLot Lot, int ParticipantCount);

public interface ISellerLiquidationRepository
{
    Task<bool> IsActiveSellerAsync(Guid id);
    Task LockCreateAsync(Guid id);
    Task<LiquidationLot?> LockLotAsync(Guid id);
    Task<LiquidationLot?> FindLotAsync(Guid id);
    Task<int> CountOpenLotsAsync(Guid sellerId);
    void AddLot(LiquidationLot lot);
    void AddNotice(Notification notice);
    Task<int> ParticipantCountAsync(Guid lotId);
    Task<List<LiquidationParticipation>> ParticipantsAsync(Guid lotId);
    Task<PagedResult<SellerLotRow>> ListAsync(Guid sellerId, SellerLiquidationQuery query, DateTime now);
    Task<PagedResult<SellerLiquidationBidDto>> BidsAsync(Guid lotId, Guid? leadingBidId, int page);
    Task<LiquidationAgreement?> LotAgreementAsync(Guid lotId);
    Task<LiquidationAgreement?> FindAgreementAsync(Guid id);
    Task<string> DepotNameAsync(Guid depotId);
    Task<ContractPartiesDto> ContractPartiesAsync(LiquidationAgreement agreement);
}
