using Retrack.API.DTOs;
using Retrack.API.DTOs.Liquidation;
using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public interface ILiquidationRepository
{
    Task<bool> IsActiveSellerAsync(Guid id);
    Task<LiquidationLot?> FindAsync(Guid id);
    Task<LiquidationLot?> LockAsync(Guid id);
    Task LockCreateAsync(Guid id);
    Task<PagedResult<LiquidationLot>> ListAsync(Guid userId, Guid depotId, LiquidationQuery query, DateTime now);
    Task<LiquidationParticipation?> ParticipationAsync(Guid lotId, Guid ownerId);
    void AddParticipation(LiquidationParticipation participation);
    Task<LiquidationBid?> FindBidAsync(Guid id);
    void AddBid(LiquidationBid bid);
    void AddNotice(Notification notice);
    Task<List<LiquidationParticipation>> ParticipantsAsync(Guid lotId);
    Task<PagedResult<MyLiquidationBidDto>> MyBidsAsync(Guid lotId, Guid ownerId, Guid depotId, int page);
    Task<LiquidationAgreement?> FindAgreementAsync(Guid id);
    Task<LiquidationAgreement?> LotAgreementAsync(Guid lotId);
    void AddAgreement(LiquidationAgreement agreement);
    Task<DepotStockReceipt?> AgreementReceiptAsync(Guid id);
    Task<PagedResult<LiquidationLadderDto>> LadderAsync(Guid lotId, Guid? leadingBidId, Guid ownerId, Guid depotId, int page);
    Task<ContractPartiesDto> ContractPartiesAsync(LiquidationAgreement agreement);
    Task<List<Guid>> ExpiredAsync(DateTime now);
}
