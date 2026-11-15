using System.Globalization;

namespace Retrack.API.Services.Shared;

public readonly record struct FeeBreakdown(decimal Fee, decimal SellerReceives, decimal BuyerTotal);

public static class PlatformFees
{
    public const string Legacy = "LEGACY_SELLER_DEDUCTION_V1";
    public const string BuyerSeparate = "BUYER_SEPARATE_V1";

    public static decimal Rate(string? configured)
    {
        if (configured is null) return 5m;
        if (!decimal.TryParse(configured, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate) || rate is < 0 or > 100 || decimal.Round(rate, 2) != rate)
            throw new InvalidOperationException("Cấu hình phí nền tảng không hợp lệ.");
        return rate;
    }

    public static FeeBreakdown Pickup(decimal gross, decimal rate, string policy) =>
        Calculate(gross, gross, rate, policy, policy == Legacy ? 2 : 0);

    public static FeeBreakdown Settlement(decimal goods, decimal invoiceTotal, decimal rate, string policy) =>
        Calculate(goods, invoiceTotal, rate, policy, 0);

    private static FeeBreakdown Calculate(decimal goods, decimal invoiceTotal, decimal rate, string policy, int digits)
    {
        if (goods < 0 || invoiceTotal < goods || rate is < 0 or > 100 || policy is not (Legacy or BuyerSeparate))
            throw new ArgumentException("Snapshot phí hoặc tiền hàng không hợp lệ.");
        // Đơn cũ giữ cách làm tròn hai số lẻ; chính sách mới dùng VND nguyên.
        var fee = decimal.Round(goods * rate / 100m, digits,
            policy == Legacy && digits == 2 ? MidpointRounding.ToEven : MidpointRounding.AwayFromZero);
        return policy == BuyerSeparate
            ? new(fee, invoiceTotal, invoiceTotal + fee)
            : new(fee, invoiceTotal - fee, invoiceTotal);
    }
}
