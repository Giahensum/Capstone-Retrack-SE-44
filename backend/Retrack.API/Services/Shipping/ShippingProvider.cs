using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Retrack.API.Models;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.Shipping;

public sealed class ShippingOptions
{
    public string Mode { get; set; } = "Disabled";
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public string ServiceType { get; set; } = "";
    public decimal MaxWeightKg { get; set; } = 2500;
    public decimal InternalFactor { get; set; } = .90m;
    public int DemoStepSeconds { get; set; } = 45;
}

public sealed record ShippingRoute(Guid FactoryId, string MaterialType, decimal WeightKg,
    string FromAddress, decimal FromLat, decimal FromLng, string ToAddress, decimal ToLat, decimal ToLng);
public sealed record QuoteRequest(Guid FactoryId, string MaterialType, decimal WeightKg, string Method = "INTERNAL");
public sealed record ShippingContacts(string SenderName, string SenderPhone, string RecipientName, string RecipientPhone, string? Note);
public sealed record BookShippingRequest(Guid OperationId, Guid QuoteId, ShippingContacts Contacts);
public sealed record ProviderQuote(string Id, decimal Amount, decimal DistanceKm, DateTime ExpiresAt, string Json);
public sealed record ProviderOrder(string Id, string Status, string? ShareLink, decimal? Amount, string? OperationId = null);

public interface IShippingProvider
{
    string Name { get; }
    Task<ProviderQuote> QuoteAsync(ShippingRoute route, CancellationToken ct);
    Task<ProviderOrder> BookAsync(ShippingQuote quote, ShippingContacts contacts, Guid operation, Guid batchId, CancellationToken ct);
    Task<ProviderOrder> GetAsync(ExternalShipment shipment, CancellationToken ct);
    Task CancelAsync(ExternalShipment shipment, CancellationToken ct);
}

// Chỉ cho phép Sandbox. Không có đường gọi production từ implementation này.
public sealed class LalamoveSandboxProvider(HttpClient http, IOptions<ShippingOptions> settings) : IShippingProvider
{
    public string Name => "LALAMOVE_SANDBOX";
    private ShippingOptions Options => settings.Value;
    public Task<JsonElement> CitiesAsync(CancellationToken ct) => SendAsync(HttpMethod.Get,"/v3/cities",null,Guid.NewGuid(),ct);
    public static string Signature(string secret, string timestamp, string method, string path, string body) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}\r\n{method}\r\n{path}\r\n\r\n{body}"))).ToLowerInvariant();

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object? payload, Guid requestId, CancellationToken ct)
    {
        if (!Options.ApiKey.StartsWith("pk_test", StringComparison.Ordinal) || !Options.ApiSecret.StartsWith("sk_test", StringComparison.Ordinal))
            throw new DepotConflictException("Chưa cấu hình khóa vận chuyển hợp lệ. Liên hệ người quản trị.");
        var body = payload == null ? "" : JsonSerializer.Serialize(payload);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        using var request = new HttpRequestMessage(method, "https://rest.sandbox.lalamove.com" + path);
        request.Headers.TryAddWithoutValidation("Authorization", $"hmac {Options.ApiKey}:{timestamp}:{Signature(Options.ApiSecret, timestamp, method.Method, path, body)}");
        request.Headers.TryAddWithoutValidation("Market", "VN");
        request.Headers.TryAddWithoutValidation("Request-ID", requestId.ToString());
        if (payload != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct);
        // Không ghi payload, thông tin liên hệ hoặc Authorization vào log.
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Dịch vụ vận chuyển trả mã {(int)response.StatusCode}. Mã tra cứu {requestId}.");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return default;
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.GetProperty("data").Clone();
    }

    private static string Text(JsonElement e, string key) => e.TryGetProperty(key, out var v) ? v.ToString() : "";
    private static decimal Number(JsonElement e, string key) => decimal.TryParse(Text(e, key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : throw new HttpRequestException("Báo giá vận chuyển không hợp lệ.");
    private static decimal? Price(JsonElement e)
    {
        if (!e.TryGetProperty("priceBreakdown", out var price)) return null;
        if (Text(price, "currency") != "VND") throw new HttpRequestException("Dịch vụ vận chuyển chưa trả giá bằng VND.");
        var amount = Number(price, "total");
        if (amount < 0 || amount > 1_000_000_000m) throw new HttpRequestException("Giá vận chuyển vượt phạm vi cho phép.");
        return amount;
    }
    public async Task<ProviderQuote> QuoteAsync(ShippingRoute route, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Options.ServiceType)) throw new DepotConflictException("Chưa chọn mã loại xe vận chuyển trong cấu hình.");
        object Stop(string address, decimal lat, decimal lng) => new { address, coordinates = new { lat = lat.ToString(CultureInfo.InvariantCulture), lng = lng.ToString(CultureInfo.InvariantCulture) } };
        var data = await SendAsync(HttpMethod.Post, "/v3/quotations", new { data = new {
            serviceType = Options.ServiceType, language = "vi_VN", isRouteOptimized = false,
            stops = new[] { Stop(route.FromAddress, route.FromLat, route.FromLng), Stop(route.ToAddress, route.ToLat, route.ToLng) }
        } }, Guid.NewGuid(), ct);
        var stops = data.GetProperty("stops");
        if (stops.GetArrayLength() != 2 || string.IsNullOrEmpty(Text(data,"quotationId"))) throw new HttpRequestException("Báo giá thiếu thông tin điểm giao nhận.");
        var distance = data.GetProperty("distance");
        var km = Number(distance,"value") / (Text(distance,"unit") == "m" ? 1000m : Text(distance,"unit") == "km" ? 1m : throw new HttpRequestException("Đơn vị khoảng cách không hợp lệ."));
        return new(Text(data,"quotationId"), Price(data) ?? throw new HttpRequestException("Thiếu giá vận chuyển."), km,
            DateTime.Parse(Text(data,"expiresAt"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal), data.GetRawText());
    }
    public async Task<ProviderOrder> BookAsync(ShippingQuote quote, ShippingContacts c, Guid operation, Guid batchId, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(quote.ProviderJson);
        var stops = doc.RootElement.GetProperty("stops");
        var route = JsonSerializer.Deserialize<ShippingRoute>(quote.SnapshotJson)!;
        var data = await SendAsync(HttpMethod.Post, "/v3/orders", new { data = new {
            quotationId = quote.ProviderQuoteId,
            sender = new { stopId = Text(stops[0],"stopId"), name = c.SenderName, phone = c.SenderPhone },
            recipients = new[] { new { stopId = Text(stops[1],"stopId"), name = c.RecipientName, phone = c.RecipientPhone, remarks = c.Note } },
            metadata = new { batchId = batchId.ToString(), materialType = route.MaterialType, weightKg = route.WeightKg.ToString(CultureInfo.InvariantCulture), operationId = operation.ToString() }
        } }, operation, ct);
        return ParseOrder(data);
    }
    private static ProviderOrder ParseOrder(JsonElement data)
    {
        var id = Text(data,"orderId");
        if (string.IsNullOrWhiteSpace(id)) throw new HttpRequestException("Thiếu mã vận đơn từ nhà vận chuyển.");
        var link = Text(data,"shareLink");
        return new(id, Text(data,"status"), Uri.TryCreate(link,UriKind.Absolute,out var uri) && uri.Scheme == "https" ? link : null, Price(data),
            data.TryGetProperty("metadata",out var metadata) ? Text(metadata,"operationId") : null);
    }
    public async Task<ProviderOrder> GetAsync(ExternalShipment s, CancellationToken ct) => ParseOrder(await SendAsync(HttpMethod.Get, "/v3/orders/" + Uri.EscapeDataString(s.ProviderOrderId!), null, Guid.NewGuid(), ct));
    public async Task CancelAsync(ExternalShipment s, CancellationToken ct) => await SendAsync(HttpMethod.Delete, "/v3/orders/" + Uri.EscapeDataString(s.ProviderOrderId!), null, Guid.NewGuid(), ct);
}

// Nhà cung cấp demo độc lập, được bật rõ bằng cấu hình; không tự fallback khi API lỗi.
public sealed class DemoShippingProvider(IOptions<ShippingOptions> settings) : IShippingProvider
{
    public string Name => "DEMO";
    public Task<ProviderQuote> QuoteAsync(ShippingRoute r, CancellationToken ct)
    {
        var km = (decimal)Retrack.API.Services.Employee.CollectionValidation.Distance((double)r.FromLat,(double)r.FromLng,(double)r.ToLat,(double)r.ToLng) / 1000m;
        // Khoảng cách quy đổi dùng riêng cho demo, không phải tuyến đường giao thông.
        km = decimal.Round(km * 1.3m, 2);
        return Task.FromResult(new ProviderQuote(Guid.NewGuid().ToString(), decimal.Ceiling(250000m + km*18000m), km, DateTime.UtcNow.AddMinutes(5), "{}"));
    }
    public Task<ProviderOrder> BookAsync(ShippingQuote q, ShippingContacts c, Guid op, Guid batch, CancellationToken ct) =>
        Task.FromResult(new ProviderOrder("RT-" + op.ToString("N")[..16].ToUpperInvariant(),"ASSIGNING_DRIVER",null,q.Amount));
    public Task<ProviderOrder> GetAsync(ExternalShipment s, CancellationToken ct)
    {
        var step = (DateTime.UtcNow-s.CreatedAt).TotalSeconds / Math.Clamp(settings.Value.DemoStepSeconds,15,600);
        return Task.FromResult(new ProviderOrder(s.ProviderOrderId!, step < 1 ? "ASSIGNING_DRIVER" : step < 2 ? "ON_GOING" : step < 3 ? "PICKED_UP" : "COMPLETED",null,s.Amount));
    }
    public Task CancelAsync(ExternalShipment s, CancellationToken ct) => Task.CompletedTask;
}
