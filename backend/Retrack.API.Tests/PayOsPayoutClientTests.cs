using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Retrack.API.Services.Money;
using Xunit;

namespace Retrack.API.Tests;

/// <summary>Kiểm thử client payOS kênh chi: chữ ký, phân loại kết quả, ánh xạ trạng thái. Không cần mạng hay DB.</summary>
public sealed class PayOsPayoutClientTests
{
    private const string Key = "payout-test-checksum-key";

    // ---------- chữ ký: vector tính độc lập bằng Python (hmac + urllib.parse.quote(safe="-_.!~*'()")) ----------
    private const string CanonA = "amount=150000&description=Chi%20tra%20don%20RT-0001&referenceId=RT-PO-0001&toAccountNumber=0123456789&toBin=970422";
    private const string SigA = "54075e626aa5e9fd9075905fd74cabd4871db24bafe6e5ed2891878ef7c16ca7";
    private const string CanonB = "amount=5000&category=&description=Thanh%20to%C3%A1n%20%C4%91%C6%A1n%20h%C3%A0ng%20(A*B)%20!~'&referenceId=RT-2&toAccountNumber=9704&toBin=970436";
    private const string SigB = "18ef052db7a28982836ac09004468507597aab7ccc38c00e3066facebbca5aa1";
    private const string CanonC = "a=1&meta=%7B%22a%22%3A%5B3%2C1%5D%2C%22b%22%3A%7B%22x%22%3A1%2C%22y%22%3A2%7D%7D";
    private const string SigC = "f410c00d61fa31f09edaefefb9fb9d8f5dfe282a40c10b98eef7e1deea22ddb4";
    private const string RawD = "{\"referenceId\":\"RT-PO-0001\",\"amount\":150000}";
    private const string SigD = "0e4d5ca8956bdf7fc4f0f49cbc379068f6df0fafcd2a55256e14f177b16ea43f";

    private static JsonObject Obj(string json) => JsonNode.Parse(json)!.AsObject();

    [Fact]
    public void Canonical_string_matches_vector_A_and_hmac()
    {
        var body = Obj("""{"referenceId":"RT-PO-0001","amount":150000,"description":"Chi tra don RT-0001","toBin":"970422","toAccountNumber":"0123456789"}""");
        Assert.Equal(CanonA, PayOsPayoutSigner.CanonicalString(body));
        Assert.Equal(SigA, PayOsPayoutSigner.Sign(PayOsPayoutSignatureMode.Canonical, Key, body, ""));
    }

    [Fact]
    public void Canonical_handles_vietnamese_special_chars_and_null()
    {
        var body = Obj("""{"referenceId":"RT-2","amount":5000,"description":"Thanh toán đơn hàng (A*B) !~'","toBin":"970436","toAccountNumber":"9704","category":null}""");
        Assert.Equal(CanonB, PayOsPayoutSigner.CanonicalString(body));
        Assert.Equal(SigB, PayOsPayoutSigner.Sign(PayOsPayoutSignatureMode.Canonical, Key, body, ""));
    }

    [Fact]
    public void Canonical_nested_objects_become_deep_sorted_compact_json_keeping_array_order()
    {
        var body = Obj("""{"meta":{"b":{"y":2,"x":1},"a":[3,1]},"a":1}""");
        Assert.Equal(CanonC, PayOsPayoutSigner.CanonicalString(body));
        Assert.Equal(SigC, PayOsPayoutSigner.Sign(PayOsPayoutSignatureMode.Canonical, Key, body, ""));
    }

    [Fact]
    public void Canonical_js_example_sorts_keys_and_empties_null()
    {
        // {b:'x y',a:1,c:null,d:undefined}: undefined không có trong JSON nên được tạo là null (rỗng) tương đương.
        var body = Obj("""{"b":"x y","a":1,"c":null,"d":null}""");
        Assert.Equal("a=1&b=x%20y&c=&d=", PayOsPayoutSigner.CanonicalString(body));
    }

    [Fact]
    public void RawJson_mode_signs_the_raw_body()
    {
        Assert.Equal(SigD, PayOsPayoutSigner.Sign(PayOsPayoutSignatureMode.RawJson, Key, new JsonObject(), RawD));
    }

    [Theory]
    [InlineData("abcXYZ019-_.!~*'()", "abcXYZ019-_.!~*'()")]
    [InlineData("a b", "a%20b")]
    [InlineData("a&b=c/d?e+f", "a%26b%3Dc%2Fd%3Fe%2Bf")]
    [InlineData("đơn", "%C4%91%C6%A1n")]
    [InlineData("Việt Nam", "Vi%E1%BB%87t%20Nam")]
    [InlineData("😀", "%F0%9F%98%80")]
    public void EncodeUriComponent_matches_javascript(string input, string expected) =>
        Assert.Equal(expected, PayOsPayoutSigner.EncodeUriComponent(input));

    [Fact]
    public void EncodeUriComponent_differs_from_EscapeDataString_on_reserved_marks() =>
        Assert.NotEqual(Uri.EscapeDataString("!*'()"), PayOsPayoutSigner.EncodeUriComponent("!*'()"));

    [Fact]
    public void Options_parse_mode_and_configured()
    {
        Assert.False(new PayOsPayoutOptions { ClientId = "a", ApiKey = "b" }.IsConfigured);
        Assert.True(new PayOsPayoutOptions { ClientId = "a", ApiKey = "b", ChecksumKey = "c" }.IsConfigured);
        Assert.Equal(PayOsPayoutSignatureMode.Canonical, new PayOsPayoutOptions().ParsedSignatureMode);
        Assert.Equal(PayOsPayoutSignatureMode.RawJson, new PayOsPayoutOptions { SignatureMode = "rawjson" }.ParsedSignatureMode);
        Assert.Equal(PayOsPayoutSignatureMode.Canonical, new PayOsPayoutOptions { SignatureMode = "nonsense" }.ParsedSignatureMode);
    }

    // ---------- description / masking ----------
    [Fact]
    public void Description_is_truncated_to_20_chars()
    {
        Assert.Equal("Thanh toan don hang ", PayOsPayoutClient.TruncateDescription("Thanh toan don hang so 123456789").PadRight(20));
        Assert.Equal("ThanhToanDonHangSo123", "ThanhToanDonHangSo1234567"[..21]);
        Assert.Equal(20, PayOsPayoutClient.TruncateDescription("ThanhToanDonHangSo1234567").Length);
        Assert.Equal("ngan", PayOsPayoutClient.TruncateDescription("  ngan "));
        Assert.Equal("", PayOsPayoutClient.TruncateDescription(null));
    }

    [Theory]
    [InlineData("0123456789", "******6789")]
    [InlineData("1234", "****")]
    [InlineData("", "****")]
    public void Account_is_masked_to_last_4(string acct, string expected) => Assert.Equal(expected, PayOsPayoutClient.MaskAccount(acct));

    // ---------- gửi lệnh chi: header, body, phân loại ----------
    [Fact]
    public async Task Create_sends_required_headers_and_truncated_body()
    {
        var h = new FakeHandler(HttpStatusCode.OK, """{"code":"00","desc":"success","data":{"id":"po_123"}}""");
        var r = await NewClient(h).CreatePayoutAsync("idem-1", "RT-PO-0001", 150000, "Thanh toan don hang so 123456789", "970422", "0123456789", "salary");
        Assert.Equal(PayOsPayoutOutcome.Accepted, r.Outcome);
        Assert.Equal("po_123", r.ProviderRef);
        Assert.Equal("idem-1", r.IdempotencyKey);
        Assert.Equal("RT-PO-0001", r.ReferenceId);
        Assert.False(r.Unknown);

        var req = Assert.Single(h.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("https://payos.test/v1/payouts", req.Uri);
        Assert.Equal("cid", req.Header("x-client-id"));
        Assert.Equal("akey", req.Header("x-api-key"));
        Assert.Equal("idem-1", req.Header("x-idempotency-key"));
        var body = JsonNode.Parse(req.Body!)!.AsObject();
        Assert.Equal(PayOsPayoutSigner.Sign(PayOsPayoutSignatureMode.Canonical, Key, body, req.Body!), req.Header("x-signature"));
        Assert.True(body["description"]!.GetValue<string>().Length <= 20);
        Assert.Equal(150000, body["amount"]!.GetValue<long>());
        Assert.Equal("970422", body["toBin"]!.GetValue<string>());
        Assert.Equal("salary", body["category"]!.GetValue<string>());
    }

    [Fact]
    public async Task Create_uses_rawjson_mode_when_configured()
    {
        var h = new FakeHandler(HttpStatusCode.OK, """{"code":"00"}""");
        await NewClient(h, mode: "RawJson").CreatePayoutAsync("k", "r", 1000, "d", "970422", "123456789");
        var req = h.Requests[0];
        Assert.Equal(PayOsPayoutSigner.Hmac(Key, req.Body!), req.Header("x-signature"));
        Assert.False(JsonNode.Parse(req.Body!)!.AsObject().ContainsKey("category"));
    }

    [Theory]
    [InlineData(200, """{"code":"00","data":{"id":"p1"}}""", PayOsPayoutOutcome.Accepted)]
    [InlineData(200, """{"code":"231","desc":"bad bin"}""", PayOsPayoutOutcome.DefiniteFailure)]
    [InlineData(400, """{"code":"20","desc":"invalid"}""", PayOsPayoutOutcome.DefiniteFailure)]
    [InlineData(401, """{"code":"401","desc":"unauthorized"}""", PayOsPayoutOutcome.DefiniteFailure)]
    [InlineData(401, "plain text", PayOsPayoutOutcome.Unknown)]
    [InlineData(500, """{"code":"500","desc":"oops"}""", PayOsPayoutOutcome.Unknown)]
    [InlineData(503, "", PayOsPayoutOutcome.Unknown)]
    [InlineData(200, "not json {", PayOsPayoutOutcome.Unknown)]
    [InlineData(200, "[1,2]", PayOsPayoutOutcome.Unknown)]
    public async Task Create_classifies_responses(int status, string body, PayOsPayoutOutcome expected)
    {
        var r = await NewClient(new FakeHandler((HttpStatusCode)status, body)).CreatePayoutAsync("k", "r", 1000, "d", "970422", "123456789");
        Assert.Equal(expected, r.Outcome);
        Assert.Equal(expected == PayOsPayoutOutcome.Unknown, r.Unknown);
        Assert.Equal("k", r.IdempotencyKey);
    }

    [Fact]
    public async Task Create_failure_carries_code_and_desc()
    {
        var r = await NewClient(new FakeHandler(HttpStatusCode.OK, """{"code":"231","desc":"bad bin"}""")).CreatePayoutAsync("k", "r", 1000, "d", "970422", "123456789");
        Assert.Equal("231", r.Code); Assert.Equal("bad bin", r.Desc); Assert.Null(r.ProviderRef);
    }

    [Fact]
    public async Task Create_timeout_and_network_errors_are_unknown_not_thrown()
    {
        var t = await NewClient(new FakeHandler(new TaskCanceledException("timeout"))).CreatePayoutAsync("k", "r", 1000, "d", "970422", "123456789");
        Assert.Equal(PayOsPayoutOutcome.Unknown, t.Outcome);
        var n = await NewClient(new FakeHandler(new HttpRequestException("down"))).CreatePayoutAsync("k", "r", 1000, "d", "970422", "123456789");
        Assert.True(n.Unknown);
    }

    [Fact]
    public async Task Create_not_configured_is_definite_failure_and_sends_nothing()
    {
        var h = new FakeHandler(HttpStatusCode.OK, "{}");
        var r = await NewClient(h, configured: false).CreatePayoutAsync("k", "r", 1000, "d", "970422", "123456789");
        Assert.Equal(PayOsPayoutOutcome.DefiniteFailure, r.Outcome);
        Assert.Empty(h.Requests);
    }

    [Fact]
    public async Task Logs_never_contain_full_account_number_or_keys()
    {
        var log = new ListLogger();
        var acct = "0123456789";
        await NewClient(new FakeHandler(HttpStatusCode.OK, """{"code":"00"}"""), log: log).CreatePayoutAsync("k", "r", 1000, "d", "970422", acct);
        await NewClient(new FakeHandler(new TaskCanceledException()), log: log).CreatePayoutAsync("k2", "r2", 1000, "d", "970422", acct);
        Assert.NotEmpty(log.Messages);
        Assert.All(log.Messages, m =>
        {
            Assert.DoesNotContain(acct, m);
            Assert.DoesNotContain(Key, m);
            Assert.DoesNotContain("akey", m);
        });
        Assert.Contains(log.Messages, m => m.Contains("******6789"));
    }

    // ---------- ánh xạ trạng thái ----------
    [Theory]
    [InlineData("PROCESSING", "PROCESSING", PayOsPayoutState.Processing)]
    [InlineData("SUCCEEDED", "COMPLETED", PayOsPayoutState.Succeeded)]
    [InlineData("SUCCEEDED", "SUCCEEDED", PayOsPayoutState.Succeeded)]
    [InlineData("SUCCEEDED", "PROCESSING", PayOsPayoutState.Succeeded)]
    [InlineData("FAILED", "SUCCEEDED", PayOsPayoutState.Failed)]
    [InlineData("SUCCEEDED", "REJECTED", PayOsPayoutState.Failed)]
    [InlineData("CANCELLED", null, PayOsPayoutState.Failed)]
    [InlineData("REVERSED", null, PayOsPayoutState.Failed)]
    [InlineData("error", null, PayOsPayoutState.Failed)]
    [InlineData("QUEUED", null, PayOsPayoutState.Processing)]
    public void MapState_rules(string tx, string? approval, PayOsPayoutState expected) =>
        Assert.Equal(expected, PayOsPayoutClient.MapState([tx], approval));

    [Fact]
    public void MapState_requires_all_transactions_to_succeed()
    {
        Assert.Equal(PayOsPayoutState.Processing, PayOsPayoutClient.MapState(["SUCCEEDED", "PROCESSING"], null));
        Assert.Equal(PayOsPayoutState.Succeeded, PayOsPayoutClient.MapState(["SUCCEEDED", "SUCCEEDED"], "COMPLETED"));
        Assert.Equal(PayOsPayoutState.Processing, PayOsPayoutClient.MapState([], "PROCESSING"));
    }

    // ---------- tra cứu ----------
    [Fact]
    public async Task GetPayout_maps_state_and_ids()
    {
        var h = new FakeHandler(HttpStatusCode.OK, """{"code":"00","data":{"id":"p1","referenceId":"r1","approvalState":"COMPLETED","transactions":[{"state":"SUCCEEDED"}]}}""");
        var s = await NewClient(h).GetPayoutAsync("p1");
        Assert.Equal(PayOsPayoutState.Succeeded, s.State);
        Assert.Equal("p1", s.PayoutId); Assert.Equal("r1", s.ReferenceId); Assert.Equal("COMPLETED", s.ApprovalState);
        Assert.Equal("https://payos.test/v1/payouts/p1", h.Requests[0].Uri);
        Assert.Null(h.Requests[0].HeaderOrNull("x-signature"));
        Assert.Equal("cid", h.Requests[0].Header("x-client-id"));
    }

    [Fact]
    public async Task FindByReference_empty_list_is_not_found_and_url_is_escaped()
    {
        var h = new FakeHandler(HttpStatusCode.OK, """{"code":"00","data":{"payouts":[]}}""");
        var s = await NewClient(h).FindByReferenceAsync("a b&c");
        Assert.Equal(PayOsPayoutState.NotFound, s.State);
        Assert.Equal("https://payos.test/v1/payouts?referenceId=a%20b%26c&limit=10", h.Requests[0].Uri);
        var arr = await NewClient(new FakeHandler(HttpStatusCode.OK, """{"code":"00","data":[]}""")).FindByReferenceAsync("x");
        Assert.Equal(PayOsPayoutState.NotFound, arr.State);
    }

    [Fact]
    public async Task FindByReference_prefers_succeeded_and_handles_array_data()
    {
        var json = """{"code":"00","data":[{"id":"a","transactions":[{"state":"FAILED"}]},{"id":"b","transactions":[{"state":"SUCCEEDED"}]}]}""";
        var s = await NewClient(new FakeHandler(HttpStatusCode.OK, json)).FindByReferenceAsync("r");
        Assert.Equal(PayOsPayoutState.Succeeded, s.State); Assert.Equal("b", s.PayoutId);
    }

    [Fact]
    public async Task Lookup_errors_are_unknown_not_thrown()
    {
        Assert.Equal(PayOsPayoutState.Unknown, (await NewClient(new FakeHandler(new TaskCanceledException())).GetPayoutAsync("p")).State);
        Assert.Equal(PayOsPayoutState.Unknown, (await NewClient(new FakeHandler(HttpStatusCode.InternalServerError, "x")).FindByReferenceAsync("r")).State);
        Assert.Equal(PayOsPayoutState.Unknown, (await NewClient(new FakeHandler(HttpStatusCode.OK, "garbage")).GetPayoutAsync("p")).State);
    }

    // ---------- số dư ----------
    [Fact]
    public async Task Balance_parses_string_and_returns_null_on_failure()
    {
        var h = new FakeHandler(HttpStatusCode.OK, """{"code":"00","data":{"balance":"1250000.50"}}""");
        Assert.Equal(1250000.50m, await NewClient(h).GetBalanceAsync());
        Assert.Equal("https://payos.test/v1/payouts-account/balance", h.Requests[0].Uri);
        Assert.Null(await NewClient(new FakeHandler(HttpStatusCode.Unauthorized, """{"code":"401"}""")).GetBalanceAsync());
        Assert.Null(await NewClient(new FakeHandler(new HttpRequestException())).GetBalanceAsync());
        Assert.Null(await NewClient(new FakeHandler(HttpStatusCode.OK, """{"code":"00","data":{"balance":"abc"}}""")).GetBalanceAsync());
    }

    // ---------- hạ tầng giả ----------
    private static PayOsPayoutClient NewClient(FakeHandler h, bool configured = true, string mode = "Canonical", ILogger<PayOsPayoutClient>? log = null)
    {
        var o = new PayOsPayoutOptions
        {
            ClientId = configured ? "cid" : "", ApiKey = configured ? "akey" : "", ChecksumKey = configured ? Key : "",
            BaseUrl = "https://payos.test/", SignatureMode = mode
        };
        return new PayOsPayoutClient(new HttpClient(h), Options.Create(o), log ?? new ListLogger());
    }

    private sealed record Captured(HttpMethod Method, string Uri, Dictionary<string, string> Headers, string? Body)
    {
        public string Header(string name) => Headers[name];
        public string? HeaderOrNull(string name) => Headers.GetValueOrDefault(name);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode status; private readonly string body; private readonly Exception? error;
        public List<Captured> Requests { get; } = [];
        public FakeHandler(HttpStatusCode status, string body) { this.status = status; this.body = body; }
        public FakeHandler(Exception error) { this.error = error; body = ""; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(new(request.Method, request.RequestUri!.OriginalString,
                request.Headers.ToDictionary(x => x.Key.ToLowerInvariant(), x => string.Join(",", x.Value)),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
            if (error is not null) throw error;
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class ListLogger : ILogger<PayOsPayoutClient>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
