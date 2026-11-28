using Retrack.API.Services.Depot;
using Xunit;

namespace Retrack.API.Tests;

public sealed class AuctionItemCostingTests
{
    private readonly Guid iron=Guid.NewGuid(),tv=Guid.NewGuid();
    [Fact] public void Separate_items_keep_their_purchase_capital_without_reference_prices()
    {
        var costs=DepotCosting.AllocateItems(50000,[new(iron,1000000),new(tv,1000000)],
            [new(iron,new(90,0,0)),new(tv,new(5,0,0))]);
        Assert.Equal(new decimal[]{1025000,1025000},costs);
        Assert.Equal(205000,costs[1]/5);
        var moreLoss=DepotCosting.AllocateItems(50000,[new(iron,1000000),new(tv,1000000)],
            [new(iron,new(45,0,0)),new(tv,new(5,0,0))]);
        Assert.Equal(costs,moreLoss);
    }
    [Fact] public void Splitting_one_item_only_allocates_that_items_capital()
    {
        var costs=DepotCosting.AllocateItems(400,[new(iron,1000),new(tv,3000)],
            [new(iron,new(10,0,0)),new(tv,new(1,1,0)),new(tv,new(1,1,20))]);
        Assert.Equal(new decimal[]{1100,1650,1670},costs);
        Assert.Equal(4420,costs.Sum());
    }
    [Fact] public void Missing_or_unknown_source_is_rejected_instead_of_shifting_its_cost()
    {
        Assert.Throws<ArgumentException>(()=>DepotCosting.AllocateItems(0,[new(iron,1000),new(tv,3000)],[new(iron,new(10,0,0))]));
        Assert.Throws<ArgumentException>(()=>DepotCosting.AllocateItems(0,[new(iron,1000)],[new(tv,new(10,0,0))]));
    }
    [Fact] public void A_split_item_requires_values_but_a_single_output_does_not()
    {
        Assert.Throws<ArgumentException>(()=>DepotCosting.AllocateItems(0,[new(iron,1000)],
            [new(iron,new(10,0,0)),new(iron,new(1,0,0))]));
        Assert.Equal(1000,Assert.Single(DepotCosting.AllocateItems(0,[new(iron,1000)],[new(iron,new(10,0,0))])));
    }
    [Fact] public void Rounding_preserves_every_dong_and_output_order()
    {
        var costs=DepotCosting.AllocateItems(1,[new(iron,1),new(tv,2)],
            [new(tv,new(1,0,0)),new(iron,new(1,0,0))]);
        Assert.Equal(new decimal[]{3,1},costs);Assert.Equal(4,costs.Sum());
    }
}
