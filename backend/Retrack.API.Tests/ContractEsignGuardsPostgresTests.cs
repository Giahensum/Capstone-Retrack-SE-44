using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Services.Depot;
using Retrack.API.Services.ESign;
using Retrack.API.Services.Factory;
using Xunit;

namespace Retrack.API.Tests;

public sealed class ContractEsignGuardsPostgresTests : IAsyncLifetime
{
    private readonly string schema="esign_guard_"+Guid.NewGuid().ToString("N");
    private NpgsqlConnection admin=null!;
    private string connection=null!;
    private readonly Guid owner=Guid.NewGuid(),factoryOwner=Guid.NewGuid(),depotId=Guid.NewGuid(),partnerId=Guid.NewGuid();
    private readonly FakeDocuSign client=new();
    private static readonly string Terms=new('A',80);
    public async Task InitializeAsync()
    {
        var source=Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")!;
        var parsed=new NpgsqlConnectionStringBuilder(source);
        Assert.Equal("Retrack_TV2_test",parsed.Database);
        Assert.Equal("Retrack_TV2_test",new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION")).Database);
        Assert.Contains(parsed.Host,new[]{"localhost","127.0.0.1"});
        admin=new(source);await admin.OpenAsync();
        await using(var create=new NpgsqlCommand($"CREATE SCHEMA {schema}",admin))await create.ExecuteNonQueryAsync();
        connection=new NpgsqlConnectionStringBuilder(source){SearchPath=schema+",public",Pooling=false}.ConnectionString;
        var sql=await File.ReadAllTextAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../db/bootstrap/retrack-system.sql")));
        await using(var setup=new NpgsqlConnection(connection))
        {
            await setup.OpenAsync();await using var command=new NpgsqlCommand(sql[..sql.IndexOf("-- RETRACK_SCHEMA_END",StringComparison.Ordinal)]+"\nCOMMIT;",setup){CommandTimeout=120};
            await command.ExecuteNonQueryAsync();
        }
        await using var db=Open();
        var depot=new Depot{Id=depotId,Owner=new(){Id=owner,Role="DEPOT_OWNER",Email=Guid.NewGuid()+"@test.invalid",FullName="Kho ký thử"},Name="Kho Đà Nẵng",VerificationStatus="APPROVED"};
        var factory=new Factory{Owner=new(){Id=factoryOwner,Role="FACTORY",Email=Guid.NewGuid()+"@test.invalid",FullName="Nhà máy ký thử"},Name="Nhà máy Đà Nẵng"};
        var partner=new FactoryDepotPartnership{Id=partnerId,Depot=depot,Factory=factory,Status="PENDING",ContractStatus="PENDING_DEPOT",ContractVersion=1,ContractCode="HD-ESIGN-TEST",ContractTerms=Terms,
            ContractTermsHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Terms))).ToLowerInvariant(),ContractExpiresAt=DateTime.UtcNow.AddMonths(12)};
        var batch=new InventoryBatch{Depot=depot,TargetFactory=factory,MaterialType="PET",DeclaredWeightKg=10,Status="VERIFIED"};
        batch.QualityCheck=new(){Batch=batch,Factory=factory,ActualWeightKg=10,IsAccepted=true,Grade="A"};
        db.AddRange(partner,batch);await db.SaveChangesAsync();
    }
    public async Task DisposeAsync()
    {
        await using var drop=new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE",admin);await drop.ExecuteNonQueryAsync();await admin.DisposeAsync();
    }
    private AppDbContext Open()=>new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    private ContractEsignService Service(AppDbContext db)=>new(db,client,new ConfigurationBuilder().Build(),NullLogger<ContractEsignService>.Instance);
    private async Task Start()
    {
        await using var db=Open();await Service(db).CreateSigningUrlAsync(factoryOwner,partnerId,default);
    }
    private async Task Renew()
    {
        await using var db=Open();var result=await new FactoryPartnerService(db).ProposeContractAsync(factoryOwner,depotId,new(){Terms=Terms,ValidMonths=12,ConfirmElectronicSignature=true},default);
        Assert.Equal("Success",result.Outcome.ToString());
    }
    private static EnvelopeState Completed()=>new("completed",DateTime.UtcNow,
        [new("1","completed",DateTime.UtcNow,null),new("2","completed",DateTime.UtcNow,null)]);

    [PostgresEsignTheory]
    [InlineData("envelope")]
    [InlineData("esignHash")]
    [InlineData("termsHash")]
    [InlineData("version")]
    public async Task StaleProviderResultCannotActivateChangedContract(string changed)
    {
        await Start();client.Reply=Completed();
        client.OnRead=async()=>
        {
            await using var edit=Open();var p=await edit.FactoryDepotPartnerships.SingleAsync();
            if(changed=="envelope")p.EsignEnvelopeId=Guid.NewGuid().ToString();
            if(changed=="esignHash")p.EsignTermsHash=new('b',64);
            if(changed=="termsHash")p.ContractTermsHash=new('c',64);
            if(changed=="version")p.ContractVersion++;
            await edit.SaveChangesAsync();
        };
        await using var db=Open();await Assert.ThrowsAsync<DepotConflictException>(()=>Service(db).RefreshAsync(owner,partnerId,default));
        await using var check=Open();var saved=await check.FactoryDepotPartnerships.SingleAsync();
        Assert.Equal("PENDING_DEPOT",saved.ContractStatus);Assert.Null(saved.DepotSignedAt);Assert.NotEqual("COMPLETED",saved.EsignStatus);
    }

    [PostgresEsignTheory]
    [InlineData("factoryBlocked")]
    [InlineData("depotBlocked")]
    [InlineData("expired")]
    public async Task CompletedEnvelopeCannotActivateBlockedOrExpiredPartnership(string condition)
    {
        await Start();client.Reply=Completed();
        await using(var edit=Open())
        {
            var p=await edit.FactoryDepotPartnerships.SingleAsync();
            if(condition=="factoryBlocked")p.BlockedByFactory=true;
            if(condition=="depotBlocked")p.BlockedByDepot=true;
            if(condition=="expired")p.ContractExpiresAt=DateTime.UtcNow.AddDays(-1);
            await edit.SaveChangesAsync();
        }
        await using var db=Open();await Assert.ThrowsAsync<DepotConflictException>(()=>Service(db).RefreshAsync(owner,partnerId,default));
        await using var check=Open();Assert.Equal("PENDING_DEPOT",(await check.FactoryDepotPartnerships.SingleAsync()).ContractStatus);
    }

    [PostgresPickupFact]
    public async Task RenewalWithSameTermsCreatesFreshEnvelopeAndPreservesOldEnvelopeInAudit()
    {
        await Start();var old=client.LastEnvelope;client.Reply=Completed();
        await using(var db=Open())await Service(db).RefreshAsync(owner,partnerId,default);
        await using(var edit=Open()){var p=await edit.FactoryDepotPartnerships.SingleAsync();p.ContractExpiresAt=DateTime.UtcNow.AddDays(-1);await edit.SaveChangesAsync();}
        await Renew();client.Reply=new("sent",null,[]);await Start();
        Assert.Equal(2,client.Created);Assert.NotEqual(old,client.LastEnvelope);
        await using var check=Open();var saved=await check.FactoryDepotPartnerships.SingleAsync();
        Assert.Equal(2,saved.ContractVersion);Assert.Equal("PENDING_DEPOT",saved.ContractStatus);Assert.Null(saved.DepotSignedAt);
        Assert.Contains(old!,Assert.Single(await check.AuditLogs.Where(a=>a.Action=="RENEW_CONTRACT").ToListAsync()).OldData);
    }

    [PostgresPickupFact]
    public async Task ProposalChangedDuringEnvelopeCreationIsNotOverwrittenByOldProviderResult()
    {
        client.OnCreate=Renew;
        await using var db=Open();await Assert.ThrowsAsync<DepotConflictException>(()=>Service(db).CreateSigningUrlAsync(factoryOwner,partnerId,default));
        await using var check=Open();var saved=await check.FactoryDepotPartnerships.SingleAsync();
        Assert.Equal(2,saved.ContractVersion);Assert.Null(saved.EsignEnvelopeId);Assert.Equal("PENDING_DEPOT",saved.ContractStatus);
    }

    [PostgresPickupFact]
    public async Task ValidCurrentEnvelopeActivatesOnceButOldWebhookCannotActivateRenewedVersion()
    {
        await Start();client.Reply=Completed();
        await using(var db=Open())
        {
            var service=Service(db);Assert.Equal("ACTIVE",(await service.RefreshAsync(owner,partnerId,default)).ContractStatus);
            var signed=(await db.FactoryDepotPartnerships.AsNoTracking().SingleAsync()).DepotSignedAt;
            Assert.Equal("ACTIVE",(await service.RefreshAsync(owner,partnerId,default)).ContractStatus);
            Assert.Equal(signed,(await db.FactoryDepotPartnerships.SingleAsync()).DepotSignedAt);
        }
        await using(var edit=Open()){var p=await edit.FactoryDepotPartnerships.SingleAsync();p.ContractStatus="PENDING_DEPOT";p.DepotSignedAt=null;await edit.SaveChangesAsync();}
        client.OnRead=Renew;
        await using(var db=Open())await Service(db).HandleWebhookAsync("{\"envelopeId\":\""+client.LastEnvelope+"\"}",default);
        await using var check=Open();var current=await check.FactoryDepotPartnerships.SingleAsync();
        Assert.Equal(2,current.ContractVersion);Assert.Equal("PENDING_DEPOT",current.ContractStatus);Assert.Null(current.DepotSignedAt);
    }

    private sealed class FakeDocuSign : IDocuSignClient
    {
        public bool IsConfigured=>true;
        public int Created{get;private set;}
        public string? LastEnvelope{get;private set;}
        public EnvelopeState Reply{get;set;}=new("sent",null,[]);
        public Func<Task>? OnRead{get;set;}
        public Func<Task>? OnCreate{get;set;}
        public async Task<string> CreateEnvelopeAsync(string subject,string documentName,string html,IReadOnlyList<EnvelopeSigner> signers,CancellationToken ct)
        {Created++;LastEnvelope=Guid.NewGuid().ToString();var hook=OnCreate;OnCreate=null;if(hook!=null)await hook();return LastEnvelope;}
        public Task<string> CreateSigningUrlAsync(string envelopeId,EnvelopeSigner signer,string returnUrl,CancellationToken ct)=>Task.FromResult("https://example.invalid/sign/"+envelopeId);
        public async Task<EnvelopeState> GetEnvelopeAsync(string envelopeId,CancellationToken ct)
        {var result=Reply;var hook=OnRead;OnRead=null;if(hook!=null)await hook();return result;}
        public Task<byte[]> DownloadCombinedAsync(string envelopeId,CancellationToken ct)=>Task.FromResult("%PDF-TEST"u8.ToArray());
    }
}

public sealed class PostgresEsignTheoryAttribute : TheoryAttribute
{
    public PostgresEsignTheoryAttribute()
    {
        if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")))
            Skip="Cần PostgreSQL test riêng để kiểm tra race hợp đồng.";
    }
}
