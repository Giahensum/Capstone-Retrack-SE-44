using System.Net;
using System.Text.Json;
using Retrack.API.DTOs;
using Retrack.API.Services.Depot;

namespace Retrack.API.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                if (context.RequestAborted.IsCancellationRequested && ex is OperationCanceledException) return;
                if (context.Response.HasStarted) throw;
                await HandleExceptionAsync(context, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            var depotRequest = context.Request.Path.StartsWithSegments("/api/depot")
                || context.Request.Path.StartsWithSegments("/api/payos");
            var (statusCode, message) = ex switch
            {
                DepotForbiddenException => (HttpStatusCode.Forbidden, ex.Message),
                DepotConflictException => (HttpStatusCode.Conflict, ex.Message),
                KeyNotFoundException => (HttpStatusCode.NotFound, ex.Message),
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, ex.Message),
                InvalidOperationException when !depotRequest => (HttpStatusCode.BadRequest, ex.Message),
                ArgumentException => (HttpStatusCode.BadRequest, ex.Message),
                System.ComponentModel.DataAnnotations.ValidationException => (HttpStatusCode.BadRequest, ex.Message),
                VerificationScannerUnavailableException => (HttpStatusCode.ServiceUnavailable, ex.Message),
                Retrack.API.Services.Shared.VerificationStorageUnavailableException => (HttpStatusCode.ServiceUnavailable, ex.Message),
                PayOsException => (HttpStatusCode.BadGateway, "Không kết nối được dịch vụ thanh toán. Hãy kiểm tra trạng thái hóa đơn trước khi thử lại."),
                HttpRequestException => (HttpStatusCode.BadGateway, "Dịch vụ kết nối đang gián đoạn. Vui lòng thử lại sau."),
                OperationCanceledException => (HttpStatusCode.GatewayTimeout, "Dịch vụ chưa phản hồi kịp. Hãy kiểm tra kết quả trước khi gửi lại thao tác."),
                _ => (HttpStatusCode.InternalServerError, "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.")
            };
            var code = statusCode switch
            {
                HttpStatusCode.BadRequest => "VALIDATION_ERROR",
                HttpStatusCode.Unauthorized => "UNAUTHENTICATED",
                HttpStatusCode.Forbidden => "FORBIDDEN",
                HttpStatusCode.NotFound => "NOT_FOUND",
                HttpStatusCode.Conflict => "CONFLICT",
                HttpStatusCode.BadGateway => "PROVIDER_UNAVAILABLE",
                HttpStatusCode.GatewayTimeout => "PROVIDER_TIMEOUT",
                HttpStatusCode.ServiceUnavailable => ex is Retrack.API.Services.Shared.VerificationStorageUnavailableException
                    ? "VERIFICATION_STORAGE_UNAVAILABLE" : "VERIFICATION_SCANNER_UNAVAILABLE",
                _ => "INTERNAL_ERROR"
            };
            if ((int)statusCode >= 500)
                _logger.LogError(ex, "API {Code}, mã tra cứu {TraceId}", code, context.TraceIdentifier);
            else
                _logger.LogWarning("API {Code}, mã tra cứu {TraceId}", code, context.TraceIdentifier);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;
            context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;

            var response = ApiResponse<object>.Fail(message);
            response.Code = code;
            response.TraceId = context.TraceIdentifier;
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return context.Response.WriteAsync(json);
        }
    }

    // Extension method
    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
            => app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}

