using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Retrack.API.Services.Money;

/// <summary>
/// Cấu hình kênh CHI (payout / "Lệnh chi") của payOS, bind từ section "PayOSPayout"
/// (PayOSPayout__ClientId, __ApiKey, __ChecksumKey, __BaseUrl, __TimeoutSeconds, __SignatureMode).
/// Đây là kênh riêng với kênh thu (PayOS__*) và có bộ khóa riêng. Không ghi khóa vào code.
/// </summary>
public sealed class PayOsPayoutOptions
{
    public string ClientId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ChecksumKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api-merchant.payos.vn";
    public int TimeoutSeconds { get; set; } = 15;
    /// <summary>Canonical (mặc định) hoặc RawJson. Xem README-payout-signature.md.</summary>
    public string SignatureMode { get; set; } = nameof(PayOsPayoutSignatureMode.Canonical);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ChecksumKey);

    public PayOsPayoutSignatureMode ParsedSignatureMode =>
        Enum.TryParse<PayOsPayoutSignatureMode>(SignatureMode?.Trim(), ignoreCase: true, out var m)
            ? m : PayOsPayoutSignatureMode.Canonical;
}

/// <summary>Cách tạo chuỗi ký cho header x-signature của POST /v1/payouts.</summary>
public enum PayOsPayoutSignatureMode
{
    /// <summary>Khóa sắp A-Z, key=value mã hóa kiểu encodeURIComponent, nối bằng &amp;.</summary>
    Canonical,
    /// <summary>HMAC trên đúng chuỗi JSON thân request gửi đi (dự phòng).</summary>
    RawJson
}

/// <summary>Kết quả phân loại khi gửi lệnh chi. Unknown nghĩa là KHÔNG biết tiền đã đi hay chưa.</summary>
public enum PayOsPayoutOutcome { Accepted, DefiniteFailure, Unknown }

/// <summary>
/// Kết quả gửi lệnh chi. Accepted chỉ nghĩa là payOS ĐÃ NHẬN lệnh (code "00"), chưa phải tiền đã tới.
/// Unknown: người gọi phải tra cứu theo ReferenceId/idempotency-key, tuyệt đối không gửi lại bằng key mới.
/// </summary>
public sealed record PayOsPayoutResult(
    PayOsPayoutOutcome Outcome,
    string? ProviderRef,
    string? Code,
    string? Desc,
    int? HttpStatus,
    string IdempotencyKey,
    string ReferenceId)
{
    public bool Unknown => Outcome == PayOsPayoutOutcome.Unknown;
    public bool IsAccepted => Outcome == PayOsPayoutOutcome.Accepted;
}

public enum PayOsPayoutState { Processing, Succeeded, Failed, NotFound, Unknown }

/// <summary>Trạng thái lệnh chi khi tra cứu. State = Unknown khi bản thân việc tra cứu lỗi (mạng/5xx/JSON hỏng).</summary>
public sealed record PayOsPayoutStatus(
    PayOsPayoutState State,
    string? PayoutId,
    string? ReferenceId,
    string? TransactionState,
    string? ApprovalState,
    string? Code,
    string? Desc);

public interface IPayOsPayoutClient
{
    bool IsConfigured { get; }
    Task<PayOsPayoutResult> CreatePayoutAsync(string idempotencyKey, string referenceId, long amount, string description,
        string toBin, string toAccountNumber, string? category = null, CancellationToken ct = default);
    Task<PayOsPayoutStatus> GetPayoutAsync(string payoutId, CancellationToken ct = default);
    Task<PayOsPayoutStatus> FindByReferenceAsync(string referenceId, CancellationToken ct = default);
    /// <summary>Số dư tài khoản chi; null khi tra cứu lỗi để người gọi tự quyết định.</summary>
    Task<decimal?> GetBalanceAsync(CancellationToken ct = default);
}

/// <summary>
/// Hàm ký request của kênh chi. CHƯA XÁC MINH với payOS thật (tài liệu payOS không nêu chuỗi ký request).
/// Nếu payOS trả 401/lỗi chữ ký, chỉ cần sửa <see cref="BuildSigningString"/> (hoặc đổi
/// PayOSPayout__SignatureMode). Xem README-payout-signature.md.
/// </summary>
public static class PayOsPayoutSigner
{
    /// <summary>HMAC-SHA256 hex thường.</summary>
    public static string Hmac(string checksumKey, string payload)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    /// <summary>
    /// ĐIỂM THAY ĐỔI DUY NHẤT khi payOS xác nhận cách ký khác. Chuỗi cần ký tùy theo chế độ.
    /// </summary>
    public static string BuildSigningString(PayOsPayoutSignatureMode mode, JsonObject body, string rawJson) =>
        mode == PayOsPayoutSignatureMode.RawJson ? rawJson : CanonicalString(body);

    public static string Sign(PayOsPayoutSignatureMode mode, string checksumKey, JsonObject body, string rawJson) =>
        Hmac(checksumKey, BuildSigningString(mode, body, rawJson));

    /// <summary>
    /// Chuỗi canonical: khóa sắp xếp A-Z (ordinal) ở mọi cấp; mỗi cặp encodeURIComponent(key)=encodeURIComponent(value)
    /// nối bằng "&amp;"; null/thiếu thành rỗng; object/array lồng thành JSON gọn với khóa sắp sâu (thứ tự mảng giữ nguyên).
    /// </summary>
    public static string CanonicalString(JsonObject body)
    {
        var pairs = body.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => EncodeUriComponent(p.Key) + "=" + EncodeUriComponent(ValueText(p.Value)));
        return string.Join("&", pairs);
    }

    private static string ValueText(JsonNode? v)
    {
        if (v is null) return "";
        switch (v.GetValueKind())
        {
            case JsonValueKind.Null or JsonValueKind.Undefined: return "";
            case JsonValueKind.String: return v.GetValue<string>();
            case JsonValueKind.True: return "true";
            case JsonValueKind.False: return "false";
            case JsonValueKind.Number: return v.ToJsonString();
            default: return SortedJson(v);
        }
    }

    private static readonly JsonWriterOptions CompactOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>JSON gọn, khóa sắp sâu, giữ thứ tự mảng, không escape ký tự Unicode (giống JSON.stringify).</summary>
    public static string SortedJson(JsonNode node)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, CompactOptions)) WriteSorted(w, node);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteSorted(Utf8JsonWriter w, JsonNode? n)
    {
        switch (n)
        {
            case null: w.WriteNullValue(); break;
            case JsonObject o:
                w.WriteStartObject();
                foreach (var p in o.OrderBy(p => p.Key, StringComparer.Ordinal)) { w.WritePropertyName(p.Key); WriteSorted(w, p.Value); }
                w.WriteEndObject(); break;
            case JsonArray a:
                w.WriteStartArray();
                foreach (var i in a) WriteSorted(w, i);
                w.WriteEndArray(); break;
            default: n.WriteTo(w); break;
        }
    }

    /// <summary>
    /// Giống encodeURIComponent của JavaScript: giữ nguyên A-Z a-z 0-9 - _ . ! ~ * ' ( ), còn lại mã hóa
    /// từng byte UTF-8 thành %XX (chữ hoa). Khác Uri.EscapeDataString (nó mã hóa ! * ' ( )).
    /// </summary>
    public static string EncodeUriComponent(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (var b in Encoding.UTF8.GetBytes(s))
        {
            var c = (char)b;
            if (b < 0x80 && (char.IsAsciiLetterOrDigit(c) || "-_.!~*'()".IndexOf(c) >= 0)) sb.Append(c);
            else sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
}

public sealed partial class PayOsPayoutClient(HttpClient http, IOptions<PayOsPayoutOptions> options, ILogger<PayOsPayoutClient> log) : IPayOsPayoutClient
{
    public const int MaxDescriptionLength = 20;
    private readonly PayOsPayoutOptions cfg = options.Value;
    public bool IsConfigured => cfg.IsConfigured;

    [GeneratedRegex("FAIL|REJECT|CANCEL|REVERS|ERROR", RegexOptions.IgnoreCase)]
    private static partial Regex FailPattern();

    /// <summary>payOS từ chối description quá 20 ký tự: cắt trong client (không cắt đôi cặp surrogate).</summary>
    public static string TruncateDescription(string? description)
    {
        var d = (description ?? "").Trim();
        if (d.Length <= MaxDescriptionLength) return d;
        var cut = d[..MaxDescriptionLength];
        if (char.IsHighSurrogate(cut[^1])) cut = cut[..^1];
        return cut.TrimEnd();
    }

    /// <summary>Che số tài khoản: chỉ giữ 4 chữ số cuối.</summary>
    public static string MaskAccount(string? account)
    {
        if (string.IsNullOrEmpty(account)) return "****";
        return account.Length <= 4 ? new string('*', account.Length) : new string('*', account.Length - 4) + account[^4..];
    }

    // ---------- Tạo lệnh chi ----------
    public async Task<PayOsPayoutResult> CreatePayoutAsync(string idempotencyKey, string referenceId, long amount, string description,
        string toBin, string toAccountNumber, string? category = null, CancellationToken ct = default)
    {
        PayOsPayoutResult Fail(PayOsPayoutOutcome o, string code, string desc) =>
            new(o, null, code, desc, null, idempotencyKey, referenceId);

        if (!IsConfigured) return Fail(PayOsPayoutOutcome.DefiniteFailure, "NOT_CONFIGURED", "PayOSPayout chưa được cấu hình.");
        if (string.IsNullOrWhiteSpace(idempotencyKey) || string.IsNullOrWhiteSpace(referenceId))
            return Fail(PayOsPayoutOutcome.DefiniteFailure, "INVALID_INPUT", "Thiếu idempotency-key hoặc referenceId.");

        var body = new JsonObject
        {
            ["referenceId"] = referenceId,
            ["amount"] = amount,
            ["description"] = TruncateDescription(description),
            ["toBin"] = toBin,
            ["toAccountNumber"] = toAccountNumber
        };
        if (!string.IsNullOrWhiteSpace(category)) body["category"] = category;
        var raw = body.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        using var req = NewRequest(HttpMethod.Post, "/v1/payouts");
        req.Headers.Add("x-idempotency-key", idempotencyKey);
        req.Headers.Add("x-signature", PayOsPayoutSigner.Sign(cfg.ParsedSignatureMode, cfg.ChecksumKey, body, raw));
        req.Content = new StringContent(raw, Encoding.UTF8, "application/json");

        string text; int status;
        try
        {
            using var res = await http.SendAsync(req, ct);
            status = (int)res.StatusCode;
            text = await res.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or IOException)
        {
            LogUnknown(log, referenceId, MaskAccount(toAccountNumber), ex.GetType().Name);
            return Fail(PayOsPayoutOutcome.Unknown, "NETWORK", ex.GetType().Name);
        }

        var result = Classify(status, text, idempotencyKey, referenceId);
        LogResult(log, referenceId, MaskAccount(toAccountNumber), result.Outcome.ToString(), status, result.Code ?? "");
        return result;
    }

    /// <summary>Phân loại phản hồi tạo lệnh chi (hàm thuần, dễ kiểm thử).</summary>
    public static PayOsPayoutResult Classify(int status, string body, string idempotencyKey, string referenceId)
    {
        PayOsPayoutResult R(PayOsPayoutOutcome o, string? id, string? code, string? desc) =>
            new(o, id, code, desc, status, idempotencyKey, referenceId);

        var root = TryParse(body);
        if (status >= 500 || root is null || root.Value.ValueKind != JsonValueKind.Object)
            return R(PayOsPayoutOutcome.Unknown, null, null, status >= 500 ? $"HTTP {status}" : "Phản hồi không đọc được");

        var code = Str(root.Value, "code");
        var desc = Str(root.Value, "desc");
        string? id = null;
        if (root.Value.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object) id = Str(d, "id");

        if (status is >= 200 and < 300 && code == "00") return R(PayOsPayoutOutcome.Accepted, id, code, desc);
        if (status >= 400 || !string.IsNullOrEmpty(code)) return R(PayOsPayoutOutcome.DefiniteFailure, id, code, desc);
        return R(PayOsPayoutOutcome.Unknown, id, code, desc); // 2xx/3xx nhưng không có code: không chắc chắn
    }

    // ---------- Tra cứu ----------
    public async Task<PayOsPayoutStatus> GetPayoutAsync(string payoutId, CancellationToken ct = default)
    {
        var (status, root) = await GetJsonAsync("/v1/payouts/" + Uri.EscapeDataString(payoutId), ct);
        if (root is null || status is null || status >= 500) return UnknownStatus(status);
        var code = Str(root.Value, "code"); var desc = Str(root.Value, "desc");
        if (status == 404) return new(PayOsPayoutState.NotFound, null, null, null, null, code, desc);
        if (status is < 200 or >= 300 || code != "00") return new(PayOsPayoutState.Unknown, null, null, null, null, code, desc);
        if (!root.Value.TryGetProperty("data", out var d) || d.ValueKind != JsonValueKind.Object)
            return new(PayOsPayoutState.NotFound, null, null, null, null, code, desc);
        return MapPayout(d, code, desc);
    }

    public async Task<PayOsPayoutStatus> FindByReferenceAsync(string referenceId, CancellationToken ct = default)
    {
        var (status, root) = await GetJsonAsync("/v1/payouts?referenceId=" + Uri.EscapeDataString(referenceId) + "&limit=10", ct);
        if (root is null || status is null || status >= 500) return UnknownStatus(status);
        var code = Str(root.Value, "code"); var desc = Str(root.Value, "desc");
        if (status is < 200 or >= 300 || code != "00") return new(PayOsPayoutState.Unknown, null, null, null, null, code, desc);

        JsonElement? list = null;
        if (root.Value.TryGetProperty("data", out var d))
        {
            if (d.ValueKind == JsonValueKind.Array) list = d;
            else if (d.ValueKind == JsonValueKind.Object && d.TryGetProperty("payouts", out var p) && p.ValueKind == JsonValueKind.Array) list = p;
            else if (d.ValueKind == JsonValueKind.Null) list = null;
            else if (d.ValueKind == JsonValueKind.Object) return Unrecognised(code, desc);
        }
        if (list is null || list.Value.GetArrayLength() == 0) return new(PayOsPayoutState.NotFound, null, null, null, null, code, desc);

        // Nhiều lệnh cùng referenceId: ưu tiên Succeeded > Processing > Failed để không kết luận sai là đã thất bại.
        PayOsPayoutStatus? best = null;
        foreach (var item in list.Value.EnumerateArray())
        {
            var s = MapPayout(item, code, desc);
            if (best is null || Rank(s.State) > Rank(best.State)) best = s;
        }
        return best!;
    }

    private static PayOsPayoutStatus Unrecognised(string? code, string? desc) => new(PayOsPayoutState.Unknown, null, null, null, null, code, desc);
    private static int Rank(PayOsPayoutState s) => s switch { PayOsPayoutState.Succeeded => 3, PayOsPayoutState.Processing => 2, PayOsPayoutState.Failed => 1, _ => 0 };
    private static PayOsPayoutStatus UnknownStatus(int? status) =>
        new(PayOsPayoutState.Unknown, null, null, null, null, null, status is null ? "Lỗi mạng/timeout/JSON" : $"HTTP {status}");

    /// <summary>Ánh xạ trạng thái: FAIL|REJECT|CANCEL|REVERS|ERROR => Failed; mọi transaction SUCCEED* => Succeeded; còn lại Processing.</summary>
    public static PayOsPayoutState MapState(IReadOnlyCollection<string> transactionStates, string? approvalState)
    {
        if (transactionStates.Any(s => FailPattern().IsMatch(s)) || (approvalState is not null && FailPattern().IsMatch(approvalState)))
            return PayOsPayoutState.Failed;
        if (transactionStates.Count > 0 && transactionStates.All(s => s.StartsWith("SUCCEED", StringComparison.OrdinalIgnoreCase)))
            return PayOsPayoutState.Succeeded;
        return PayOsPayoutState.Processing;
    }

    private static PayOsPayoutStatus MapPayout(JsonElement d, string? code, string? desc)
    {
        var states = new List<string>();
        if (d.TryGetProperty("transactions", out var tx) && tx.ValueKind == JsonValueKind.Array)
            foreach (var t in tx.EnumerateArray()) { var s = t.ValueKind == JsonValueKind.Object ? Str(t, "state") : null; if (!string.IsNullOrEmpty(s)) states.Add(s); }
        var own = d.ValueKind == JsonValueKind.Object ? Str(d, "state") : null;
        if (!string.IsNullOrEmpty(own)) states.Add(own);
        var approval = Str(d, "approvalState");
        return new(MapState(states, approval), Str(d, "id"), Str(d, "referenceId"),
            states.Count > 0 ? string.Join(",", states) : null, approval, code, desc);
    }

    // ---------- Số dư ----------
    public async Task<decimal?> GetBalanceAsync(CancellationToken ct = default)
    {
        var (status, root) = await GetJsonAsync("/v1/payouts-account/balance", ct);
        if (root is null || status is not (>= 200 and < 300) || Str(root.Value, "code") != "00") return null;
        if (!root.Value.TryGetProperty("data", out var d) || d.ValueKind != JsonValueKind.Object || !d.TryGetProperty("balance", out var b)) return null;
        var text = b.ValueKind == JsonValueKind.Number ? b.GetRawText() : b.GetString();
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    // ---------- tiện ích ----------
    private async Task<(int? Status, JsonElement? Root)> GetJsonAsync(string path, CancellationToken ct)
    {
        if (!IsConfigured) return (null, null);
        try
        {
            using var req = NewRequest(HttpMethod.Get, path);
            using var res = await http.SendAsync(req, ct);
            var text = await res.Content.ReadAsStringAsync(ct);
            return ((int)res.StatusCode, TryParse(text));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or IOException)
        {
            LogLookupFailed(log, ex.GetType().Name);
            return (null, null);
        }
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, cfg.BaseUrl.TrimEnd('/') + path);
        req.Headers.Add("x-client-id", cfg.ClientId);
        req.Headers.Add("x-api-key", cfg.ApiKey);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return req;
    }

    private static JsonElement? TryParse(string body)
    {
        try { using var doc = JsonDocument.Parse(body); return doc.RootElement.Clone(); }
        catch (JsonException) { return null; }
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
            ? v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.Number => v.GetRawText(), _ => null }
            : null;

    // Log chỉ chứa referenceId và số tài khoản đã che (4 số cuối); không bao giờ ghi khóa/chữ ký.
    [LoggerMessage(Level = LogLevel.Information, Message = "payOS payout ref={Ref} acct={Acct} outcome={Outcome} http={Http} code={Code}")]
    private static partial void LogResult(ILogger l, string @ref, string acct, string outcome, int http, string code);

    [LoggerMessage(Level = LogLevel.Warning, Message = "payOS payout ref={Ref} acct={Acct} KẾT QUẢ KHÔNG RÕ ({Error}); phải tra cứu, không gửi lại bằng key mới")]
    private static partial void LogUnknown(ILogger l, string @ref, string acct, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "payOS payout tra cứu lỗi ({Error})")]
    private static partial void LogLookupFailed(ILogger l, string error);
}
