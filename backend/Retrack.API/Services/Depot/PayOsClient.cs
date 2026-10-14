using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Retrack.API.Services.Depot;

/// <summary>Cấu hình PayOS lấy từ biến môi trường / .env (PayOS__ClientId...). Không ghi key vào code.</summary>
public sealed class PayOsOptions
{
    public string ClientId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ChecksumKey { get; set; } = "";
    public string ReturnUrl { get; set; } = "";
    public string CancelUrl { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api-merchant.payos.vn";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ChecksumKey);
}

public sealed record PayOsLink(string CheckoutUrl, string PaymentLinkId);
public sealed record PayOsPayment(string Status, long Amount, string? Reference, DateTime? PaidAt);
public sealed class PayOsException(string message) : Exception(message);

public interface IPayOsClient
{
    bool IsConfigured { get; }
    Task<PayOsLink> CreatePaymentLinkAsync(long orderCode, long amount, string description, CancellationToken ct = default);
    Task<PayOsPayment> GetPaymentAsync(long orderCode, CancellationToken ct = default);
    bool VerifyWebhookData(JsonElement data, string signature);
}

public sealed class PayOsClient(HttpClient http, IOptions<PayOsOptions> options) : IPayOsClient
{
    private readonly PayOsOptions cfg = options.Value;
    public bool IsConfigured => cfg.IsConfigured;

    public static string Sign(string checksumKey, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static string CreateRequestPayload(long amount, string cancelUrl, string description, long orderCode, string returnUrl) =>
        $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";

    /// <summary>Chuỗi ký webhook: các khóa của "data" sắp xếp A-Z, dạng key=value nối bằng &amp;; null → rỗng.</summary>
    public static string WebhookPayload(JsonElement data)
    {
        var pairs = data.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => p.Name + "=" + p.Value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => "",
            JsonValueKind.String => p.Value.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => p.Value.GetRawText()
        });
        return string.Join("&", pairs);
    }

    public bool VerifyWebhookData(JsonElement data, string signature)
    {
        if (!IsConfigured || data.ValueKind != JsonValueKind.Object || string.IsNullOrEmpty(signature)) return false;
        var expected = Encoding.UTF8.GetBytes(Sign(cfg.ChecksumKey, WebhookPayload(data)));
        return CryptographicOperations.FixedTimeEquals(expected, Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
    }

    public async Task<PayOsLink> CreatePaymentLinkAsync(long orderCode, long amount, string description, CancellationToken ct = default)
    {
        EnsureConfigured();
        var signature = Sign(cfg.ChecksumKey, CreateRequestPayload(amount, cfg.CancelUrl, description, orderCode, cfg.ReturnUrl));
        using var req = Request(HttpMethod.Post, "/v2/payment-requests");
        req.Content = JsonContent.Create(new { orderCode, amount, description, cancelUrl = cfg.CancelUrl, returnUrl = cfg.ReturnUrl, signature });
        using var res = await http.SendAsync(req, ct);
        var root = await ReadAsync(res, ct);
        var data = root.GetProperty("data");
        return new(data.GetProperty("checkoutUrl").GetString()!, data.GetProperty("paymentLinkId").GetString()!);
    }

    public async Task<PayOsPayment> GetPaymentAsync(long orderCode, CancellationToken ct = default)
    {
        EnsureConfigured();
        using var req = Request(HttpMethod.Get, $"/v2/payment-requests/{orderCode.ToString(CultureInfo.InvariantCulture)}");
        using var res = await http.SendAsync(req, ct);
        var data = (await ReadAsync(res, ct)).GetProperty("data");
        string? reference = null; DateTime? paidAt = null;
        if (data.TryGetProperty("transactions", out var tx) && tx.ValueKind == JsonValueKind.Array && tx.GetArrayLength() > 0)
        {
            var first = tx[0];
            if (first.TryGetProperty("reference", out var r) && r.ValueKind == JsonValueKind.String) reference = r.GetString();
            if (first.TryGetProperty("transactionDateTime", out var t) && DateTime.TryParse(t.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt)) paidAt = dt;
        }
        return new(data.GetProperty("status").GetString() ?? "", data.GetProperty("amountPaid").GetInt64(), reference, paidAt);
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, cfg.BaseUrl.TrimEnd('/') + path);
        req.Headers.Add("x-client-id", cfg.ClientId);
        req.Headers.Add("x-api-key", cfg.ApiKey);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return req;
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured) throw new PayOsException("PayOS chưa được cấu hình (PayOS__ClientId, PayOS__ApiKey, PayOS__ChecksumKey).");
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage res, CancellationToken ct)
    {
        var body = await res.Content.ReadAsStringAsync(ct);
        JsonElement root;
        try { root = JsonDocument.Parse(body).RootElement.Clone(); }
        catch (JsonException) { throw new PayOsException($"PayOS trả về phản hồi không hợp lệ (HTTP {(int)res.StatusCode})."); }
        var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
        if (!res.IsSuccessStatusCode || code != "00")
            throw new PayOsException("PayOS từ chối yêu cầu: " + (root.TryGetProperty("desc", out var d) ? d.GetString() : $"HTTP {(int)res.StatusCode}"));
        return root;
    }
}
