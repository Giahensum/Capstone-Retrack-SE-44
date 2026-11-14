using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Retrack.API.Services.Shared;
using Xunit;

namespace Retrack.API.Tests;

public sealed class PrivateVerificationStorageTests
{
    private static readonly string Key = "retrack/private-depot-verifications/" + new string('a', 32) + "/" + new string('b', 32) + ".pdf";
    private sealed class Handler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; return Task.FromResult(response()); }
    }
    private sealed class Clients(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
    private static CloudinaryVerificationStorage Store(Handler handler)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Cloudinary:CloudName"]="fixture", ["Cloudinary:ApiKey"]="123456", ["Cloudinary:ApiSecret"]="fixture-only",
        }).Build();
        return new(config, Options.Create(new VerificationStorageOptions()), new Clients(handler), NullLogger<CloudinaryVerificationStorage>.Instance);
    }
    [Fact]
    public void Private_download_is_short_lived_authenticated_raw_api_url_even_after_provider_switches_back_to_database()
    {
        using var handler = new Handler(() => new(HttpStatusCode.OK));
        var store = Store(handler); Assert.Equal("DATABASE", store.Provider);
        var uri = store.DownloadUri(Key);
        Assert.Equal("https", uri.Scheme); Assert.Equal("api.cloudinary.com", uri.Host);
        Assert.EndsWith("/raw/download", uri.AbsolutePath);
        var query = uri.Query.TrimStart('?').Split('&').Select(x => x.Split('=',2)).ToDictionary(x => x[0], x => Uri.UnescapeDataString(x[1]));
        Assert.Equal("authenticated", query["type"]); Assert.Equal(Key, query["public_id"]);
        Assert.NotEmpty(query["signature"]);
        Assert.InRange(long.Parse(query["expires_at"]), DateTimeOffset.UtcNow.ToUnixTimeSeconds()+100, DateTimeOffset.UtcNow.ToUnixTimeSeconds()+121);
    }
    [Theory]
    [InlineData("https://example.com/private.pdf")]
    [InlineData("retrack/private-depot-verifications/../../private.pdf")]
    [InlineData("retrack/private-depot-verifications/abc/def.pdf")]
    public async Task Untrusted_storage_key_never_causes_network_fetch(string key)
    {
        using var handler = new Handler(() => new(HttpStatusCode.OK));
        await Assert.ThrowsAsync<VerificationStorageUnavailableException>(() => Store(handler).ReadAsync(key, 10, default));
        Assert.Equal(0, handler.Calls);
    }
    [Theory]
    [InlineData(302)]
    [InlineData(401)]
    [InlineData(404)]
    [InlineData(500)]
    public async Task Provider_redirect_or_error_is_not_returned_as_document(int status)
    {
        using var handler = new Handler(() => new((HttpStatusCode)status));
        await Assert.ThrowsAsync<VerificationStorageUnavailableException>(() => Store(handler).ReadAsync(Key, 10, default));
        Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task Body_length_is_bounded_even_without_content_length_header()
    {
        var source = new CountingStream(new byte[1000000]);
        using var handler = new Handler(() => new(HttpStatusCode.OK) { Content = new StreamContent(source) });
        await Assert.ThrowsAsync<VerificationStorageUnavailableException>(() => Store(handler).ReadAsync(Key, 5, default));
        Assert.Equal(6, source.BytesRead);
    }
    private sealed class CountingStream(byte[] content) : MemoryStream(content)
    {
        public long BytesRead;
        public override bool CanSeek => false;
        public override async ValueTask<int> ReadAsync(Memory<byte> bytes, CancellationToken ct = default)
        { var count = await base.ReadAsync(bytes, ct); BytesRead += count; return count; }
    }
}
