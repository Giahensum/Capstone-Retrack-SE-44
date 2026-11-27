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
    /// <summary>URL quay về cho Factory; để trống thì suy ra từ ReturnUrl (cùng domain, đường dẫn /factory).</summary>
    public string FactoryReturnUrl { get; set; } = "";
    public string FactoryCancelUrl { get; set; } = "";
    /// <summary>URL quay về cho khoản thu qua sàn (/pay/return); để trống thì suy ra từ ReturnUrl (cùng domain).</summary>
    public string MoneyReturnUrl { get; set; } = "";
    public (string Return, string Cancel) Redirects(PayOsRedirect target, string? query = null)
    {
        if (target == PayOsRedirect.Subscription)
        {
            if (!Uri.TryCreate(ReturnUrl, UriKind.Absolute, out var subscriptionBase)) throw new PayOsException("Chưa cấu hình PayOS ReturnUrl cho website.");
            var url = subscriptionBase.GetLeftPart(UriPartial.Authority) + "/depot/subscription?payos=return";
            return (url, url.Replace("payos=return", "payos=cancel"));
        }
        if (target == PayOsRedirect.Money)
        {
            // Trang /pay/return nhận flow + source để biết khoản thu nào cần đồng bộ; PayOS tự nối thêm code, id, cancel, status, orderCode.
            var baseUrl = !string.IsNullOrWhiteSpace(MoneyReturnUrl) ? MoneyReturnUrl.TrimEnd('/')
                : Uri.TryCreate(ReturnUrl, UriKind.Absolute, out var m) ? $"{m.GetLeftPart(UriPartial.Authority)}/pay/return" : "/pay/return";
            var url = string.IsNullOrWhiteSpace(query) ? baseUrl : baseUrl + (baseUrl.Contains('?') ? "&" : "?") + query;
            return (url, url);
        }
        if (target != PayOsRedirect.Factory) return (ReturnUrl, CancelUrl);
        string Derive(string explicitUrl, string fallback, string status) =>
            !string.IsNullOrWhiteSpace(explicitUrl) ? explicitUrl
            : Uri.TryCreate(fallback, UriKind.Absolute, out var u) ? $"{u.GetLeftPart(UriPartial.Authority)}/factory?payos={status}" : fallback;
        return (Derive(FactoryReturnUrl, ReturnUrl, "return"), Derive(FactoryCancelUrl, CancelUrl, "cancel"));
    }
    public int PollSeconds { get; set; } = 15;
    public string BaseUrl { get; set; } = "https://api-merchant.payos.vn";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ChecksumKey);
}

public sealed record PayOsLink(string CheckoutUrl, string PaymentLinkId);
public sealed record PayOsPayment(string Status, long Amount, string? Reference, DateTime? PaidAt, string? LinkId = null);
public sealed class PayOsException(string message) : Exception(message);

/// <summary>Trang web mà PayOS đưa người nộp quay lại sau khi thanh toán hoặc hủy.</summary>
public enum PayOsRedirect { Depot, Factory, Money, Subscription }

public interface IPayOsClient
{
    bool IsConfigured { get; }
    Task<PayOsLink> CreatePaymentLinkAsync(long orderCode, long amount, string description, CancellationToken ct = default);
    /// <summary>Mặc định dùng URL của Depot để các bản cài đặt cũ (ví dụ giả lập trong test) vẫn hoạt động.</summary>
    Task<PayOsLink> CreatePaymentLinkAsync(long orderCode, long amount, string description, PayOsRedirect redirect, CancellationToken ct = default) =>
        CreatePaymentLinkAsync(orderCode, amount, description, ct);
    /// <summary>Tạo link cho khoản thu qua sàn: redirectQuery (flow=...&amp;source=...) được nối vào URL quay về. Mặc định bỏ qua query để các bản cài đặt giả lập trong test vẫn hoạt động.</summary>
    Task<PayOsLink> CreatePaymentLinkAsync(long orderCode, long amount, string description, PayOsRedirect redirect, string? redirectQuery, CancellationToken ct = default) =>
        CreatePaymentLinkAsync(orderCode, amount, description, redirect, ct);
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

    public Task<PayOsLink> CreatePaymentLinkAsync(long orderCode, long amount, string description, CancellationToken ct = default) =>
        CreatePaymentLinkAsync(orderCode, amount, description, PayOsRedirect.Depot, ct);

    public Task<PayOsLink> CreatePaymentLinkAsync(long orderCode, long amount, string description, PayOsRedirect redirect, CancellationToken ct = default) =>
        CreatePaymentLinkAsync(orderCode, amount, description, redirect, null, ct);

    public async Task<PayOsLink> CreatePaymentLinkAsync(long orderCode, long amount, string description, PayOsRedirect redirect, string? redirectQuery, CancellationToken ct = default)
    {
        EnsureConfigured();
        var (returnUrl, cancelUrl) = cfg.Redirects(redirect, redirectQuery);
        var signature = Sign(cfg.ChecksumKey, CreateRequestPayload(amount, cancelUrl, description, orderCode, returnUrl));
        using var req = Request(HttpMethod.Post, "/v2/payment-requests");
        req.Content = JsonContent.Create(new { orderCode, amount, description, cancelUrl, returnUrl, signature });
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
        return new(data.GetProperty("status").GetString() ?? "", data.GetProperty("amountPaid").GetInt64(), reference, paidAt,
            data.TryGetProperty("id", out var linkId) && linkId.ValueKind == JsonValueKind.String ? linkId.GetString() : null);
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
