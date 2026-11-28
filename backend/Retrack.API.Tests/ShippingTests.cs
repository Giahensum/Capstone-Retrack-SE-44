using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Shipping;
using Xunit;

namespace Retrack.API.Tests;

public sealed class ShippingTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly ShippingOptions config = new() {Mode="Demo",DemoStepSeconds=15};
    private readonly User owner=new(){Role="DEPOT_OWNER",Email="shipping-owner@test.local"};
    private readonly User driver=new(){Role="DRIVER",Email="shipping-driver@test.local"};
    private Depot depot=null!;
    private Factory factory=null!;
    private InventoryBatch batch=null!;
    private ShippingService Service(IShippingProvider? provider=null)=>new(new ShippingRepository(db),
        new DepotService(new DepotOwnerRepository(db),new DepotPaymentReadRepository(db)),
        provider??new DemoShippingProvider(Options.Create(config)),Options.Create(config),new DriverDeliveryRepository(db));
    public async Task InitializeAsync() {
        await connection.OpenAsync();connection.CreateFunction("NOW",()=>DateTime.UtcNow.ToString("O"));
        db=new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);await db.Database.EnsureCreatedAsync();
        depot=new(){Name="Kho test",Address="Đà Nẵng",Owner=owner,VerificationStatus="APPROVED",Latitude=16.05m,Longitude=108.2m};
        factory=new(){Name="Nhà máy test",Address="Hòa Khánh",Owner=new(){Role="FACTORY",Email="shipping-factory@test.local"},Latitude=16.08m,Longitude=108.15m};
        batch=new(){Depot=depot,TargetFactory=factory,MaterialType="PET",DeclaredWeightKg=1200,Status="TRANSPORT_READY",ShippingMethod="EXTERNAL"};
        db.InventoryBatches.Add(batch);db.DepotStaffs.Add(new(){Depot=depot,User=driver,StaffType="DRIVER"});await db.SaveChangesAsync();
    }
    public async Task DisposeAsync(){await db.DisposeAsync();await connection.DisposeAsync();}
    private Task<ShippingQuote> Quote(string method="EXTERNAL")=>Service().QuoteAsync(owner.Id,depot.Id,new(factory.Id,"PET",1200,method),default);
    private static ShippingContacts Contacts=>new("Người giao","0901234567","Người nhận","0912345678","12 bao PET, đã đóng gọn");
    [Fact] public async Task InternalQuoteUsesNinetyPercentWithoutCreatingCarrierOrder() {
        var quote=await Quote("INTERNAL");Assert.Equal(.9m,quote.Factor);Assert.Equal(decimal.Ceiling(quote.Amount*.9m),quote.EstimatedCost);
        Assert.Empty(await db.ExternalShipments.ToListAsync());
    }
    [Fact] public async Task BookingRetryDoesNotCreateTwoOrdersAndDeliveryDoesNotCompleteFactoryWorkflow() {
        var quote=await Quote();var input=new BookShippingRequest(Guid.NewGuid(),quote.Id,Contacts);
        await Service().BookAsync(owner.Id,depot.Id,batch.Id,input,default);await Service().BookAsync(owner.Id,depot.Id,batch.Id,input,default);
        var shipment=await db.ExternalShipments.SingleAsync();Assert.Equal("ASSIGNING_DRIVER",shipment.Status);
        await Service().ApplyAsync(shipment.Id,new(shipment.ProviderOrderId!,"PICKED_UP",null,quote.Amount),"TEST",default);
        Assert.Equal("IN_TRANSIT",(await db.InventoryBatches.FindAsync(batch.Id))!.Status);
        await Service().ApplyAsync(shipment.Id,new(shipment.ProviderOrderId!,"COMPLETED",null,quote.Amount),"TEST",default);
        var delivered=await db.InventoryBatches.Include(b=>b.TransportJob).SingleAsync(b=>b.Id==batch.Id);
        Assert.Equal("DELIVERED",delivered.Status);Assert.Equal("DELIVERED",delivered.TransportJob!.Status);Assert.Null(delivered.FactoryReceivedAt);
        await Service().ApplyAsync(shipment.Id,new(shipment.ProviderOrderId!,"ON_GOING",null,quote.Amount),"TEST",default);
        Assert.Equal("COMPLETED",(await db.ExternalShipments.SingleAsync()).Status);
    }
    [Fact] public async Task OwnerRefreshChecksScopeAndSynchronizesSkippedStagesWithoutConfirmingFactoryReceipt() {
        var quote=await Quote();
        await Service().BookAsync(owner.Id,depot.Id,batch.Id,new(Guid.NewGuid(),quote.Id,Contacts),default);
        var shipment=await db.ExternalShipments.SingleAsync();
        shipment.CreatedAt=DateTime.UtcNow.AddHours(-1);
        shipment.LastCheckedAt=DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotForbiddenException>(()=>Service().RefreshForOwnerAsync(Guid.NewGuid(),depot.Id,batch.Id,default));
        await Service().RefreshForOwnerAsync(owner.Id,depot.Id,batch.Id,default);
        var result=await db.InventoryBatches.Include(b=>b.TransportJob).SingleAsync(b=>b.Id==batch.Id);
        Assert.Equal("DELIVERED",result.Status);
        Assert.Equal("DELIVERED",result.TransportJob!.Status);
        Assert.Null(result.FactoryReceivedAt);
        Assert.Equal("COMPLETED",(await db.ExternalShipments.SingleAsync()).Status);
    }
    [Fact] public async Task WrongOwnerCannotQuoteOrReadBatch() {
        await Assert.ThrowsAsync<DepotForbiddenException>(()=>Service().QuoteAsync(Guid.NewGuid(),depot.Id,new(factory.Id,"PET",100),default));
        await Assert.ThrowsAsync<DepotForbiddenException>(()=>Service().GetAsync(Guid.NewGuid(),depot.Id,batch.Id,default));
    }
    [Fact] public async Task ExpiredAndChangedRouteQuotesCannotBeBooked() {
        var q=await Quote();q.ExpiresAt=DateTime.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotConflictException>(()=>Service().BookAsync(owner.Id,depot.Id,batch.Id,new(Guid.NewGuid(),q.Id,Contacts),default));
        Assert.Empty(await db.ExternalShipments.ToListAsync());
        q.ExpiresAt=DateTime.UtcNow.AddMinutes(2);factory.Latitude+=.01m;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotConflictException>(()=>Service().BookAsync(owner.Id,depot.Id,batch.Id,new(Guid.NewGuid(),q.Id,Contacts),default));
    }
    [Fact] public async Task DriverPoolAndClaimExcludeExternalBatch() {
        var job=new TransportJob {Batch=batch};db.TransportJobs.Add(job);await db.SaveChangesAsync();
        var repository=new DriverJobRepository(db);
        Assert.Equal(0,(await repository.ListAsync(driver.Id,false,1,default)).TotalCount);
        Assert.Equal(0,await repository.ClaimAsync(driver.Id,job.Id,DateTime.UtcNow,default));
    }
    [Fact] public async Task PendingFactoryAndClaimedDriverBlockBooking() {
        var q=await Quote();batch.Status="PENDING_APPROVAL";await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotConflictException>(()=>Service().BookAsync(owner.Id,depot.Id,batch.Id,new(Guid.NewGuid(),q.Id,Contacts),default));
        batch.Status="TRANSPORT_READY";db.TransportJobs.Add(new(){Batch=batch,Driver=driver,Status="ACCEPTED"});await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotConflictException>(()=>Service().BookAsync(owner.Id,depot.Id,batch.Id,new(Guid.NewGuid(),q.Id,Contacts),default));
    }
    [Fact] public async Task CannotSwitchToInternalWhileCarrierActiveAndCancellationDoesNotRestoreStock() {
        var q=await Quote();await Service().BookAsync(owner.Id,depot.Id,batch.Id,new(Guid.NewGuid(),q.Id,Contacts),default);
        await Assert.ThrowsAsync<DepotConflictException>(()=>Service().SetMethodAsync(owner.Id,depot.Id,batch.Id,"INTERNAL",null,default));
        await Service().CancelAsync(owner.Id,depot.Id,batch.Id,default);
        Assert.Equal("CANCELED",(await db.ExternalShipments.SingleAsync()).Status);
        Assert.Equal("TRANSPORT_READY",(await db.InventoryBatches.FindAsync(batch.Id))!.Status);
        await Service().SetMethodAsync(owner.Id,depot.Id,batch.Id,"INTERNAL",null,default);
        Assert.Equal("PENDING",(await db.TransportJobs.SingleAsync()).Status);
    }
    private sealed class TimeoutProvider: IShippingProvider {
        public string Name=>"DEMO";
        public Task<ProviderQuote> QuoteAsync(ShippingRoute r,CancellationToken ct)=>throw new NotSupportedException();
        public Task<ProviderOrder> BookAsync(ShippingQuote q,ShippingContacts c,Guid op,Guid batch,CancellationToken ct)=>throw new HttpRequestException("timeout");
        public Task<ProviderOrder> GetAsync(ExternalShipment s,CancellationToken ct)=>throw new NotSupportedException();
        public Task CancelAsync(ExternalShipment s,CancellationToken ct)=>throw new NotSupportedException();
    }
    [Fact] public async Task AmbiguousBookingLocksRetriesInsteadOfCreatingDuplicateOrder() {
        var q=await Quote();var input=new BookShippingRequest(Guid.NewGuid(),q.Id,Contacts);
        await Assert.ThrowsAsync<DepotConflictException>(()=>Service(new TimeoutProvider()).BookAsync(owner.Id,depot.Id,batch.Id,input,default));
        Assert.Equal("UNKNOWN",(await db.ExternalShipments.SingleAsync()).Status);
        await Assert.ThrowsAsync<DepotConflictException>(()=>Service().BookAsync(owner.Id,depot.Id,batch.Id,input with {OperationId=Guid.NewGuid()},default));
        Assert.Single(await db.ExternalShipments.ToListAsync());
    }
    [Theory][InlineData(0)][InlineData(3000)] public async Task InvalidWeightRejected(decimal weight) =>
        await Assert.ThrowsAsync<ArgumentException>(()=>Service().QuoteAsync(owner.Id,depot.Id,new(factory.Id,"PET",weight),default));
    [Fact] public void ContactsNormalizeVietnamesePhonesAndRejectInvalidInput() {
        Assert.Equal("+84901234567",ShippingService.ValidateContacts(Contacts).SenderPhone);
        Assert.Throws<ArgumentException>(()=>ShippingService.ValidateContacts(Contacts with {SenderPhone="123"}));
    }
    [Fact] public async Task DisabledProviderDoesNotSilentlyFallbackToDemo() {
        config.Mode="Disabled";await Assert.ThrowsAsync<DepotConflictException>(()=>Quote());
    }

    [Fact] public async Task Shipping_detail_keeps_public_shape_and_excludes_entity_graph() {
        var value=System.Text.Json.JsonSerializer.SerializeToElement(await Service().GetAsync(owner.Id,depot.Id,batch.Id,default));
        Assert.Equal(new[]{"enabled","events","method","quote","receivedAt","shipment"},value.EnumerateObject().Select(x=>x.Name).Order().ToArray());
        Assert.Equal("EXTERNAL",value.GetProperty("method").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null,value.GetProperty("shipment").ValueKind);
        Assert.Empty(value.GetProperty("events").EnumerateArray());
    }
    [Fact] public async Task Webhook_only_marks_known_sandbox_shipment_without_advancing_batch_or_payment() {
        var quote=await Quote();
        var shipment=new ExternalShipment {BatchId=batch.Id,QuoteId=quote.Id,OperationId=Guid.NewGuid(),Provider="LALAMOVE_SANDBOX",ProviderOrderId="sandbox-only",Status="ASSIGNING_DRIVER",Amount=12345};
        db.ExternalShipments.Add(shipment);await db.SaveChangesAsync();
        var controller=new Retrack.API.Controllers.Depot.ShippingWebhookController(new ShippingWebhookService(new ShippingRepository(db)));
        var payload=System.Text.Json.JsonSerializer.SerializeToElement(new {data=new {orderId="sandbox-only",status="COMPLETED",amount=1}});
        await controller.Receive(payload,default); db.ChangeTracker.Clear();
        var row=await db.ExternalShipments.SingleAsync(); Assert.NotNull(row.LastWebhookAt);Assert.Equal("ASSIGNING_DRIVER",row.Status);Assert.Equal(12345,row.Amount);
        Assert.Equal("TRANSPORT_READY",(await db.InventoryBatches.SingleAsync()).Status);Assert.Empty(await db.ShippingEvents.ToListAsync());
        var stamped=row.LastWebhookAt;await controller.Receive(payload,default);db.ChangeTracker.Clear();
        Assert.Equal(stamped,(await db.ExternalShipments.SingleAsync()).LastWebhookAt);
    }

    [Fact] public async Task Shipping_detail_preserves_event_fields_and_bounded_history() {
        var quote=await Quote();
        await Service().BookAsync(owner.Id,depot.Id,batch.Id,new(Guid.NewGuid(),quote.Id,Contacts),default);
        var shipment=await db.ExternalShipments.SingleAsync();
        var epoch=DateTime.UtcNow.AddHours(-1);
        db.ShippingEvents.AddRange(Enumerable.Range(0,60).Select(i=>new ShippingEvent {ShipmentId=shipment.Id,Status="ON_GOING",Source="TEST",CreatedAt=epoch.AddSeconds(i)}));
        await db.SaveChangesAsync();
        var value=System.Text.Json.JsonSerializer.SerializeToElement(await Service().GetAsync(owner.Id,depot.Id,batch.Id,default));
        var events=value.GetProperty("events").EnumerateArray().ToArray();
        Assert.Equal(50,events.Length);
        Assert.Equal(new[]{"CreatedAt","Source","Status"},events[0].EnumerateObject().Select(p=>p.Name).Order().ToArray());
        Assert.Equal("BOOKING_RESPONSE",events[0].GetProperty("Source").GetString());
        Assert.False(value.GetProperty("shipment").TryGetProperty("Batch",out _));
    }

    [Fact] public async Task Reconciliation_marks_stale_booking_and_retries_only_eligible_provider_orders() {
        var now=DateTime.UtcNow;
        ExternalShipment New(string state,string provider,string? order,DateTime checkedAt,DateTime created) {
            var lot=new InventoryBatch {DepotId=depot.Id,TargetFactoryId=factory.Id,MaterialType="PET",DeclaredWeightKg=1200,Status="TRANSPORT_READY",ShippingMethod="EXTERNAL"};
            var quote=new ShippingQuote {OwnerId=owner.Id,DepotId=depot.Id,FactoryId=factory.Id,MaterialType="PET",Provider=provider,ExpiresAt=now.AddMinutes(5)};
            db.InventoryBatches.Add(lot);db.ShippingQuotes.Add(quote);
            return new() {BatchId=lot.Id,QuoteId=quote.Id,OperationId=Guid.NewGuid(),Status=state,Provider=provider,ProviderOrderId=order,LastCheckedAt=checkedAt,CreatedAt=created};
        }
        var stale=New("BOOKING","DEMO",null,now,now.AddMinutes(-3));
        var fresh=New("BOOKING","DEMO",null,now,now);
        var eligible=Enumerable.Range(0,6).Select(i=>New("ON_GOING","DEMO","order-"+i,now.AddMinutes(-5).AddSeconds(i),now)).ToArray();
        var wrong=New("ON_GOING","OTHER","other",now.AddMinutes(-5),now);
        var terminal=New("COMPLETED","DEMO","done",now.AddMinutes(-5),now);
        var recent=New("ON_GOING","DEMO","recent",now,now);
        db.ExternalShipments.AddRange(eligible.Concat(new[]{stale,fresh,wrong,terminal,recent}));
        await db.SaveChangesAsync();
        var ids=await Service().ReconciliationCandidatesAsync(default);
        Assert.Equal(eligible.Take(5).Select(s=>s.Id),ids);
        db.ChangeTracker.Clear();
        Assert.Equal("UNKNOWN",(await db.ExternalShipments.FindAsync(stale.Id))!.Status);
        Assert.Equal("BOOKING",(await db.ExternalShipments.FindAsync(fresh.Id))!.Status);
        await Service().RecordSyncFailureAsync(eligible[0].Id,default);
        db.ChangeTracker.Clear();
        var failed=(await db.ExternalShipments.FindAsync(eligible[0].Id))!;
        Assert.Equal("ON_GOING",failed.Status);
        Assert.True(failed.LastCheckedAt>=now);
        Assert.Equal("Chưa đồng bộ được trạng thái vận chuyển. Hệ thống sẽ thử lại.",failed.LastError);
        Assert.Empty(await db.ShippingEvents.ToListAsync());
    }
}
