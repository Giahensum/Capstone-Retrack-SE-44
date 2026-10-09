using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services;
using Xunit;

namespace Retrack.Tests;

public sealed class PaymentTests : IAsyncLifetime
{
    private const string Proof = "https://example.com/payment-proof.png";
    private readonly List<Guid> requestIds = [];
    private readonly List<Guid> userIds = [];
    private readonly List<Guid> depotIds = [];

    public async Task InitializeAsync()
    {
        await using var db = Open();
        await TestDatabase.MigrateAsync(db);
    }

    public async Task DisposeAsync()
    {
        await using var db = Open();
        await db.PlatformTransactions.Where(t => requestIds.Contains(t.SourceId)).ExecuteDeleteAsync();
        await db.InventoryBatches.Where(b => depotIds.Contains(b.DepotId)).ExecuteDeleteAsync();
        await db.PickupRequests.Where(r => requestIds.Contains(r.Id)).ExecuteDeleteAsync();
        await db.Depots.Where(d => depotIds.Contains(d.Id)).ExecuteDeleteAsync();
        await db.Users.Where(u => userIds.Contains(u.Id)).ExecuteDeleteAsync();
    }

    private static AppDbContext Open(params IInterceptor[] interceptors)
    {
        var connection = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set RETRACK_TEST_CONNECTION to the dedicated PostgreSQL test database.");
        if (new NpgsqlConnectionStringBuilder(connection).Database != "Retrack_TV2_test")
            throw new InvalidOperationException("Tests require database Retrack_TV2_test; application databases are forbidden.");
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection).AddInterceptors(interceptors).Options);
    }

    private async Task<(Guid Request, Guid Owner)> Seed(string status = "AWAITING_PAYMENT", string role = "DEPOT_OWNER", bool active = true, bool targeted = true)
    {
        await using var db = Open();
        var owner = new User { Email = $"owner-{Guid.NewGuid()}@test.invalid", Role = role, IsActive = active, FullName = "Test owner" };
        var seller = new User { Email = $"seller-{Guid.NewGuid()}@test.invalid", Role = "SELLER", FullName = "Test seller" };
        var depot = new Depot { Owner = owner, Name = "Test depot", Address = "Test address" };
        var request = new PickupRequest { Seller = seller, TargetDepot = targeted ? depot : null, Address = "Test address", Status = status, GrossAmount = 1000m, PlatformFeePercentage = 1m, PlatformFeeAmount = 10m, NetAmount = 990m };
        db.AddRange(owner, seller, depot, request);
        await db.SaveChangesAsync();
        requestIds.Add(request.Id);
        depotIds.Add(depot.Id);
        userIds.AddRange([owner.Id, seller.Id]);
        return (request.Id, owner.Id);
    }

    private static async Task<PickupRequestDto> Pay(AppDbContext db, Guid request, Guid owner, string proof = Proof)
    {
        var service = new PickupService(new PickupRequestRepository(db), db, new DepotPaymentService(new DepotOwnerRepository(db), new DepotPaymentRepository(db), new DepotUnitOfWork(db)));
        return await service.MarkPaymentSentAsync(request, owner, proof);
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("DRIVER")]
    [InlineData("DEPOT_EMPLOYEE")]
    public async Task Public_registration_cannot_create_privileged_or_staff_accounts(string role)
    {
        await using var db = Open();
        var service = new AuthService(new UserRepository(db), new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterAsync(new RegisterDto
        {
            Email = "forbidden@test.invalid", FullName = "Forbidden", Password = "TestOnly123!", Role = role
        }));
        Assert.False(await db.Users.AnyAsync(u => u.Email == "forbidden@test.invalid"));
    }

    [Fact]
    public async Task Owner_payment_persists_proof_status_and_exactly_one_fee()
    {
        var seed = await Seed();
        await using (var db = Open())
            Assert.Equal(Proof, (await Pay(db, seed.Request, seed.Owner)).PaymentProofUrl);
        await using var verify = Open();
        var request = await verify.PickupRequests.FindAsync(seed.Request);
        Assert.Equal("PAYMENT_SENT", request!.Status);
        Assert.Equal(Proof, request.PaymentProofUrl);
        var fee = Assert.Single(await verify.PlatformTransactions.Where(t => t.SourceId == seed.Request).ToListAsync());
        Assert.Equal("PICKUP_REQUEST", fee.SourceType);
        Assert.Equal(10m, fee.FeeAmount);
    }

    [Fact]
    public async Task Seller_confirms_received_payment_before_inventory_increases()
    {
        var seed = await Seed();
        Guid sellerId;
        Guid depotId;
        await using (var db = Open())
        {
            var request = (await db.PickupRequests.FindAsync(seed.Request))!;
            sellerId = request.SellerId;
            depotId = request.TargetDepotId!.Value;
            db.PickupRequestItems.Add(new PickupRequestItem
            {
                PickupRequestId = seed.Request, MaterialType = "PAPER", WeightKg = 10,
                PricePerKg = 100, SubTotal = 1000
            });
            await db.SaveChangesAsync();
            await Pay(db, seed.Request, seed.Owner);
        }

        await using (var db = Open())
        {
            var service = new PickupService(new PickupRequestRepository(db), db,
                new DepotPaymentService(new DepotOwnerRepository(db), new DepotPaymentRepository(db), new DepotUnitOfWork(db)));
            Assert.Equal(Proof, (await service.GetByIdAsync(seed.Request, sellerId))!.PaymentProofUrl);
            await Assert.ThrowsAsync<DepotForbiddenException>(() => service.MarkDoneAsync(seed.Request, Guid.NewGuid()));
            Assert.Equal("DONE", (await service.MarkDoneAsync(seed.Request, sellerId)).Status);
            Assert.Equal("DONE", (await service.MarkDoneAsync(seed.Request, sellerId)).Status);
        }

        await using var verify = Open();
        Assert.Equal(1, await verify.PlatformTransactions.CountAsync(t => t.SourceId == seed.Request));
        var inventory = new InventoryService(new DepotInventoryRepository(verify),
            new DepotService(new DepotOwnerRepository(verify), new DepotPaymentReadRepository(verify)));
        Assert.Equal(10m, Assert.Single(await inventory.GetAsync(seed.Owner, depotId)).ReceivedKg);
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("SCHEDULED")]
    [InlineData("WEIGHED")]
    [InlineData("SELLER_CONFIRMED")]
    [InlineData("CANCELLED")]
    public async Task Other_states_are_rejected_without_writes(string state)
    {
        var seed = await Seed(state);
        await using var db = Open();
        var error = await Record.ExceptionAsync(() => Pay(db, seed.Request, seed.Owner));
        Assert.Equal("DepotConflictException", error?.GetType().Name);
        await AssertUnchanged(seed.Request, state);
    }

    [Theory]
    [InlineData("DEPOT_OWNER", true, true, true)]
    [InlineData("DEPOT_OWNER", false, true, false)]
    [InlineData("SELLER", true, true, false)]
    [InlineData("DEPOT_OWNER", true, false, false)]
    public async Task Unauthorized_actors_are_rejected_without_writes(string role, bool active, bool targeted, bool foreignOwner)
    {
        var seed = await Seed(role: role, active: active, targeted: targeted);
        await using var db = Open();
        var error = await Record.ExceptionAsync(() => Pay(db, seed.Request, foreignOwner ? Guid.NewGuid() : seed.Owner));
        Assert.Equal("DepotForbiddenException", error?.GetType().Name);
        await AssertUnchanged(seed.Request, "AWAITING_PAYMENT");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/relative/proof.png")]
    [InlineData("ftp://example.com/proof.png")]
    public async Task Invalid_proofs_are_rejected_without_writes(string proof)
    {
        var seed = await Seed();
        await using var db = Open();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Pay(db, seed.Request, seed.Owner, proof));
        await AssertUnchanged(seed.Request, "AWAITING_PAYMENT");
    }

    [Theory]
    [InlineData("PAYMENT_SENT")]
    [InlineData("DONE")]
    public async Task Same_proof_retry_preserves_state_timestamp_and_single_fee(string state)
    {
        var seed = await Seed();
        await using (var db = Open()) await Pay(db, seed.Request, seed.Owner);
        DateTime updated;
        await using (var db = Open())
        {
            var request = (await db.PickupRequests.FindAsync(seed.Request))!;
            request.Status = state;
            await db.SaveChangesAsync();
            updated = request.UpdatedAt;
        }
        await using (var db = Open()) Assert.Equal(state, (await Pay(db, seed.Request, seed.Owner)).Status);
        await using var verify = Open();
        Assert.Equal(updated, (await verify.PickupRequests.FindAsync(seed.Request))!.UpdatedAt);
        Assert.Equal(1, await verify.PlatformTransactions.CountAsync(t => t.SourceId == seed.Request));
    }

    [Fact]
    public async Task Changed_proof_retry_conflicts_and_preserves_original()
    {
        var seed = await Seed();
        await using (var db = Open()) await Pay(db, seed.Request, seed.Owner);
        await using (var db = Open())
        {
            var error = await Record.ExceptionAsync(() => Pay(db, seed.Request, seed.Owner, "https://example.com/other.png"));
            Assert.Equal("DepotConflictException", error?.GetType().Name);
        }
        await using var verify = Open();
        Assert.Equal(Proof, (await verify.PickupRequests.FindAsync(seed.Request))!.PaymentProofUrl);
        Assert.Equal(1, await verify.PlatformTransactions.CountAsync(t => t.SourceId == seed.Request));
    }

    [Fact]
    public async Task Concurrent_same_proof_requests_create_only_one_fee()
    {
        var seed = await Seed();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Attempt()
        {
            await using var db = Open();
            await start.Task;
            Assert.Equal("PAYMENT_SENT", (await Pay(db, seed.Request, seed.Owner)).Status);
        }
        var attempts = Enumerable.Range(0, 8).Select(_ => Attempt()).ToArray();
        start.SetResult();
        await Task.WhenAll(attempts);
        await using var verify = Open();
        Assert.Equal(1, await verify.PlatformTransactions.CountAsync(t => t.SourceId == seed.Request));
    }

    [Fact]
    public async Task Concurrent_different_proofs_have_one_winner_and_one_conflict()
    {
        var seed = await Seed();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<(string Proof, Exception? Error)> Attempt(string proof)
        {
            await using var db = Open();
            await start.Task;
            return (proof, await Record.ExceptionAsync(() => Pay(db, seed.Request, seed.Owner, proof)));
        }
        var attempts = new[] { Attempt(Proof), Attempt("https://example.com/other.png") };
        start.SetResult();
        var results = await Task.WhenAll(attempts);
        var winner = Assert.Single(results, r => r.Error == null);
        Assert.Equal("DepotConflictException", Assert.Single(results, r => r.Error != null).Error!.GetType().Name);
        await using var verify = Open();
        Assert.Equal(winner.Proof, (await verify.PickupRequests.FindAsync(seed.Request))!.PaymentProofUrl);
        Assert.Equal(1, await verify.PlatformTransactions.CountAsync(t => t.SourceId == seed.Request));
    }

    [Fact]
    public async Task Missing_request_returns_not_found()
    {
        var seed = await Seed();
        await using var db = Open();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Pay(db, Guid.NewGuid(), seed.Owner));
    }

    [Fact]
    public async Task Fee_write_failure_rolls_back_request_status_and_proof()
    {
        var seed = await Seed();
        await using (var db = Open(new FailFeeSave()))
            await Assert.ThrowsAsync<InjectedFeeFailure>(() => Pay(db, seed.Request, seed.Owner));
        await AssertUnchanged(seed.Request, "AWAITING_PAYMENT");
    }

    [Fact]
    public async Task Database_unique_constraint_rejects_a_second_fee_for_the_same_source()
    {
        var seed = await Seed();
        await using (var db = Open()) await Pay(db, seed.Request, seed.Owner);
        await using var duplicate = Open();
        duplicate.PlatformTransactions.Add(new PlatformTransaction { SourceType = "PICKUP_REQUEST", SourceId = seed.Request, FeeAmount = 10 });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Lists_and_details_are_scoped_to_selected_depot_even_for_same_owner()
    {
        var first = await Seed();
        var second = await Seed();
        var foreign = await Seed();
        await using var db = Open();
        var firstRequest = (await db.PickupRequests.FindAsync(first.Request))!;
        var secondRequest = (await db.PickupRequests.FindAsync(second.Request))!;
        var foreignRequest = (await db.PickupRequests.FindAsync(foreign.Request))!;
        var secondDepot = (await db.Depots.FindAsync(secondRequest.TargetDepotId))!;
        secondDepot.OwnerId = first.Owner;
        await db.SaveChangesAsync();
        var service = new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db));
        var depots = await service.GetDepotsAsync(first.Owner);
        Assert.Equal(2, depots.Count);
        Assert.DoesNotContain(depots, d => d.Id == foreignRequest.TargetDepotId);
        var list = await service.GetPaymentsAsync(first.Owner, firstRequest.TargetDepotId!.Value, new DepotQuery());
        Assert.Equal(first.Request, Assert.Single(list.Items).Id);
        Assert.Equal(1, list.TotalCount);
        Assert.Equal(first.Request, (await service.GetPaymentAsync(first.Owner, firstRequest.TargetDepotId.Value, first.Request)).Id);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetPaymentAsync(first.Owner, firstRequest.TargetDepotId.Value, second.Request));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.GetPaymentsAsync(first.Owner, foreignRequest.TargetDepotId!.Value, new DepotQuery()));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.GetPaymentAsync(first.Owner, foreignRequest.TargetDepotId!.Value, foreign.Request));
    }

    [Theory]
    [InlineData("AWAITING_PAYMENT", 0)]
    [InlineData("PAYMENT_SENT", 0)]
    [InlineData("DONE", 100)]
    public async Task Inventory_receives_only_completed_pickups(string status, int expected)
    {
        var seed = await Seed(status);
        await using var db = Open();
        var depotId = (await db.PickupRequests.FindAsync(seed.Request))!.TargetDepotId!.Value;
        db.PickupRequestItems.Add(new PickupRequestItem { PickupRequestId = seed.Request, MaterialType = "PAPER", WeightKg = 100 });
        await db.SaveChangesAsync();
        var rows = await new InventoryService(new Retrack.API.Repositories.DepotInventoryRepository(db), new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db))).GetAsync(seed.Owner, depotId);
        Assert.Equal(expected, rows.Sum(r => r.ReceivedKg));
    }

    [Fact]
    public async Task Inventory_separates_reservations_exports_and_restores_cancelled_weight()
    {
        var seed = await Seed("DONE");
        var foreign = await Seed("DONE");
        await using var db = Open();
        var depotId = (await db.PickupRequests.FindAsync(seed.Request))!.TargetDepotId!.Value;
        var foreignDepotId = (await db.PickupRequests.FindAsync(foreign.Request))!.TargetDepotId!.Value;
        db.PickupRequestItems.AddRange(
            new PickupRequestItem { PickupRequestId = seed.Request, MaterialType = "PAPER", WeightKg = 100 },
            new PickupRequestItem { PickupRequestId = foreign.Request, MaterialType = "PAPER", WeightKg = 900 });
        var reserved = new InventoryBatch { DepotId = depotId, MaterialType = "PAPER", DeclaredWeightKg = 30, Status = "MARKETPLACE" };
        db.InventoryBatches.AddRange(reserved,
            new InventoryBatch { DepotId = depotId, MaterialType = "PAPER", DeclaredWeightKg = 20, Status = "IN_TRANSIT" },
            new InventoryBatch { DepotId = depotId, MaterialType = "PAPER", DeclaredWeightKg = 15, Status = "CANCELLED" });
        await db.SaveChangesAsync();
        var service = new InventoryService(new Retrack.API.Repositories.DepotInventoryRepository(db), new DepotService(new Retrack.API.Repositories.DepotOwnerRepository(db), new Retrack.API.Repositories.DepotPaymentReadRepository(db)));
        var row = Assert.Single(await service.GetAsync(seed.Owner, depotId));
        Assert.Equal(100m, row.ReceivedKg);
        Assert.Equal(30m, row.ReservedKg);
        Assert.Equal(20m, row.ExportedKg);
        Assert.Equal(80m, row.OnHandKg);
        Assert.Equal(50m, row.AvailableKg);
        reserved.Status = "CANCELLED";
        await db.SaveChangesAsync();
        Assert.Equal(80m, Assert.Single(await service.GetAsync(seed.Owner, depotId)).AvailableKg);
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.GetAsync(seed.Owner, foreignDepotId));
    }

    private static async Task AssertUnchanged(Guid id, string status)
    {
        await using var db = Open();
        var request = (await db.PickupRequests.FindAsync(id))!;
        Assert.Equal(status, request.Status);
        Assert.Null(request.PaymentProofUrl);
        Assert.Equal(0, await db.PlatformTransactions.CountAsync(t => t.SourceId == id));
    }

    private sealed class InjectedFeeFailure : Exception { }
    private sealed class FailFeeSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<PlatformTransaction>().Any(e => e.State == EntityState.Added))
                throw new InjectedFeeFailure();
            return ValueTask.FromResult(result);
        }
    }
}
