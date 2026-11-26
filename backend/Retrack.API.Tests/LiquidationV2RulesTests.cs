using Retrack.API.Models;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Liquidation;
using Xunit;

namespace Retrack.API.Tests;

// Luật thuần của phiên nhiều hạng mục: đơn vị/số lượng, lưới bước giá, tiền theo lượng đã đăng và gia hạn 2 phút x tối đa 5 lần.
public sealed class LiquidationV2RulesTests
{
    private static LiquidationItem Item(decimal? highest = null) => new()
    { Name = "Sắt vụn", Unit = "kg", Kind = "MATERIAL", Quantity = 100, StartingUnitPrice = 3000, BidIncrement = 100, HighestUnitPrice = highest, HighestBidId = highest.HasValue ? Guid.NewGuid() : null };

    [Theory]
    [InlineData("kg", 100.125, true)]
    [InlineData("kg", 100.1255, false)]
    [InlineData("kg", 0, false)]
    [InlineData("cai", 3, true)]
    [InlineData("cai", 2.5, false)]
    [InlineData("bo", 1, true)]
    [InlineData("tan", 1, false)]
    public void Item_quantity_follows_unit(string unit, double quantity, bool ok)
    {
        void Check() => LiquidationV2Rules.CheckItem("Hạng mục", "Mô tả đủ dài cho loại và tình trạng", unit, (decimal)quantity, 1000, 100);
        if (ok) Check(); else Assert.Throws<ArgumentException>(Check);
    }

    [Fact]
    public void Used_goods_need_type_condition_or_set_composition()
    {
        Assert.Throws<ArgumentException>(() => LiquidationV2Rules.CheckItem("Tivi", "cũ", "cai", 2, 500000, 10000));
        Assert.Throws<ArgumentException>(() => LiquidationV2Rules.CheckItem("Bộ bàn ghế", "bộ", "bo", 1, 500000, 10000));
        LiquidationV2Rules.CheckItem("Bộ bàn ghế", "1 bàn gỗ + 4 ghế, trầy nhẹ", "bo", 1, 500000, 10000);
    }

    [Fact]
    public void First_bid_must_be_at_least_starting_and_on_grid()
    {
        LiquidationV2Rules.CheckBid(Item(), 3000);
        LiquidationV2Rules.CheckBid(Item(), 3300);
        Assert.Throws<DepotConflictException>(() => LiquidationV2Rules.CheckBid(Item(), 2900));
        Assert.Throws<ArgumentException>(() => LiquidationV2Rules.CheckBid(Item(), 3050));
    }

    [Fact]
    public void Next_bid_must_beat_highest_by_increment_and_stay_on_grid()
    {
        Assert.Equal(3200, LiquidationV2Rules.NextMin(Item(3100)));
        Assert.Throws<DepotConflictException>(() => LiquidationV2Rules.CheckBid(Item(3100), 3100));
        Assert.Throws<ArgumentException>(() => LiquidationV2Rules.CheckBid(Item(3100), 3250));
        LiquidationV2Rules.CheckBid(Item(3100), 3200);
    }

    [Fact]
    public void Purchase_amount_uses_posted_quantity_rounded_once()
    {
        Assert.Equal(300_000, LiquidationV2Rules.Amount(100, 3000));
        Assert.Equal(3_752, LiquidationV2Rules.Amount(1.2505m, 3000)); // 3751,5 làm tròn lên 3752
    }

    [Fact]
    public void Extension_adds_two_minutes_at_most_five_times()
    {
        var end = new DateTime(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);
        short count = 0;
        // Ngoài 2 phút cuối: không gia hạn.
        Assert.False(LiquidationV2Rules.Extend(end.AddMinutes(-3), end, count).Extended);
        for (var i = 1; i <= 5; i++)
        {
            var result = LiquidationV2Rules.Extend(end.AddSeconds(-30), end, count);
            Assert.True(result.Extended);
            Assert.Equal(end.AddMinutes(2), result.End);
            (end, count) = (result.End, result.Count);
        }
        Assert.Equal(5, count);
        var capped = LiquidationV2Rules.Extend(end.AddSeconds(-10), end, count);
        Assert.False(capped.Extended);
        Assert.Equal(end, capped.End);
    }
}
