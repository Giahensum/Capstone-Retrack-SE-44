using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Retrack.API.DTOs;

namespace Retrack.API.Helpers;

public static class VerificationRateLimits
{
    public const string Upload = "depot-verification-upload";
    public const string Download = "depot-verification-download";
    public static IServiceCollection AddVerificationRateLimits(this IServiceCollection services) => services.AddRateLimiter(options =>
    {
        options.AddPolicy(Upload, context => Partition(context, 8));
        options.AddPolicy(Download, context => Partition(context, 30));
        options.OnRejected = async (context, token) =>
        {
            var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
                ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)) : 60;
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            context.HttpContext.Response.Headers["X-Correlation-ID"] = context.HttpContext.TraceIdentifier;
            var response = Retrack.API.DTOs.ApiResponse<object>.Fail("Bạn thao tác với hồ sơ quá nhanh. Vui lòng chờ một phút rồi thử lại.");
            response.Code = "RATE_LIMITED"; response.TraceId = context.HttpContext.TraceIdentifier;
            await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: token);
        };
    });
    private static RateLimitPartition<string> Partition(HttpContext context, int limit) =>
        RateLimitPartition.GetSlidingWindowLimiter(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous", _ => new()
        {
            PermitLimit = limit, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6,
            QueueLimit = 0, AutoReplenishment = true,
        });
}
