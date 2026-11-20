using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;

namespace Retrack.API.Services.Money;

/// <summary>Các khóa cấu hình thu/chi trong bảng system_configs (config_key tối đa 50 ký tự).</summary>
public static class MoneyConfigKeys
{
    public const string Mode = "money.mode";
    public const string CommissionDefaultPrefix = "commission.default.";
    public const string CommissionDefaultPickup = "commission.default.PICKUP";
    public const string CommissionDefaultBatch = "commission.default.BATCH";
    public const string CommissionDefaultLiquidation = "commission.default.LIQUIDATION";
    public const string PayoutAutoEnabled = "payout.auto_enabled";
    public const string PayoutSchedule = "payout.schedule";
    public const string PayoutHoldMinutes = "payout.hold_minutes";
    public const string PayoutMinAmount = "payout.min_amount";
    public const string PayoutAutoMaxAmount = "payout.auto_max_amount";
    public const string PayoutProvider = "payout.provider";
    public const string PayoutTestEnabled = "payout.test_enabled";
    public const string PayoutTestMaxAmount = "payout.test_max_amount";

    public const string ModeLegacy = "LEGACY";
    public const string ModePlatform = "PLATFORM";

    public static string CommissionDefaultKey(string flow) => CommissionDefaultPrefix + flow;

    public static readonly string[] All =
    [
        Mode, CommissionDefaultPickup, CommissionDefaultBatch, CommissionDefaultLiquidation, PayoutAutoEnabled, PayoutSchedule,
        PayoutHoldMinutes, PayoutMinAmount, PayoutAutoMaxAmount, PayoutProvider, PayoutTestEnabled, PayoutTestMaxAmount
    ];
}

/// <summary>Ảnh chụp cấu hình tại một thời điểm. Khóa thiếu hoặc sai định dạng dùng giá trị mặc định an toàn.</summary>
public sealed record MoneyConfig(
    string Mode, IReadOnlyDictionary<string, decimal> DefaultCommission, bool PayoutAutoEnabled, string PayoutSchedule,
    int PayoutHoldMinutes, decimal PayoutMinAmount, decimal PayoutAutoMaxAmount, string PayoutProvider,
    bool PayoutTestEnabled, decimal PayoutTestMaxAmount)
{
    public bool IsPlatformMode => Mode == MoneyConfigKeys.ModePlatform;

    public decimal DefaultRate(string flow) => DefaultCommission.TryGetValue(flow, out var rate) ? rate : 0m;

    /// <summary>Mặc định an toàn: LEGACY, hoa hồng 0, giữ 24 giờ, chi tay, nhà cung cấp mô phỏng.</summary>
    public static MoneyConfig FromValues(IReadOnlyDictionary<string, string> v)
    {
        string Text(string key, string fallback) => v.TryGetValue(key, out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : fallback;
        decimal Dec(string key, decimal fallback, decimal min = 0, decimal max = decimal.MaxValue) =>
            v.TryGetValue(key, out var s) && decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && d >= min && d <= max ? d : fallback;
        bool Bool(string key, bool fallback) => v.TryGetValue(key, out var s) && bool.TryParse(s?.Trim(), out var b) ? b : fallback;

        var mode = Text(MoneyConfigKeys.Mode, "LEGACY").ToUpperInvariant();
        if (mode != MoneyConfigKeys.ModePlatform) mode = MoneyConfigKeys.ModeLegacy;
        var schedule = Text(MoneyConfigKeys.PayoutSchedule, "DAILY").ToUpperInvariant();
        if (schedule != "WEEKLY") schedule = "DAILY";
        var provider = Text(MoneyConfigKeys.PayoutProvider, "SIMULATED").ToUpperInvariant();
        if (provider != "PAYOS") provider = "SIMULATED";
        var rates = MoneyFlows.CommissionFlows.ToDictionary(f => f, f => Dec(MoneyConfigKeys.CommissionDefaultKey(f), 0m, 0m, 100m));
        return new(mode, rates, Bool(MoneyConfigKeys.PayoutAutoEnabled, false), schedule,
            (int)Dec(MoneyConfigKeys.PayoutHoldMinutes, 1440m, 0m, 525600m), Dec(MoneyConfigKeys.PayoutMinAmount, 50000m),
            Dec(MoneyConfigKeys.PayoutAutoMaxAmount, 5000000m), provider, Bool(MoneyConfigKeys.PayoutTestEnabled, false),
            Dec(MoneyConfigKeys.PayoutTestMaxAmount, 10000m));
    }
}

/// <summary>Đọc cấu hình thu/chi từ system_configs. Không cache: mỗi lần gọi là một truy vấn nhỏ để đổi cấu hình có hiệu lực ngay.</summary>
public sealed class MoneyConfigReader(AppDbContext db)
{
    public async Task<MoneyConfig> GetAsync(CancellationToken ct = default)
    {
        var values = await db.SystemConfigs.AsNoTracking().Where(c => MoneyConfigKeys.All.Contains(c.ConfigKey))
            .ToDictionaryAsync(c => c.ConfigKey, c => c.ConfigValue, ct);
        return MoneyConfig.FromValues(values);
    }
}
