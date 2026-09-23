namespace Retrack.API.Middleware;

/// <summary>
/// Global exception handler — Tự động bắt lỗi và trả response chuẩn
/// </summary>
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
        catch (Exception ex) when (ex is Retrack.API.Helpers.ConflictException
            or Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException
            || ex is Microsoft.EntityFrameworkCore.DbUpdateException { InnerException: Npgsql.PostgresException { SqlState: "23505" or "40001" } })
        {
            context.Response.StatusCode = 409;
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Dữ liệu đã thay đổi hoặc thao tác không còn hợp lệ. Vui lòng tải lại." });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found");
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { success = false, message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access");
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { success = false, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request");
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Internal server error" });
        }
    }
}
