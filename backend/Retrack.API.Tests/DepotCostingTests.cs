using Retrack.API.Services.Depot;
using Xunit;

namespace Retrack.API.Tests;

public class DepotCostingTests
{
    [Fact]
    public void Mixed_units_allocate_purchase_by_reference_value_and_keep_exact_total()
    {
        var result = DepotCosting.Allocate(20_000_000, 4_000_000,
            [new(1000, 15000, 0), new(100, 40000, 0), new(10, 600000, 0)]);
        Assert.Equal(new decimal[] { 14_400_000, 3_840_000, 5_760_000 }, result);
        Assert.Equal(24_000_000, result.Sum());
    }

    [Fact]
    public void Rounding_assigns_the_remainder_without_creating_money()
    {
        Assert.Equal(10, DepotCosting.Allocate(10, 0, [new(1, 1, 0), new(1, 1, 0), new(1, 1, 0)]).Sum());
    }

    [Fact]
    public void Direct_repair_cost_only_belongs_to_its_line()
    {
        Assert.Equal(new decimal[] { 500, 700 }, DepotCosting.Allocate(1000, 0, [new(1, 1, 0), new(1, 1, 200)]));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(-1, 10)]
    public void Invalid_quantities_or_reference_values_are_rejected(decimal quantity, decimal reference)
        => Assert.Throws<ArgumentException>(() => DepotCosting.Allocate(1000, 0, [new(quantity, reference, 0)]));

    [Fact]
    public void Fifo_preserves_costs_from_two_sources_and_does_not_hide_unknown_cost()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var result = DepotCosting.Take(1300, [new(a, 1000, 5000), new(b, 500, 6000)]);
        Assert.Equal(new decimal[] { 1000, 300 }, result.Select(x => x.Quantity));
        Assert.Equal(6_800_000, result.Sum(x => x.Quantity * x.UnitCost));
        Assert.Null(Assert.Single(DepotCosting.Take(1, [new(a, 1, null)])).UnitCost);
    }

    [Fact]
    public void Fifo_rejects_insufficient_stock()
        => Assert.Throws<DepotConflictException>(() => DepotCosting.Take(2, [new(Guid.NewGuid(), 1, 1)]));
    [Fact]
    public void Large_valid_values_do_not_overflow_when_allocating()
    {
        var result = DepotCosting.Allocate(1_000_000_000_000, 1_000_000_000_000,
            [new(1_000_000_000, 1_000_000_000_000, 0), new(1_000_000_000, 1_000_000_000_000, 0)]);
        Assert.Equal(2_000_000_000_000, result.Sum());
    }
}
