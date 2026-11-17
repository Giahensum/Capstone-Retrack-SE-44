using Retrack.API.Models;

namespace Retrack.API.Services.Shared;

public static class PartnershipPolicy
{
    // Quan hệ cũ chưa có hợp đồng vẫn hoạt động; hợp đồng đã lập phải còn hiệu lực.
    public static bool AllowsDirectDelivery(FactoryDepotPartnership? partner, DateTime now) =>
        partner is { Status: "APPROVED", IsBlocked: false } &&
        (string.IsNullOrEmpty(partner.ContractStatus) && string.IsNullOrEmpty(partner.ContractCode) &&
            !partner.ContractEffectiveAt.HasValue && !partner.ContractExpiresAt.HasValue || partner.ContractStatus == "ACTIVE" &&
            (!partner.ContractEffectiveAt.HasValue || partner.ContractEffectiveAt <= now) &&
            (!partner.ContractExpiresAt.HasValue || partner.ContractExpiresAt > now));
}
