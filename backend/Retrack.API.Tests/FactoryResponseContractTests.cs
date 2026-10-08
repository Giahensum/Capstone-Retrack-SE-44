using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Controllers.Shared;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Services.Factory;
using Retrack.API.Services.Shared;
using Xunit;

namespace Retrack.API.Tests;

public class FactoryResponseContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("READY_FOR_PICKUP", "IN_PROGRESS", "IN_TRANSIT")]
    [InlineData("TRANSPORT_READY", "DELIVERED", "DELIVERED")]
    [InlineData("PENDING_FACTORY", null, "ACCEPTED")]
    [InlineData("COMPLETED", null, "PAID")]
    [InlineData("CANCELLED", null, "REJECTED")]
    [InlineData("WEIGHED", null, "WEIGHED")]
    public void OrderMappingPreservesPublicStatuses(string batchStatus, string? transportStatus, string expected)
    {
        var batch = new InventoryBatch
        {
            Status = batchStatus,
            Depot = new Depot(),
            TransportJob = transportStatus is null ? null : new TransportJob { Status = transportStatus }
        };

        Assert.Equal(expected, FactoryOrderMapper.Map(batch).Status);
    }

    [Fact]
    public void OrderResponsePreservesJsonFieldsAndOptionalSections()
    {
        var batch = new InventoryBatch
        {
            Status = "WEIGHED", Code = "LO-2610-007", MaterialType = "PET", DeclaredWeightKg = 100,
            Depot = new Depot { Name = "Depot", Address = "Address", Owner = new User { Phone = "123" } },
            QualityCheck = new BatchQualityCheck
            {
                Grade = "PENDING", ActualWeightKg = 95, GrossWeightKg = 105, TareWeightKg = 10,
                DifferencePercentage = -5, QualityNote = "Checked", TicketNumber = "T-001"
            }
        };
        var response = FactoryOrderMapper.Map(batch);
        Assert.Equal("LO-2610-007", response.BatchCode);
        var json = JsonSerializer.SerializeToElement(response, JsonOptions);
        AssertFields(json, "id", "batchId", "batchCode", "materialType", "estimatedWeightKg", "actualWeightKg",
            "depotId", "depotName", "depotAddress", "depotPhone", "agreedPrice", "totalAmount", "status",
            "receivedAt", "decidedAt", "rejectionReason", "settledAt", "feeAmount", "netPayableAmount",
            "paymentReference", "platformFeePercentage", "createdAt", "transport", "weightVerification", "weightTicket", "invoice");
        Assert.Equal(JsonValueKind.Null, json.GetProperty("transport").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("invoice").ValueKind);
        var verification = json.GetProperty("weightVerification");
        AssertFields(verification, "depotWeightKg", "factoryWeightKg", "differencePercentage", "isVerified",
            "purityPercent", "moisturePercent", "contaminationPercent", "grade", "qualityNote", "note");
        Assert.Equal(JsonValueKind.Null, verification.GetProperty("grade").ValueKind);
        Assert.Equal("Checked", verification.GetProperty("note").GetString());
        Assert.Equal(95m, verification.GetProperty("factoryWeightKg").GetDecimal());
        AssertFields(json.GetProperty("weightTicket"), "ticketNumber", "grossWeightKg", "tareWeightKg", "netWeightKg", "ticketImageUrl");

        batch.QualityCheck.InvoiceNumber = "INV-001";
        batch.QualityCheck.InvoiceFileUrl = "invoice.pdf";
        batch.QualityCheck.InvoiceStatus = "UPLOADED";
        json = JsonSerializer.SerializeToElement(FactoryOrderMapper.Map(batch), JsonOptions);
        AssertFields(json.GetProperty("invoice"), "invoiceNumber", "invoiceFileUrl", "status");
        Assert.Equal("invoice.pdf", json.GetProperty("invoice").GetProperty("invoiceFileUrl").GetString());

        batch.QualityCheck = null;
        json = JsonSerializer.SerializeToElement(FactoryOrderMapper.Map(batch), JsonOptions);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("weightVerification").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("weightTicket").ValueKind);
    }

    [Fact]
    public async Task SharedOrderQueryScopesFactoryAndLoadsDetailsForListAndDashboard()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var factory = new Factory { Owner = new User { Role = "FACTORY" } };
        var otherFactory = new Factory { Owner = new User { Role = "FACTORY" } };
        var depot = new Depot { Name = "Depot", Owner = new User { Phone = "123" } };
        var included = new InventoryBatch { TargetFactory = factory, Depot = depot, Status = "ACCEPTED" };
        included.TransportJob = new TransportJob { Batch = included, Status = "IN_TRANSIT" };
        db.InventoryBatches.AddRange(included,
            new InventoryBatch { TargetFactory = factory, Depot = depot, Status = "DRAFT" },
            new InventoryBatch { TargetFactory = factory, Depot = depot, Status = "MARKETPLACE" },
            new InventoryBatch { TargetFactory = otherFactory, Depot = depot, Status = "ACCEPTED" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var orders = await new FactoryOrderService(db).ListAsync(factory.OwnerId, new OrderQuery(), default);
        Assert.NotNull(orders.Data);
        var order = Assert.Single(orders.Data.Items);
        Assert.Equal(included.Id, order.Id);
        Assert.Equal("IN_TRANSIT", order.Status);
        Assert.Equal("123", order.DepotPhone);
        Assert.NotNull(order.Transport);
        Assert.Equal(1, orders.Data.TotalCount);
        var page = JsonSerializer.SerializeToElement(orders.Data, JsonOptions);
        AssertFields(page, "items", "totalCount", "page", "pageSize", "totalPages");

        var dashboard = await new FactoryDashboardService(db).GetAsync(factory.OwnerId, default);
        Assert.NotNull(dashboard.Data);
        Assert.Equal(1, dashboard.Data.OrderCount);
        Assert.Equal(order, Assert.Single(dashboard.Data.RecentOrders));
    }

    [Fact]
    public void TypedFailurePreservesErrorEnvelopeWithoutDataOrCreatedLocation()
    {
        var controller = new TestController();
        var result = controller.ToActionResult(ServiceResult<OrderResponse>.NotFound("Missing"), id => $"/orders/{id}");
        var response = Assert.IsType<NotFoundObjectResult>(result);
        var json = JsonSerializer.SerializeToElement(response.Value, JsonOptions);
        AssertFields(json, "success", "message");
        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal("Missing", json.GetProperty("message").GetString());
    }

    [Fact]
    public async Task AttachmentUploadChecksFileSignatureAndKeepsOnlyAUrlInTheContract()
    {
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"retrack-factory-attachment-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var service = new FactoryAttachmentService(db, new TestEnvironment(temporaryRoot));
            var ownerId = Guid.NewGuid();
            var pngBytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3 };
            var upload = new FormFile(new MemoryStream(pngBytes), 0, pngBytes.Length, "file", "evidence.png");

            var url = await service.UploadAsync(ownerId, upload, default);

            Assert.StartsWith($"/api/factory/attachments/{ownerId:N}/", url);
            Assert.True(service.IsOwnedUrl(url, ownerId));
            var id = Guid.ParseExact(url.Split('/').Last(), "N");
            var opened = await service.OpenAsync(ownerId, "FACTORY", ownerId, id, default);
            Assert.Equal("image/png", opened.ContentType);
            Assert.True(File.Exists(opened.Path));

            var fake = new FormFile(new MemoryStream("not an image"u8.ToArray()), 0, 12, "file", "fake.png");
            await Assert.ThrowsAsync<ArgumentException>(() => service.UploadAsync(ownerId, fake, default));
        }
        finally
        {
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
        }
    }

    private static void AssertFields(JsonElement json, params string[] names)
        => Assert.Equal(names.OrderBy(x => x), json.EnumerateObject().Select(x => x.Name).OrderBy(x => x));

    private sealed class TestController : ControllerBase;

    private sealed class TestEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Retrack.API.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
