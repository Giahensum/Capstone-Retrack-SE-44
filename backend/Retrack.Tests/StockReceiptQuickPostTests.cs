using Microsoft.EntityFrameworkCore;
using Retrack.API.Repositories;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Depot;
using Xunit;

namespace Retrack.Tests;

public sealed partial class StockReceiptTests
{
    [Fact]
    public async Task Owner_creates_posted_receipt_atomically_and_retry_does_not_duplicate_inventory()
    {
        var input = Input(250); input.PostImmediately = true;
        async Task<Guid> Create()
        {
            await using var db = Open(); var result = await Stock(db).CreateAsync(owner, depot, input);
            Assert.Equal("POSTED", result.Status); return result.Id;
        }
        var ids = await Task.WhenAll(Create(), Create()); Assert.Equal(ids[0], ids[1]);
        await using var check = Open(); Assert.Equal(250, Assert.Single(await Inventory(check).GetAsync(owner, depot)).AvailableKg);
        var saved = await check.DepotStockReceipts.SingleAsync(x => x.Id == ids[0]);
        Assert.Equal(owner, saved.PostedBy); Assert.Equal(owner, saved.ReviewedBy); Assert.NotNull(saved.PostedAt);
        Assert.Equal(ids[0], await Create());
        Assert.Equal(1, await check.DepotStockReceipts.CountAsync(x => x.DepotId == depot));
    }

    [Fact]
    public async Task Employee_cannot_bypass_owner_review_using_immediate_post_flag()
    {
        await using var db = Open(); var input = Input(); input.PurchaseCost = 0;
        input.CostsConfirmed = false; input.Lines[0].ReferencePrice = 0; input.PostImmediately = true;
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Stock(db).CreateAsync(employee, depot, input));
        await using var check = Open(); Assert.False(await check.DepotStockReceipts.AnyAsync(x => x.DepotId == depot));
    }

    [Fact]
    public async Task Failure_after_saving_immediate_receipt_rolls_back_header_lines_and_stock()
    {
        await using var db = Open(); var input = Input(250); input.PostImmediately = true;
        var service = new DepotStockService(new DepotStockRepository(db), new DepotBatchRepository(db),
            new FailAfterSave(new DepotUnitOfWork(db)), Scope(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(owner, depot, input));
        await using var check = Open(); Assert.False(await check.DepotStockReceipts.AnyAsync(x => x.Id == input.OperationId));
        Assert.Empty(await Inventory(check).GetAsync(owner, depot));
    }

    private sealed class FailAfterSave(IDepotUnitOfWork inner) : IDepotUnitOfWork
    {
        public Task<IDepotTransaction> BeginAsync() => inner.BeginAsync();
        public async Task SaveAsync() { await inner.SaveAsync(); throw new InvalidOperationException("Giả lập lỗi trước commit."); }
    }
}
