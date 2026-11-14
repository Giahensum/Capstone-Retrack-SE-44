using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Retrack.API.Services.Depot;
using Xunit;

namespace Retrack.API.Tests;

public sealed class VerificationScannerTests
{
    private sealed class Host(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Fixture";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private static VerificationFileScanner Scanner(int port = 1, string mode = "ClamAv", string environment = "Production") =>
        new(Options.Create(new VerificationScannerOptions { Port = port, Mode = mode, TimeoutSeconds = 1 }), new Host(environment));
    [Fact]
    public async Task Disabled_scanning_is_explicit_in_development_and_fails_closed_in_production()
    {
        using var dev = Scanner(mode: "Disabled", environment: "Development");
        Assert.True(dev.Policy.AcceptsUploads); Assert.Equal("DevelopmentOnly", dev.Policy.Mode);
        Assert.False(await dev.ScanAsync("%PDF-1.7"u8.ToArray()));
        using var production = Scanner(mode: "Disabled");
        Assert.False(production.Policy.AcceptsUploads);
        await Assert.ThrowsAsync<VerificationScannerUnavailableException>(() => production.ScanAsync("%PDF-1.7"u8.ToArray()));
    }
    [Theory]
    [InlineData("stream: OK\0", false, false)]
    [InlineData("stream: Fixture FOUND\0", true, false)]
    [InlineData("stream: Scan failed ERROR\0", false, true)]
    [InlineData("stream: OK", false, true)]
    public async Task Instream_protocol_transmits_bounded_chunks_and_only_accepts_complete_clean_response(string reply, bool malware, bool unavailable)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var content = new byte[160000]; Random.Shared.NextBytes(content);
        var received = new MemoryStream();
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var header = new byte[10]; await stream.ReadExactlyAsync(header);
            Assert.Equal("zINSTREAM\0", Encoding.ASCII.GetString(header));
            var length = new byte[4];
            while (true)
            {
                await stream.ReadExactlyAsync(length); var count = BinaryPrimitives.ReadInt32BigEndian(length);
                if (count == 0) break;
                Assert.InRange(count, 1, 64 * 1024);
                var bytes = new byte[count]; await stream.ReadExactlyAsync(bytes); received.Write(bytes);
            }
            await stream.WriteAsync(Encoding.UTF8.GetBytes(reply));
        });
        using var scanner = Scanner(port);
        if (malware) await Assert.ThrowsAsync<ArgumentException>(() => scanner.ScanAsync(content));
        else if (unavailable) await Assert.ThrowsAsync<VerificationScannerUnavailableException>(() => scanner.ScanAsync(content));
        else Assert.True(await scanner.ScanAsync(content));
        await server;
        Assert.Equal(content, received.ToArray());
    }
    [Fact]
    public async Task Timeout_and_caller_cancellation_do_not_silently_accept_file()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var stop = new CancellationTokenSource();
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(stop.Token);
            try { await Task.Delay(5000, stop.Token); } catch (OperationCanceledException) { }
        });
        using var scanner = Scanner(port);
        await Assert.ThrowsAsync<VerificationScannerUnavailableException>(() => scanner.ScanAsync("%PDF-1.7"u8.ToArray()));
        stop.Cancel(); await server;
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanner.ScanAsync("%PDF-1.7"u8.ToArray(), cancelled.Token));
    }
    [Fact]
    public async Task Oversized_content_is_rejected_before_connecting()
    {
        using var scanner = Scanner();
        await Assert.ThrowsAsync<ArgumentException>(() => scanner.ScanAsync(new byte[2 * 1024 * 1024 + 1]));
    }
}
