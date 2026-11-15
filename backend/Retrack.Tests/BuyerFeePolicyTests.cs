using Retrack.API.Services.Shared;
using Xunit;

namespace Retrack.Tests;

public sealed class BuyerFeePolicyTests
{
    [Theory]
    [InlineData(1000000, 5, 50000, 1000000, 1050000)]
    [InlineData(1001, 5, 50, 1001, 1051)]
    [InlineData(10, 5, 1, 10, 11)]
    [InlineData(1000, 0, 0, 1000, 1000)]
    [InlineData(1000, 100, 1000, 1000, 2000)]
    public void New_pickup_keeps_seller_whole_and_charges_buyer_separately(decimal gross, decimal rate,
        decimal fee, decimal seller, decimal buyer)
    {
        var value = PlatformFees.Pickup(gross, rate, PlatformFees.BuyerSeparate);
        Assert.Equal(fee, value.Fee);
        Assert.Equal(seller, value.SellerReceives);
        Assert.Equal(buyer, value.BuyerTotal);
    }

    [Fact]
    public void Legacy_pickup_preserves_fractional_fee_and_seller_deduction()
    {
        var value = PlatformFees.Pickup(1001, 5, PlatformFees.Legacy);
        Assert.Equal(50.05m, value.Fee);
        Assert.Equal(950.95m, value.SellerReceives);
        Assert.Equal(1001, value.BuyerTotal);
    }

    [Theory]
    [InlineData(PlatformFees.BuyerSeparate, 11000000, 11500000)]
    [InlineData(PlatformFees.Legacy, 10500000, 11000000)]
    public void Factory_fee_excludes_goods_vat(string policy, decimal seller, decimal buyer)
    {
        var value = PlatformFees.Settlement(10000000, 11000000, 5, policy);
        Assert.Equal(500000, value.Fee);
        Assert.Equal(seller, value.SellerReceives);
        Assert.Equal(buyer, value.BuyerTotal);
    }

    [Theory]
    [InlineData(-1, 5, PlatformFees.BuyerSeparate)]
    [InlineData(100, -1, PlatformFees.BuyerSeparate)]
    [InlineData(100, 101, PlatformFees.BuyerSeparate)]
    [InlineData(100, 5, "UNKNOWN")]
    public void Invalid_financial_snapshot_is_rejected(decimal gross, decimal rate, string policy) =>
        Assert.Throws<ArgumentException>(() => PlatformFees.Pickup(gross, rate, policy));

    [Theory]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("5.001")]
    [InlineData("invalid")]
    public void Rate_rejects_invalid_or_unpersistable_precision(string configured) =>
        Assert.Throws<InvalidOperationException>(() => PlatformFees.Rate(configured));
}
