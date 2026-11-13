using Microsoft.EntityFrameworkCore;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Depot;
using Xunit;

namespace Retrack.Tests;

public sealed partial class StockReceiptTests
{
    private static UpdateStockReceiptDto Edit(StockReceiptDto receipt, decimal quantity = 200) => new()
    {
        ExpectedRevision = receipt.Revision, Reference = receipt.Reference, PurchaseCost = receipt.PurchaseCost,
        CostsConfirmed = true, Expenses = [new("FUEL", "Xăng xe thực nhận", 100000)],
        Lines = [new() { Kind = "MATERIAL", MaterialType = "PET", Name = "Nhựa PET", Unit = "kg", Quantity = quantity, ReferencePrice = 1000 },
            new() { Kind = "REUSABLE", Name = "Ghế", Unit = "cái", Quantity = 10, ReferencePrice = 20000 }]
    };

    [Fact]
    public async Task Draft_edit_replaces_mixed_lines_reallocates_costs_and_only_posts_corrected_stock()
    {
        await using var db = Open(); var receipt = await Stock(db).CreateAsync(owner, depot, Input());
        var edit = Edit(receipt); var updated = await Stock(db).UpdateDraftAsync(owner, depot, receipt.Id, edit);
        Assert.Equal("DRAFT", updated.Status); Assert.NotEqual(receipt.Revision, updated.Revision);
        Assert.Equal(2, updated.Lines.Count); Assert.Equal(5100000, updated.Lines.Sum(x => x.AllocatedCost));
        Assert.Empty(await Inventory(db).GetAsync(owner, depot));
        await using var check = Open(); var persisted = await Stock(check).GetAsync(owner, depot, receipt.Id);
        Assert.Equal("PET", persisted.Lines[0].MaterialType); Assert.Equal(200, persisted.Lines[0].Quantity);
        Assert.DoesNotContain(persisted.Lines, x => x.Id == receipt.Lines[0].Id);
        await Stock(check).ReviewAsync(owner, depot, receipt.Id, "POSTED");
        Assert.Equal(200, Assert.Single(await Inventory(check).GetAsync(owner, depot)).AvailableKg);
        Assert.Equal(10, Assert.Single((await Stock(check).ReusableAsync(owner, depot, new())).Items).AvailableQuantity);
    }

    [Theory]
    [InlineData("SUBMITTED")][InlineData("POSTED")][InlineData("REJECTED")][InlineData("VOIDED")]
    public async Task Only_draft_quantity_can_be_edited(string status)
    {
        await using var db = Open(); var receipt = await Stock(db).CreateAsync(owner, depot, Input());
        if (status == "VOIDED") await Stock(db).ReviewAsync(owner, depot, receipt.Id, "POSTED");
        await Stock(db).ReviewAsync(owner, depot, receipt.Id, status, "Lý do kiểm thử");
        await Assert.ThrowsAsync<DepotConflictException>(() => Stock(db).UpdateDraftAsync(owner, depot, receipt.Id, Edit(receipt)));
        await using var check = Open(); Assert.Equal(1000, (await Stock(check).GetAsync(owner, depot, receipt.Id)).Lines[0].Quantity);
    }

    [Fact]
    public async Task Draft_edit_is_owner_scoped_and_rejects_stale_revision_without_overwriting()
    {
        await using var db = Open(); var receipt = await Stock(db).CreateAsync(owner, depot, Input());
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Stock(db).UpdateDraftAsync(employee, depot, receipt.Id, Edit(receipt)));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Stock(db).UpdateDraftAsync(Guid.NewGuid(), depot, receipt.Id, Edit(receipt)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Stock(db).UpdateDraftAsync(owner, depot, Guid.NewGuid(), Edit(receipt)));
        var input = Edit(receipt); await Stock(db).UpdateDraftAsync(owner, depot, receipt.Id, input);
        await using var second = Open();
        await Assert.ThrowsAsync<DepotConflictException>(() => Stock(second).UpdateDraftAsync(owner, depot, receipt.Id, Edit(receipt, 900)));
        Assert.Equal(200, (await Stock(second).GetAsync(owner, depot, receipt.Id)).Lines[0].Quantity);
    }

    [Theory]
    [InlineData(0)][InlineData(-1)][InlineData(0.0001)]
    public async Task Invalid_draft_edit_does_not_change_original_lines(decimal quantity)
    {
        await using var db = Open(); var receipt = await Stock(db).CreateAsync(owner, depot, Input());
        await Assert.ThrowsAsync<ArgumentException>(() => Stock(db).UpdateDraftAsync(owner, depot, receipt.Id, Edit(receipt, quantity)));
        await using var check = Open(); Assert.Equal(1000, (await Stock(check).GetAsync(owner, depot, receipt.Id)).Lines.Single().Quantity);
    }

    [Fact]
    public async Task Concurrent_draft_edit_allows_one_revision_and_post_reloads_current_lines()
    {
        await using var stale = Open(); var receipt = await Stock(stale).CreateAsync(owner, depot, Input());
        async Task<bool> Attempt(decimal quantity)
        {
            await using var db = Open();
            try { await Stock(db).UpdateDraftAsync(owner, depot, receipt.Id, Edit(receipt, quantity)); return true; }
            catch (DepotConflictException) { return false; }
        }
        Assert.Single(await Task.WhenAll(Attempt(200), Attempt(300)), x => x);
        var posted = await Stock(stale).ReviewAsync(owner, depot, receipt.Id, "POSTED");
        Assert.Equal("PET", posted.Lines[0].MaterialType);
        await using var check = Open(); var available = Assert.Single(await Inventory(check).GetAsync(owner, depot));
        Assert.Equal(posted.Lines[0].Quantity, available.AvailableKg);
        Assert.True(available.AvailableKg is 200 or 300);
    }
}
