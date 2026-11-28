using Retrack.API.Models;

namespace Retrack.API.Helpers;

public static class LiquidationSourceRevision
{
    public static string For(LiquidationAgreement a) => string.Join('.', new[]
        {a.PaymentConfirmedAt,a.SellerHandedOverAt,a.OwnerCollectedAt,a.AbandonReportedAt,a.ResolvedAt}.Select(x => (x?.Ticks / 10)?.ToString() ?? "0"))
        + $".{a.HandoverRevision}.{a.HandoverStatus}.{a.PricingBasis}";
}
