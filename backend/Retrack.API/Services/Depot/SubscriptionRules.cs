using System.Globalization;
using Retrack.API.Models;

namespace Retrack.API.Services.Depot;

public static class SubscriptionRules
{
    public const int TrialDays = 14;
    public const decimal DefaultPrice = 199000m;
    public static bool Enabled(IReadOnlyDictionary<string, string> settings) => settings.GetValueOrDefault("BUSINESS_MODEL") == "DEPOT_SUBSCRIPTION";
    public static decimal Price(IReadOnlyDictionary<string, string> settings) => settings.TryGetValue("SUBSCRIPTION_MONTHLY_PRICE", out var text)
        && decimal.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var price) && price is >= 1000 and <= 10000000 ? price : DefaultPrice;
    public static bool HasAccess(DepotSubscription? subscription, DateTime now) => subscription?.PaidUntil > now || subscription?.TrialEndsAt > now;
    public static DateTime RenewalStart(DepotSubscription subscription, DateTime now) => new[] { now, subscription.PaidUntil ?? now, subscription.TrialEndsAt ?? now }.Max();
    // Cộng một tháng lịch tại Việt Nam; 31/01 → ngày cuối tháng 02, không coi tháng nào cũng 30 ngày.
    public static DateTime AddMonth(DateTime utc) => DateTime.SpecifyKind(utc.AddHours(7).AddMonths(1).AddHours(-7), DateTimeKind.Utc);
}
