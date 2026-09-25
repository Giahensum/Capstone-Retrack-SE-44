using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Retrack.API.Controllers.Factory;
using Retrack.API.Controllers.Shared;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
using Retrack.API.Services.Factory;
using Retrack.API.Services.Shared;
using Xunit;

namespace Retrack.API.Tests;

public class FactoryServiceWorkflowTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["Factory:WeightDiscrepancyPercentage"] = "5"
        }).Build();

    private static async Task<(Factory Factory, Depot Depot, InventoryBatch Batch)> Seed(AppDbContext db,
        string status = "RECEIVED")
    {
        var factory = new Factory { Owner = new User { Role = "FACTORY" }, Name = "Factory", MinimumPurityPercent = 90 };
        var depot = new Depot { Owner = new User { Role = "DEPOT_OWNER" }, Name = "Depot" };
        var batch = new InventoryBatch
        {
            Depot = depot, TargetFactory = factory, DeclaredWeightKg = 100,
            MaterialType = "PET", Status = status
        };
        db.InventoryBatches.Add(batch);
        await db.SaveChangesAsync();
        return (factory, depot, batch);
    }

    [Fact]
    public async Task DemandCreationPreservesCreatedLocationAndResponse()
    {
        await using var db = NewDb();
        var (factory, _, _) = await Seed(db);
        var controller = new FactoryDemandController(new FactoryDemandService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, factory.OwnerId.ToString())], "Test"))
                }
            }
        };
        var result = Assert.IsType<CreatedResult>(await controller.Create(new DemandRequest
        {
            MaterialType = MaterialType.PET, QuantityKg = 50, Note = "  demand  "
        }, default));
        var demand = await db.FactoryDemands.SingleAsync();
        Assert.Equal($"/api/factory/demands/{demand.Id}", result.Location);
        var json = JsonSerializer.SerializeToElement(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal(demand.Id, json.GetProperty("data").GetProperty("id").GetGuid());
        Assert.Equal("demand", demand.Note);
    }

    [Fact]
    public async Task DemandCannotBeUpdatedByAnotherFactory()
    {
        await using var db = NewDb();
        var (factory, _, _) = await Seed(db);
        var other = new Factory { Owner = new User { Role = "FACTORY" } };
        var demand = new FactoryDemand { Factory = other, RequiredWeightKg = 10 };
        db.FactoryDemands.Add(demand);
        await db.SaveChangesAsync();
        var result = await new FactoryDemandService(db).UpdateAsync(factory.OwnerId, demand.Id,
            new DemandRequest { QuantityKg = 20 }, default);
        Assert.Equal(ServiceOutcome.NotFound, result.Outcome);
        Assert.Equal(10, demand.RequiredWeightKg);
    }

    [Fact]
    public async Task WeighingRequiresDiscrepancyNoteAndQualityRequiresMinimumPurity()
    {
        await using var db = NewDb();
        var (factory, _, batch) = await Seed(db);
        var service = new FactoryQCService(db, Configuration());
        var request = new WeighRequest { GrossWeightKg = 125, TareWeightKg = 5 };
        Assert.Equal(ServiceOutcome.Invalid, (await service.WeighAsync(factory.OwnerId, batch.Id, request, default)).Outcome);
        Assert.Empty(db.BatchQualityChecks);
        Assert.Equal("RECEIVED", batch.Status);
        request.Note = "  Confirmed discrepancy  ";
        Assert.Equal(ServiceOutcome.Success, (await service.WeighAsync(factory.OwnerId, batch.Id, request, default)).Outcome);
        var qc = await db.BatchQualityChecks.SingleAsync();
        Assert.Equal(120, qc.ActualWeightKg);
        Assert.Equal(20, qc.DifferencePercentage);
        Assert.Equal("WEIGHED", batch.Status);
        var quality = new QualityRequest { Accept = true, PurityPercent = 85, Grade = "A" };
        Assert.Equal(ServiceOutcome.Invalid, (await service.QualityAsync(factory.OwnerId, batch.Id, quality, default)).Outcome);
        quality.PurityPercent = 95;
        Assert.Equal(ServiceOutcome.Success, (await service.QualityAsync(factory.OwnerId, batch.Id, quality, default)).Outcome);
        Assert.Equal("VERIFIED", batch.Status);
        Assert.True(qc.IsAccepted);
    }

    [Fact]
    public async Task MarketplaceAcceptanceRequiresDepotApproval()
    {
        await using var db = NewDb();
        var (factory, depot, batch) = await Seed(db, "MARKETPLACE");
        batch.TargetFactory = null;
        batch.TargetFactoryId = null;
        await db.SaveChangesAsync();
        var service = new FactoryMarketService(db);
        Assert.Equal(ServiceOutcome.Conflict,
            (await service.AcceptAsync(factory.OwnerId, batch.Id, new BatchOfferRequest(), default)).Outcome);
        Assert.Equal("PENDING", (await db.FactoryDepotPartnerships.SingleAsync()).Status);
        Assert.Empty(db.TransportJobs);
        // Arrange an approved partnership as input to the Factory workflow.
        (await db.FactoryDepotPartnerships.SingleAsync()).Status = "APPROVED";
        await db.SaveChangesAsync();
        var accepted = await service.AcceptAsync(factory.OwnerId, batch.Id, new BatchOfferRequest(), default);
        Assert.Equal(ServiceOutcome.Success, accepted.Outcome);
        Assert.Equal(batch.Id, accepted.ResourceId);
        Assert.Equal(factory.Id, batch.TargetFactoryId);
        Assert.Equal("ACCEPTED", batch.Status);
        Assert.Single(db.TransportJobs);
    }

    [Fact]
    public async Task BlockingPartnerRejectsPendingDirectOffers()
    {
        await using var db = NewDb();
        var (factory, depot, batch) = await Seed(db, "PENDING_FACTORY");
        batch.DirectOfferFactoryId = factory.Id;
        db.FactoryDepotPartnerships.Add(new FactoryDepotPartnership { Factory = factory, Depot = depot, Status = "APPROVED" });
        await db.SaveChangesAsync();
        var result = await new FactoryPartnerService(db).UpdateStatusAsync(factory.OwnerId, depot.Id,
            new PartnerStatusRequest { Status = PartnershipStatus.BLOCKED }, default);
        Assert.Equal(ServiceOutcome.Success, result.Outcome);
        Assert.Equal("REJECTED", batch.Status);
        Assert.Null(batch.DirectOfferFactoryId);
    }

    [Fact]
    public async Task FactoryCanReceiveOnlyDeliveredOrdersOnce()
    {
        await using var db = NewDb();
        var (factory, _, batch) = await Seed(db, "ACCEPTED");
        var job = new TransportJob { Batch = batch, Status = "ACCEPTED" };
        db.TransportJobs.Add(job);
        await db.SaveChangesAsync();
        var orders = new FactoryOrderService(db);
        Assert.Equal(ServiceOutcome.Conflict, (await orders.ReceiveAsync(factory.OwnerId, batch.Id, default)).Outcome);
        Assert.Equal("ACCEPTED", batch.Status);
        // Arrange delivery as input; the Driver implementation is outside these tests.
        job.Status = "DELIVERED";
        batch.Status = "DELIVERED";
        await db.SaveChangesAsync();
        Assert.Equal(ServiceOutcome.Success, (await orders.ReceiveAsync(factory.OwnerId, batch.Id, default)).Outcome);
        Assert.Equal("RECEIVED", batch.Status);
        Assert.NotNull(batch.FactoryReceivedAt);
        Assert.Equal(ServiceOutcome.Conflict, (await orders.ReceiveAsync(factory.OwnerId, batch.Id, default)).Outcome);
    }

    [Theory]
    [InlineData(ServiceOutcome.Success, 200)]
    [InlineData(ServiceOutcome.Invalid, 400)]
    [InlineData(ServiceOutcome.NotFound, 404)]
    [InlineData(ServiceOutcome.Conflict, 409)]
    public void ResultMappingPreservesHttpStatusAndEnvelope(ServiceOutcome outcome, int status)
    {
        var controller = new TestController();
        var response = Assert.IsAssignableFrom<ObjectResult>(controller.ToActionResult(new ServiceResult(outcome, Message: "message")));
        Assert.Equal(status, response.StatusCode);
        var json = JsonSerializer.SerializeToElement(response.Value);
        Assert.Equal(outcome == ServiceOutcome.Success, json.GetProperty("success").GetBoolean());
        Assert.Equal("message", json.GetProperty("message").GetString());
        Assert.False(json.TryGetProperty("data", out _));
    }

    private sealed class TestController : ControllerBase;
}
