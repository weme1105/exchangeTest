using System.Net;
using System.Text.Json;

namespace ExchangeTest.Api.Middleware;

public sealed class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, code, message) = ex switch
        {
            ArgumentException => (HttpStatusCode.BadRequest, "validation_error", ex.Message),
            KeyNotFoundException => (HttpStatusCode.NotFound, "not_found", ex.Message),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, "forbidden", "You are not allowed to perform this operation."),
            InvalidOperationException => (HttpStatusCode.Conflict, "invalid_operation", ex.Message),
            _ => (HttpStatusCode.InternalServerError, "internal_error", "An unexpected error occurred.")
        };

        if ((int)statusCode >= 500)
            _logger.LogError(ex, "Unhandled API exception");

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(new
        {
            code,
            message,
            traceId = context.TraceIdentifier
        });

        await context.Response.WriteAsync(payload);
    }
}
