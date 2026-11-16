using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GameOfLife.Api.ErrorHandling;

/// <summary>
/// Last-resort handler: converts any unhandled exception into an RFC 7807 response
/// without leaking internal details to the client.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Unhandled exception while processing {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = "The server encountered an unexpected condition while processing the request.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            Instance = httpContext.Request.Path,
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken);

        return true;
    }
}
