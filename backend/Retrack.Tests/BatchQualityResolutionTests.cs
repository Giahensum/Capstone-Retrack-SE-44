using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Factory;
using Xunit;

namespace Retrack.Tests;

public sealed class BatchQualityResolutionTests : IAsyncLifetime
{
    private readonly string schema = "quality_" + Guid.NewGuid().ToString("N");
    private string connection = null!;
    private NpgsqlConnection admin = null!;
    private readonly string uploadRoot = Path.Combine(Path.GetTempPath(), "retrack-quality-" + Guid.NewGuid().ToString("N"));
    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION");
        var other = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES");
        if (source is null || other is null || new NpgsqlConnectionStringBuilder(source).Database != "Retrack_TV2_test"
            || new NpgsqlConnectionStringBuilder(other).Database != "Retrack_TV2_test") throw new InvalidOperationException("Chỉ chạy trên DB test.");
        admin = new NpgsqlConnection(source); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin)) await create.ExecuteNonQueryAsync();
        connection = new NpgsqlConnectionStringBuilder(source) { SearchPath = schema + ",public", Pooling = false }.ConnectionString;
        var sql = await File.ReadAllTextAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../db/bootstrap/retrack-system.sql")));
        await using var conn = new NpgsqlConnection(connection); await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql[..sql.IndexOf("-- RETRACK_SCHEMA_END", StringComparison.Ordinal)] + "\nCOMMIT;", conn);
        await cmd.ExecuteNonQueryAsync();
    }
    public async Task DisposeAsync()
    {
        await using var cmd = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin); await cmd.ExecuteNonQueryAsync(); await admin.DisposeAsync();
        if (Directory.Exists(uploadRoot)) { var full = Path.GetFullPath(uploadRoot); if (!full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("retrack-quality-", StringComparison.Ordinal)) throw new InvalidOperationException("Sai thư mục fixture."); Directory.Delete(full, true); }
    }
    private AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    private static User U(string role) => new() { Role = role, Email = Guid.NewGuid() + "@test.invalid", FullName = "Fixture quyết toán" };
    private static BatchService Batches(AppDbContext db)
    {
        var scope = new DepotService(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db));
        var repo = new DepotBatchRepository(db); var work = new DepotUnitOfWork(db);
        return new(repo, work, scope, new InventoryService(new DepotInventoryRepository(db), scope), null!, new DepotStockService(new DepotStockRepository(db), repo, work, scope), new global::Retrack.API.Services.Depot.BatchSettlementService(repo, work, scope), new global::Retrack.API.Repositories.DriverDeliveryRepository(db));
    }
    private static BatchSettlementService Decisions(AppDbContext db) => new(new DepotBatchRepository(db), new DepotUnitOfWork(db),
        new DepotService(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db)));
    private static SettlementRequest Price(decimal value = 1000) => new() { AgreedPricePerKg = value, VatLegalBasis = "Dữ liệu kiểm thử không áp dụng VAT" };
    private static BatchPaymentProofRequest Proof(Guid owner) => new() { PaymentReference = "CK-FIXTURE", PaymentProofUrl = $"/api/factory/attachments/{owner:N}/{Guid.Empty:N}" };
    private async Task<(Guid Owner, Guid FactoryOwner, Guid Depot, Guid Batch)> Seed()
    {
        await using var db = Open();
        var owner = U("DEPOT_OWNER"); var factoryOwner = U("FACTORY");
        var depot = new Depot { Owner = owner, Name = "Kho xác nhận", VerificationStatus = "APPROVED" };
        var factory = new Factory { Owner = factoryOwner, Name = "Nhà máy", AcceptedMaterialsCsv = "PET" };
        var pickup = new PickupRequest { Seller = U("SELLER"), TargetDepot = depot, Address = "Ngũ Hành Sơn", Status = "DONE" };
        pickup.Items.Add(new() { MaterialType = "PET", WeightKg = 100, PricePerKg = 1000, SubTotal = 100000 });
        db.AddRange(factory, pickup); await db.SaveChangesAsync();
        var created = await Batches(db).CreateAsync(owner.Id, depot.Id, new() { OperationId = Guid.NewGuid(), MaterialType = "PET", WeightKg = 10, TargetFactoryId = factory.Id });
        var batch = await db.InventoryBatches.SingleAsync(b => b.Id == created.Id);
        batch.Status = "WEIGHED"; batch.FactoryReceivedAt = DateTime.UtcNow;
        batch.QualityCheck = new() { Factory = factory, Batch = batch, ActualWeightKg = 10, IsAccepted = false, Grade = "A", PurityPercent = 95 };
        db.BatchQualityChecks.Add(batch.QualityCheck);
        await db.SaveChangesAsync(); return (owner.Id, factoryOwner.Id, depot.Id, batch.Id);
    }
    [Theory]
    [InlineData("RENEGOTIATE", "NEGOTIATING")]
    [InlineData("RETURN", "RETURN_REQUESTED")]
    public async Task Failed_qc_opens_a_resolution_without_releasing_goods(string resolution, string status)
    {
        var f = await Seed(); await using var db = Open();
        var result = await new FactoryQCService(db, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()).QualityAsync(f.FactoryOwner, f.Batch,
            new() { Accept = false, PurityPercent = 50, Grade = "C", Resolution = resolution, Note = "KCS không đạt, hai bên cần xử lý tiếp" }, default);
        Assert.Equal("Success", result.Outcome.ToString()); Assert.Equal(status, result.Data!.Status);
        Assert.False((await db.BatchQualityChecks.SingleAsync()).IsAccepted);
        var scope = new DepotService(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db));
        Assert.Equal(90m, (await new InventoryService(new DepotInventoryRepository(db), scope).GetAsync(f.Owner, f.Depot)).Single().AvailableKg);
        Assert.Contains("50", (await db.BatchWorkflowEvents.SingleAsync()).SnapshotJson);
    }

    private FactoryAttachmentService Attachments(AppDbContext db) => new(db, new UploadEnvironment { ContentRootPath = uploadRoot });
    private FactoryQualityResolutionService Resolutions(AppDbContext db) => new(db, Attachments(db));
    private static BatchReturnService Returns(AppDbContext db) => new(new DepotBatchRepository(db), new DepotUnitOfWork(db), new DepotService(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db)));
    private async Task<string> Upload(AppDbContext db, Guid owner)
    {
        await using var stream = new MemoryStream(new byte[] { 137,80,78,71,13,10,26,10,1,2,3 });
        return await Attachments(db).UploadAsync(owner, new Microsoft.AspNetCore.Http.FormFile(stream,0,stream.Length,"file","bien-ban-fixture.png"), default);
    }
    private static BatchReturnReceiptDto Receipt(decimal kg = 10) => new() { Confirmed = true, ReceivedKg = kg, Reference = "NHAN-TRA-FIXTURE", Note = "Đã cân và kiểm đủ toàn bộ lô tại kho" };
    private static Task<Retrack.API.Services.Shared.ServiceResult<QualityResponse>> Reject(AppDbContext db, Guid factory, Guid batch, string resolution) =>
        new FactoryQCService(db, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()).QualityAsync(factory, batch,
            new() { Accept=false,PurityPercent=50,Grade="C",Resolution=resolution,Note="Vật liệu không đạt ngưỡng ban đầu" }, default);
    [Fact]
    public async Task Return_is_two_sided_retry_safe_and_releases_original_fifo_once_after_receipt()
    {
        var f=await Seed(); string proof;
        await using(var db=Open())
        {
            await Reject(db,f.FactoryOwner,f.Batch,"RETURN"); proof=await Upload(db,f.FactoryOwner);
            await Assert.ThrowsAsync<DepotConflictException>(()=>Returns(db).ReceiveAsync(f.Owner,f.Depot,f.Batch,Receipt()));
            var input=new ReturnHandoverRequest { Reference="TRA-FIXTURE",ProofUrl=proof,Note="Đã bàn giao đủ lô cho bên vận chuyển trả" };
            Assert.Equal("Success",(await Resolutions(db).HandoverAsync(f.FactoryOwner,f.Batch,input,default)).Outcome.ToString());
            Assert.Equal("Success",(await Resolutions(db).HandoverAsync(f.FactoryOwner,f.Batch,input,default)).Outcome.ToString());
            var inventory=new InventoryService(new DepotInventoryRepository(db),new DepotService(new DepotOwnerRepository(db),new DepotPaymentReadRepository(db)));
            Assert.Equal(90m,(await inventory.GetAsync(f.Owner,f.Depot)).Single().AvailableKg);
            Assert.Equal(90m,(await new DepotStockRepository(db).AvailableLayersAsync(f.Depot,"PET")).Sum(x=>x.Quantity));
            await Assert.ThrowsAsync<DepotConflictException>(()=>Returns(db).ReceiveAsync(f.Owner,f.Depot,f.Batch,Receipt(9)));
            var outsider=U("DEPOT_OWNER"); db.Users.Add(outsider); await db.SaveChangesAsync();
            await Assert.ThrowsAsync<DepotForbiddenException>(()=>Returns(db).ReceiveAsync(outsider.Id,f.Depot,f.Batch,Receipt()));
            var fileId=Guid.ParseExact(proof.Split('/').Last(),"N");
            Assert.True(File.Exists((await Attachments(db).OpenAsync(f.Owner,"DEPOT_OWNER",f.FactoryOwner,fileId,default)).Path));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Attachments(db).OpenAsync(outsider.Id,"DEPOT_OWNER",f.FactoryOwner,fileId,default));
        }
        async Task Receive(){await using var db=Open();await Returns(db).ReceiveAsync(f.Owner,f.Depot,f.Batch,Receipt());}
        await Task.WhenAll(Receive(),Receive()); await Receive();
        await using var check=Open();
        Assert.Equal("RETURNED",(await check.InventoryBatches.SingleAsync()).Status);
        Assert.Equal(1,await check.BatchWorkflowEvents.CountAsync(e=>e.Action=="RETURN_RECEIVED"));
        Assert.Equal(1,await check.BatchWorkflowEvents.CountAsync(e=>e.Action=="RETURN_HANDED_OVER"));
        Assert.Equal(100m,(await new DepotStockRepository(check).AvailableLayersAsync(f.Depot,"PET")).Sum(x=>x.Quantity));
        Assert.Single(await check.PickupRequestItems.ToListAsync()); Assert.Empty(await check.DepotStockLines.ToListAsync()); Assert.Empty(await check.PlatformTransactions.ToListAsync());
        var changed=Receipt();changed.Reference="KHAC";await Assert.ThrowsAsync<DepotConflictException>(()=>Returns(check).ReceiveAsync(f.Owner,f.Depot,f.Batch,changed));
        await Batches(check).CreateAsync(f.Owner,f.Depot,new(){OperationId=Guid.NewGuid(),MaterialType="PET",WeightKg=100});
    }
    [Fact]
    public async Task Renegotiation_keeps_failed_qc_and_money_agreement_prevents_return()
    {
        var f=await Seed();await using var db=Open();await Reject(db,f.FactoryOwner,f.Batch,"RENEGOTIATE");
        var result=await new FactoryOrderService(db).SettleAsync(f.FactoryOwner,f.Batch,Price(),default);
        Assert.Equal("Success",result.Outcome.ToString()); Assert.Equal("SETTLEMENT_PENDING",result.Data!.Status);
        Assert.False((await db.BatchQualityChecks.SingleAsync()).IsAccepted); Assert.Equal("C",(await db.BatchQualityChecks.SingleAsync()).Grade);
        await Decisions(db).DecideAsync(f.Owner,f.Depot,f.Batch,new(){Revision=1,Amount=10000,Accept=true});
        Assert.Equal("Conflict",(await Resolutions(db).ChooseAsync(f.FactoryOwner,f.Batch,new(){Resolution="RETURN",Note="Muốn trả sau khi đồng ý tiền"},default)).Outcome.ToString());
        var proof=await Upload(db,f.FactoryOwner);
        Assert.Equal("Conflict",(await Resolutions(db).HandoverAsync(f.FactoryOwner,f.Batch,new(){Reference="TRA",ProofUrl=proof,Note="Không được trả sau thỏa thuận tiền"},default)).Outcome.ToString());
        Assert.Equal(3,await db.BatchWorkflowEvents.CountAsync()); Assert.Empty(await db.PlatformTransactions.ToListAsync());
    }
    [Fact]
    public async Task Legacy_rejection_requires_explicit_resolution_and_retry_preserves_qc_history()
    {
        var f=await Seed();await using var db=Open();var b=await db.InventoryBatches.Include(x=>x.QualityCheck).SingleAsync();b.Status="REJECTED";b.SettlementWorkflowVersion=0;b.QualityCheck!.Grade="C";await db.SaveChangesAsync();
        var input=new QualityResolutionRequest {Resolution="RENEGOTIATE",Note="Hai bên chọn thương lượng theo hiện trạng"};
        await Resolutions(db).ChooseAsync(f.FactoryOwner,f.Batch,input,default);await Resolutions(db).ChooseAsync(f.FactoryOwner,f.Batch,input,default);
        Assert.Equal(1,b.SettlementWorkflowVersion);Assert.Equal("NEGOTIATING",b.Status);Assert.False(b.QualityCheck.IsAccepted);Assert.Single(await db.BatchWorkflowEvents.ToListAsync());Assert.Null(b.SettledAt);
    }
    [Fact]
    public async Task Handover_rejects_foreign_proof_wrong_factory_and_wrong_state_without_changes()
    {
        var f=await Seed();await using var db=Open();var other=U("FACTORY");db.Factories.Add(new(){Owner=other,Name="Nhà máy khác"});await db.SaveChangesAsync();
        Assert.Equal("NotFound",(await Resolutions(db).ChooseAsync(other.Id,f.Batch,new(){Resolution="RETURN",Note="Sai nhà máy không được đổi hướng"},default)).Outcome.ToString());
        var foreign=await Upload(db,other.Id);
        Assert.Equal("Invalid",(await Resolutions(db).HandoverAsync(f.FactoryOwner,f.Batch,new(){Reference="TRA",ProofUrl=foreign,Note="Sai chứng từ"},default)).Outcome.ToString());
        var own=await Upload(db,f.FactoryOwner);
        Assert.Equal("Conflict",(await Resolutions(db).HandoverAsync(f.FactoryOwner,f.Batch,new(){Reference="TRA",ProofUrl=own,Note="Chưa có kết quả KCS"},default)).Outcome.ToString());
        Assert.Empty(await db.BatchWorkflowEvents.ToListAsync());
    }
    private sealed class UploadEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string ContentRootPath {get;set;}=""; public string WebRootPath {get;set;}=""; public string ApplicationName {get;set;}="Test"; public string EnvironmentName {get;set;}="Development";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider {get;set;}=new Microsoft.Extensions.FileProviders.NullFileProvider();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider {get;set;}=new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
