using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Shipping;
using Xunit;

namespace Retrack.API.Tests;

public sealed class ShippingPostgresTests
{
    [PostgresPickupFact]
    public async Task BootstrapUpgradeAndConcurrentBookingsUseOneCarrierRequest()
    {
        var connection=Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")!;
        Assert.Equal("Retrack_TV2_test",new NpgsqlConnectionStringBuilder(connection).Database);
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root!=null&&!Directory.Exists(Path.Combine(root.FullName,"db","bootstrap")))root=root.Parent;
        Assert.NotNull(root);
        var schema="shipping_"+Guid.NewGuid().ToString("N");
        await using var admin=new NpgsqlConnection(connection);await admin.OpenAsync();
        await using(var create=new NpgsqlCommand($"CREATE SCHEMA {schema}",admin))await create.ExecuteNonQueryAsync();
        var scoped=new NpgsqlConnectionStringBuilder(connection){SearchPath=schema,Pooling=false}.ConnectionString;
        AppDbContext Context()=>new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scoped).Options);
        var settings=Options.Create(new ShippingOptions{Mode="Demo"});
        ShippingService Service(AppDbContext db)=>new(db,new DepotService(new DepotOwnerRepository(db),new DepotPaymentReadRepository(db)),new DemoShippingProvider(settings),settings);
        try {
            await using(var sql=new NpgsqlConnection(scoped)) {
                await sql.OpenAsync();
                var bootstrap=await File.ReadAllTextAsync(Path.Combine(root!.FullName,"db/bootstrap/retrack-system.sql"));
                bootstrap=bootstrap.Replace("SET LOCAL retrack.seed_demo = 'on';","SET LOCAL retrack.seed_demo = 'off';");
                await using(var cmd=new NpgsqlCommand(bootstrap,sql))await cmd.ExecuteNonQueryAsync();
                var upgrade=await File.ReadAllTextAsync(Path.Combine(root.FullName,"db/shipping/01_shipping_schema.sql"));
                await using(var cmd=new NpgsqlCommand(upgrade,sql))await cmd.ExecuteNonQueryAsync();
            }
            await using var setup=Context();
            var depot=new Depot {Owner=new(){Role="DEPOT_OWNER",Email="shipping-pg-owner@test.local"},VerificationStatus="APPROVED",Address="Kho",Latitude=16m,Longitude=108m};
            var batch=new InventoryBatch {Depot=depot,TargetFactory=new(){Owner=new(){Role="FACTORY",Email="shipping-pg-factory@test.local"},Address="Nhà máy",Latitude=16.1m,Longitude=108.1m},MaterialType="PET",DeclaredWeightKg=1000,ShippingMethod="EXTERNAL",Status="TRANSPORT_READY"};
            setup.InventoryBatches.Add(batch);await setup.SaveChangesAsync();
            var quote=await Service(setup).QuoteAsync(depot.OwnerId,depot.Id,new(batch.TargetFactoryId!.Value,"PET",1000,"EXTERNAL"),default);
            var contacts=new ShippingContacts("Người giao","0901234567","Người nhận","0912345678",null);
            async Task<bool> Book() {
                await using var db=Context();
                try{await Service(db).BookAsync(depot.OwnerId,depot.Id,batch.Id,new(Guid.NewGuid(),quote.Id,contacts),default);return true;}
                catch(DepotConflictException){return false;}
            }
            var results=await Task.WhenAll(Book(),Book());Assert.Single(results,x=>x);
            Assert.Equal(1,await setup.ExternalShipments.CountAsync());
            Assert.Equal("EXTERNAL",await setup.InventoryBatches.Where(b=>b.Id==batch.Id).Select(b=>b.ShippingMethod).SingleAsync());
        } finally {
            await using var drop=new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE",admin);await drop.ExecuteNonQueryAsync();
        }
    }
}
