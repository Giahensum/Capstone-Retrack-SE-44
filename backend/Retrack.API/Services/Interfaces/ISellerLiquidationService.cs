using Retrack.API.DTOs;
using Retrack.API.DTOs.Seller;

namespace Retrack.API.Services.Interfaces;

public interface ISellerLiquidationService
{
    Task<SellerLiquidationDetailDto> CreateAsync(Guid sellerId, CreateLiquidationLotDto dto);
    Task<PagedResult<SellerLiquidationSummaryDto>> ListAsync(Guid sellerId, SellerLiquidationQuery query);
    Task<SellerLiquidationDetailDto> DetailAsync(Guid sellerId, Guid lotId);
    Task<PagedResult<SellerLiquidationBidDto>> BidsAsync(Guid sellerId, Guid lotId, int page);
    Task CancelAsync(Guid sellerId, Guid lotId, string reason);
    Task<SellerLiquidationAgreementDto> ConfirmPaymentAsync(Guid sellerId, Guid agreementId);
    Task<SellerLiquidationAgreementDto> ConfirmHandoverAsync(Guid sellerId, Guid agreementId);
    Task<SellerLiquidationAgreementDto> SignAsync(Guid sellerId, Guid agreementId);
    Task<SellerLiquidationAgreementDto> ConfirmDepositAsync(Guid sellerId, Guid agreementId);
}
