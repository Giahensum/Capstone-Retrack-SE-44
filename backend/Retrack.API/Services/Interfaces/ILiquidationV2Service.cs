using Retrack.API.DTOs;
using Retrack.API.DTOs.Liquidation;

namespace Retrack.API.Services.Interfaces;

/// <summary>Chủ kho: xem phiên, đăng ký hạng mục, khảo sát, trả giá theo đơn giá, thỏa thuận và nguồn nhập kho.</summary>
public interface ILiquidationV2Service
{
    Task<PagedResult<LiquidationV2SummaryDto>> ListAsync(Guid ownerId, Guid depotId, string? view, string? search, int page, int pageSize);
    Task<LiquidationV2DetailDto> DetailAsync(Guid ownerId, Guid depotId, Guid lotId);
    Task<LiquidationV2DetailDto> RegisterAsync(Guid ownerId, Guid depotId, Guid lotId, LiquidationV2ParticipationInput input);
    Task<LiquidationV2DetailDto> ConfirmSurveyAsync(Guid ownerId, Guid depotId, Guid lotId, bool accepted);
    Task<LiquidationV2BidResultDto> BidAsync(Guid ownerId, Guid depotId, Guid lotId, Guid itemId, LiquidationV2BidInput input);
    Task<PagedResult<LiquidationV2MyBidDto>> MyBidsAsync(Guid ownerId, Guid depotId, Guid lotId, Guid itemId, int page);
    Task<LiquidationV2AgreementDto> AgreementAsync(Guid ownerId, Guid depotId, Guid agreementId);
    Task<LiquidationV2AgreementDto> BuyerActionAsync(Guid ownerId, Guid depotId, Guid agreementId, string action, string? text = null);
    Task<LiquidationIntakeSourceDto> IntakeSourceAsync(Guid ownerId, Guid depotId, Guid agreementId);
    Task<List<Guid>> ExpiredAsync();
    Task FinalizeAsync(Guid lotId);
}

/// <summary>Seller: tạo phiên nhiều hạng mục, theo dõi kết quả từng hạng mục và xác nhận thỏa thuận với từng kho thắng.</summary>
public interface ISellerLiquidationV2Service
{
    Task<SellerLiquidationV2DetailDto> CreateAsync(Guid sellerId, CreateLiquidationV2Dto dto);
    Task<PagedResult<SellerLiquidationV2SummaryDto>> ListAsync(Guid sellerId, string? search, int page, int pageSize);
    Task<SellerLiquidationV2DetailDto> DetailAsync(Guid sellerId, Guid lotId);
    Task<PagedResult<SellerLiquidationV2BidDto>> ItemBidsAsync(Guid sellerId, Guid lotId, Guid itemId, int page);
    Task CancelAsync(Guid sellerId, Guid lotId, string reason);
    Task<LiquidationV2AgreementDto> SellerActionAsync(Guid sellerId, Guid agreementId, string action, string? text = null);
}

/// <summary>Admin: danh sách báo bỏ mua và quyết định tiếp tục/hủy thỏa thuận (không phạt, không chuyển người thắng).</summary>
public interface IAdminLiquidationService
{
    Task<PagedResult<LiquidationDisputeDto>> DisputesAsync(bool openOnly, int page, int pageSize);
    Task<LiquidationDisputeDto> ResolveAsync(Guid adminId, Guid agreementId, LiquidationDisputeResolveInput input);
}
