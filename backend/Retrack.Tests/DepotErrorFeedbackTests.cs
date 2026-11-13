using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Retrack.API.Middleware;
using Retrack.API.Services.Depot;
using Xunit;

namespace Retrack.Tests;

public sealed class DepotErrorFeedbackTests
{
    [Theory]
    [InlineData("internal", 500, "INTERNAL_ERROR")]
    [InlineData("argument", 400, "VALIDATION_ERROR")]
    [InlineData("conflict", 409, "CONFLICT")]
    [InlineData("forbidden", 403, "FORBIDDEN")]
    [InlineData("missing", 404, "NOT_FOUND")]
    [InlineData("provider", 502, "PROVIDER_UNAVAILABLE")]
    [InlineData("payos", 502, "PROVIDER_UNAVAILABLE")]
    [InlineData("timeout", 504, "PROVIDER_TIMEOUT")]
    public async Task Depot_errors_have_safe_messages_and_matching_correlation(string kind, int status, string code)
    {
        Exception failure = kind switch
        {
            "argument" => new ArgumentException("Khối lượng phải lớn hơn 0."),
            "conflict" => new DepotConflictException("Tồn kho khả dụng không đủ."),
            "forbidden" => new DepotForbiddenException(),
            "missing" => new KeyNotFoundException("Không tìm thấy phiếu nhập."),
            "provider" => new HttpRequestException("private-provider-response"),
            "payos" => new PayOsException("private-provider-response"),
            "timeout" => new OperationCanceledException("private-timeout"),
            _ => new InvalidOperationException("private-database-detail")
        };
        var context = new DefaultHttpContext { TraceIdentifier = "depot-test-correlation" };
        context.Request.Path = "/api/depot/stock/receipts";
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => throw failure,
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.Equal(context.TraceIdentifier, body.RootElement.GetProperty("traceId").GetString());
        Assert.Equal(context.TraceIdentifier, context.Response.Headers["X-Correlation-ID"].ToString());
        Assert.False(body.RootElement.GetProperty("success").GetBoolean());
        if (status >= 500) Assert.DoesNotContain("private-", body.RootElement.GetProperty("message").GetString());
        if (kind == "conflict") Assert.Equal(failure.Message, body.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Request_cancellation_does_not_write_a_system_error_body()
    {
        var context = new DefaultHttpContext();
        context.RequestAborted = new CancellationToken(true);
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => throw new OperationCanceledException(),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(0, context.Response.Body.Length);
    }
}
