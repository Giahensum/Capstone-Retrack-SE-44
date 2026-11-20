namespace Retrack.API.Services.Money;

/// <summary>Tính hoa hồng thuần (không truy cập DB). Tiền VND là số nguyên; làm tròn nửa lên tới 1 đồng.</summary>
public static class CommissionCalculator
{
    /// <summary>Tỷ lệ riêng của người nhận (nếu có) thắng mặc định chung của dòng tiền.</summary>
    public static decimal ResolveRate(decimal? payeeOverride, decimal defaultRate) => Validate(payeeOverride ?? defaultRate);

    /// <summary>commission = làm tròn nửa lên(gross × rate ÷ 100) tới 1 đồng.</summary>
    public static decimal Commission(decimal gross, decimal rate)
    {
        if (gross < 0) throw new ArgumentOutOfRangeException(nameof(gross), "Số tiền không được âm.");
        return decimal.Round(gross * Validate(rate) / 100m, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Hoa hồng hoàn lại cho một lần hoàn một phần, chia theo tỷ lệ TÍCH LŨY: tổng hoa hồng hoàn sau mọi lần
    /// luôn bằng đúng hoa hồng gốc khi hoàn đủ gross, không lệch làm tròn.
    /// Ví dụ gross 100, hoa hồng 5: hoàn 33, 33, 34 cho 2, 1, 2 (tổng 5).
    /// </summary>
    /// <param name="refundedGrossSoFar">Tổng gross đã hoàn trước lần này.</param>
    /// <param name="refundedCommissionSoFar">Tổng hoa hồng đã hoàn trước lần này.</param>
    public static decimal RefundCommission(decimal originalGross, decimal originalCommission, decimal refundedGrossSoFar,
        decimal refundedCommissionSoFar, decimal refundGross)
    {
        if (originalGross < 0 || originalCommission < 0 || refundedGrossSoFar < 0 || refundedCommissionSoFar < 0)
            throw new ArgumentOutOfRangeException(nameof(originalGross), "Số tiền không được âm.");
        if (refundGross <= 0) throw new ArgumentOutOfRangeException(nameof(refundGross), "Số tiền hoàn phải lớn hơn 0.");
        var cumulative = refundedGrossSoFar + refundGross;
        if (cumulative > originalGross) throw new ArgumentOutOfRangeException(nameof(refundGross), "Tổng hoàn vượt quá số tiền gốc.");
        var cumulativeCommission = cumulative == originalGross
            ? originalCommission
            : decimal.Round(originalCommission * cumulative / originalGross, 0, MidpointRounding.AwayFromZero);
        return Math.Clamp(cumulativeCommission - refundedCommissionSoFar, 0m, originalCommission - refundedCommissionSoFar);
    }

    private static decimal Validate(decimal rate)
    {
        if (rate < 0 || rate > 100) throw new ArgumentOutOfRangeException(nameof(rate), "Tỷ lệ hoa hồng phải trong khoảng 0 đến 100.");
        return rate;
    }
}
