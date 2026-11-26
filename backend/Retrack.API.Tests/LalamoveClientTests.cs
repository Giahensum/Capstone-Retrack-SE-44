using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Shipping;
using Xunit;

namespace Retrack.API.Tests;

public sealed class LalamoveClientTests
{
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>send(request);
    }
    [Fact] public async Task QuoteUsesOnlySandboxAndSignsExactBodyWithVietnamMarket() {
        var settings=new ShippingOptions {Mode="LalamoveSandbox",ApiKey="pk_test_fixture",ApiSecret="sk_test_fixture",ServiceType="TEST_TRUCK"};
        var handler=new Handler(async request=>{
            Assert.Equal("rest.sandbox.lalamove.com",request.RequestUri!.Host);
            Assert.Equal("/v3/quotations",request.RequestUri.AbsolutePath);Assert.Equal("VN",request.Headers.GetValues("Market").Single());
            var body=await request.Content!.ReadAsStringAsync();
            var auth=request.Headers.GetValues("Authorization").Single().Replace("hmac ","").Split(':');
            var expected=Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.ApiSecret),Encoding.UTF8.GetBytes($"{auth[1]}\r\nPOST\r\n/v3/quotations\r\n\r\n{body}"))).ToLowerInvariant();
            Assert.Equal(expected,auth[2]);
            using var payload=JsonDocument.Parse(body);Assert.Equal("TEST_TRUCK",payload.RootElement.GetProperty("data").GetProperty("serviceType").GetString());
            return new(HttpStatusCode.Created){Content=new StringContent(JsonSerializer.Serialize(new {data=new {quotationId="quote-test",expiresAt=DateTime.UtcNow.AddMinutes(5),stops=new[]{new {stopId="1"},new {stopId="2"}},priceBreakdown=new {total="500000",currency="VND"},distance=new {value="10000",unit="m"}}}))};
        });
        var client=new LalamoveSandboxProvider(new HttpClient(handler),Options.Create(settings));
        var result=await client.QuoteAsync(new(Guid.NewGuid(),"PET",1000,"Kho",16m,108m,"Nhà máy",16.1m,108.1m),default);
        Assert.Equal(500000,result.Amount);Assert.Equal(10,result.DistanceKm);
    }
    [Fact] public async Task ProductionKeysCannotBeUsedEvenWithSandboxClient() {
        var client=new LalamoveSandboxProvider(new HttpClient(new Handler(_=>throw new Exception("Không được gửi HTTP"))),
            Options.Create(new ShippingOptions {ApiKey="pk_prod_fixture",ApiSecret="sk_prod_fixture",ServiceType="TRUCK"}));
        await Assert.ThrowsAsync<DepotConflictException>(()=>client.CitiesAsync(default));
    }
    [Fact] public async Task DemoProgressionAndCancelAreIndependentOfDriverMobile() {
        var demo=new DemoShippingProvider(Options.Create(new ShippingOptions {DemoStepSeconds=15}));
        var shipment=new Retrack.API.Models.ExternalShipment {ProviderOrderId="RT-TEST",CreatedAt=DateTime.UtcNow.AddSeconds(-31)};
        Assert.Equal("PICKED_UP",(await demo.GetAsync(shipment,default)).Status);
        shipment.CreatedAt=DateTime.UtcNow.AddSeconds(-46);
        Assert.Equal("COMPLETED",(await demo.GetAsync(shipment,default)).Status);
    }
}
