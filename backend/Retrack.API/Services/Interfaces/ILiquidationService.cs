using Retrack.API.DTOs;
using Retrack.API.DTOs.Liquidation;

namespace Retrack.API.Services.Interfaces;

public interface ILiquidationService
{
    Task<PagedResult<LiquidationSummaryDto>> ListAsync(Guid userId, Guid? depotId, LiquidationQuery query);
    Task<LiquidationDetailDto> DetailAsync(Guid userId, Guid? depotId, Guid lotId);
    Task ParticipateAsync(Guid ownerId, Guid depotId, Guid lotId, bool consent, bool declareSurvey);
    Task<MyLiquidationBidDto> BidAsync(Guid ownerId, Guid depotId, Guid lotId, LiquidationBidInput dto);
    Task<PagedResult<MyLiquidationBidDto>> MyBidsAsync(Guid ownerId, Guid depotId, Guid lotId, int page);
    Task FinalizeAsync(Guid lotId);
    Task<PagedResult<LiquidationLadderDto>> LadderAsync(Guid ownerId, Guid depotId, Guid lotId, int page);
    Task<List<Guid>> ExpiredAsync();
    Task<LiquidationAgreementDto> AgreementAsync(Guid userId, Guid? depotId, Guid agreementId);
    Task AgreementActionAsync(Guid userId, Guid? depotId, Guid agreementId, string action, string? reference = null);
}
