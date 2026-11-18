using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Retrack.API.Services.ESign;

/// <summary>Lỗi khi gọi DocuSign; thông điệp đã được lọc, an toàn để trả về cho người dùng (middleware đổi thành 502).</summary>
public sealed class DocuSignException(string message) : Exception(message);

/// <summary>Một người ký trong envelope. <c>ClientUserId</c> bắt buộc có để ký nhúng ngay trong hệ thống (không gửi email).</summary>
public sealed record EnvelopeSigner(string RecipientId, string Email, string Name, string ClientUserId, string AnchorString);

public sealed record EnvelopeSignerState(string RecipientId, string Status, DateTime? SignedAt, string? DeclinedReason);

public sealed record EnvelopeState(string Status, DateTime? CompletedAt, IReadOnlyList<EnvelopeSignerState> Signers);

public interface IDocuSignClient
{
    bool IsConfigured { get; }
    /// <summary>Tạo và gửi envelope từ tài liệu HTML; trả về envelopeId.</summary>
    Task<string> CreateEnvelopeAsync(string subject, string documentName, string html, IReadOnlyList<EnvelopeSigner> signers, CancellationToken ct);
    /// <summary>Địa chỉ trang ký nhúng của một người ký (dùng một lần, hết hạn rất nhanh).</summary>
    Task<string> CreateSigningUrlAsync(string envelopeId, EnvelopeSigner signer, string returnUrl, CancellationToken ct);
    Task<EnvelopeState> GetEnvelopeAsync(string envelopeId, CancellationToken ct);
    /// <summary>Bản PDF gộp (kèm các chữ ký) của envelope.</summary>
    Task<byte[]> DownloadCombinedAsync(string envelopeId, CancellationToken ct);
}

/// <summary>
/// Gọi REST API của DocuSign eSignature trực tiếp bằng HttpClient (không thêm gói NuGet). Đăng nhập kiểu JWT Grant (máy chủ tự đăng nhập,
/// cần cấp quyền "impersonation" một lần). Token được dùng chung giữa các request cho tới gần hết hạn.
/// </summary>
public sealed class DocuSignClient(HttpClient http, IOptions<DocuSignOptions> options, ILogger<DocuSignClient> logger) : IDocuSignClient
{
    private readonly DocuSignOptions cfg = options.Value;
    private static readonly SemaphoreSlim TokenGate = new(1, 1);
    private static string? cachedToken;
    private static DateTimeOffset cachedExpiry = DateTimeOffset.MinValue;

    public bool IsConfigured => cfg.IsConfigured;

    private string ApiBase => $"{cfg.BaseUri.TrimEnd('/')}/restapi/v2.1/accounts/{cfg.AccountId}";

    private void EnsureConfigured()
    {
        if (!cfg.IsConfigured)
            throw new DocuSignException("Chưa cấu hình DocuSign (DocuSign__IntegrationKey, UserId, AccountId, PrivateKeyBase64).");
    }

    // ------------------------------------------------------------------ đăng nhập JWT Grant
    private string BuildAssertion(RSA rsa)
    {
        var now = DateTimeOffset.UtcNow;
        var header = JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" });
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = cfg.IntegrationKey,
            sub = cfg.UserId,
            aud = cfg.OAuthHost,
            iat = now.ToUnixTimeSeconds(),
            exp = now.AddMinutes(50).ToUnixTimeSeconds(),
            scope = "signature impersonation"
        });
        var signingInput = Base64UrlEncoder.Encode(header) + "." + Base64UrlEncoder.Encode(payload);
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return signingInput + "." + Base64UrlEncoder.Encode(signature);
    }

    private async Task<string> AccessTokenAsync(CancellationToken ct)
    {
        if (cachedToken is not null && DateTimeOffset.UtcNow < cachedExpiry) return cachedToken;
        await TokenGate.WaitAsync(ct);
        try
        {
            if (cachedToken is not null && DateTimeOffset.UtcNow < cachedExpiry) return cachedToken;
            using var rsa = RSA.Create();
            try { rsa.ImportFromPem(cfg.ReadPrivateKeyPem()); }
            catch (CryptographicException) { throw new DocuSignException("Khóa riêng RSA của DocuSign không đọc được (cần dạng PEM, mã hóa base64)."); }

            using var req = new HttpRequestMessage(HttpMethod.Post, $"https://{cfg.OAuthHost}/oauth/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                    ["assertion"] = BuildAssertion(rsa)
                })
            };
            using var res = await http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                logger.LogWarning("DocuSign OAuth lỗi {Status}: {Body}", (int)res.StatusCode, Trim(body));
                throw new DocuSignException(body.Contains("consent_required", StringComparison.OrdinalIgnoreCase)
                    ? "Ứng dụng DocuSign chưa được cấp quyền (consent). Hãy cấp quyền một lần rồi thử lại."
                    : "Không đăng nhập được DocuSign. Kiểm tra Integration Key, User ID và khóa RSA.");
            }
            var json = JsonNode.Parse(body) ?? throw new DocuSignException("DocuSign trả về dữ liệu đăng nhập không hợp lệ.");
            var token = Text(json["access_token"]);
            if (string.IsNullOrEmpty(token)) throw new DocuSignException("DocuSign không trả về access token.");
            var ttl = json["expires_in"] is JsonValue v && v.TryGetValue<int>(out var seconds) ? seconds : 3600;
            cachedToken = token;
            cachedExpiry = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, ttl - 120));
            return token;
        }
        finally { TokenGate.Release(); }
    }

    // ------------------------------------------------------------------ gọi API
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, JsonNode? body, string accept, CancellationToken ct)
    {
        EnsureConfigured();
        using var req = new HttpRequestMessage(method, ApiBase + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await AccessTokenAsync(ct));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        if (body is not null) req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        return await http.SendAsync(req, ct);
    }

    private async Task<JsonNode> JsonAsync(HttpMethod method, string path, JsonNode? body, string action, CancellationToken ct)
    {
        using var res = await SendAsync(method, path, body, "application/json", ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw Failure(res, text, action);
        return JsonNode.Parse(text) ?? throw new DocuSignException($"DocuSign trả về dữ liệu rỗng khi {action}.");
    }

    private DocuSignException Failure(HttpResponseMessage res, string body, string action)
    {
        logger.LogWarning("DocuSign lỗi khi {Action}: {Status} {Body}", action, (int)res.StatusCode, Trim(body));
        string? message = null;
        try { message = Text(JsonNode.Parse(body)?["message"]); } catch (JsonException) { /* thân phản hồi không phải JSON */ }
        return new DocuSignException($"DocuSign từ chối khi {action} (mã {(int)res.StatusCode}){(string.IsNullOrWhiteSpace(message) ? "" : ": " + Trim(message, 200))}.");
    }

    public async Task<string> CreateEnvelopeAsync(string subject, string documentName, string html, IReadOnlyList<EnvelopeSigner> signers, CancellationToken ct)
    {
        var signerNodes = new JsonArray();
        foreach (var s in signers)
        {
            signerNodes.Add(new JsonObject
            {
                ["email"] = s.Email,
                ["name"] = s.Name,
                ["recipientId"] = s.RecipientId,
                ["routingOrder"] = "1", // hai bên cùng được ký, không bắt buộc theo thứ tự
                ["clientUserId"] = s.ClientUserId,
                ["tabs"] = new JsonObject
                {
                    ["signHereTabs"] = new JsonArray(new JsonObject
                    {
                        ["anchorString"] = s.AnchorString,
                        ["anchorUnits"] = "pixels",
                        ["anchorXOffset"] = "0",
                        ["anchorYOffset"] = "0"
                    })
                }
            });
        }
        var body = new JsonObject
        {
            ["emailSubject"] = subject,
            ["status"] = "sent",
            ["documents"] = new JsonArray(new JsonObject
            {
                ["documentId"] = "1",
                ["name"] = documentName,
                ["htmlDefinition"] = new JsonObject { ["source"] = html }
            }),
            ["recipients"] = new JsonObject { ["signers"] = signerNodes }
        };
        if (!string.IsNullOrWhiteSpace(cfg.WebhookUrl))
        {
            body["eventNotification"] = new JsonObject
            {
                ["url"] = cfg.WebhookUrl,
                ["loggingEnabled"] = "true",
                ["requireAcknowledgment"] = "true",
                ["envelopeEvents"] = new JsonArray(Event("completed"), Event("declined"), Event("voided"))
            };
        }
        var created = await JsonAsync(HttpMethod.Post, "/envelopes", body, "tạo hợp đồng", ct);
        var id = Text(created["envelopeId"]);
        return string.IsNullOrEmpty(id) ? throw new DocuSignException("DocuSign không trả về mã envelope.") : id;
    }

    public async Task<string> CreateSigningUrlAsync(string envelopeId, EnvelopeSigner signer, string returnUrl, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["returnUrl"] = returnUrl,
            ["authenticationMethod"] = "none",
            ["email"] = signer.Email,
            ["userName"] = signer.Name,
            ["clientUserId"] = signer.ClientUserId
        };
        var view = await JsonAsync(HttpMethod.Post, $"/envelopes/{Uri.EscapeDataString(envelopeId)}/views/recipient", body, "mở trang ký", ct);
        var url = Text(view["url"]);
        return string.IsNullOrEmpty(url) ? throw new DocuSignException("DocuSign không trả về địa chỉ trang ký.") : url;
    }

    public async Task<EnvelopeState> GetEnvelopeAsync(string envelopeId, CancellationToken ct)
    {
        var node = await JsonAsync(HttpMethod.Get, $"/envelopes/{Uri.EscapeDataString(envelopeId)}?include=recipients", null, "đọc trạng thái hợp đồng", ct);
        var signers = new List<EnvelopeSignerState>();
        if (node["recipients"]?["signers"] is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is null) continue;
                signers.Add(new EnvelopeSignerState(
                    Text(item["recipientId"]) ?? "",
                    (Text(item["status"]) ?? "").ToLowerInvariant(),
                    Date(Text(item["signedDateTime"])),
                    Text(item["declinedReason"])));
            }
        }
        return new EnvelopeState((Text(node["status"]) ?? "").ToLowerInvariant(), Date(Text(node["completedDateTime"])), signers);
    }

    public async Task<byte[]> DownloadCombinedAsync(string envelopeId, CancellationToken ct)
    {
        using var res = await SendAsync(HttpMethod.Get, $"/envelopes/{Uri.EscapeDataString(envelopeId)}/documents/combined", null, "application/pdf", ct);
        if (!res.IsSuccessStatusCode) throw Failure(res, await res.Content.ReadAsStringAsync(ct), "tải hợp đồng đã ký");
        return await res.Content.ReadAsByteArrayAsync(ct);
    }

    // ------------------------------------------------------------------ tiện ích
    private static JsonObject Event(string status) => new() { ["envelopeEventStatusCode"] = status };

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var s) ? s : node?.ToString();

    private static DateTime? Date(string? raw) =>
        DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    private static string Trim(string text, int max = 400) => text.Length <= max ? text : text[..max];
}
