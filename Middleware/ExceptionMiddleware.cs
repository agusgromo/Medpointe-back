using Medpointe.Models.Api;

namespace Medpointe.Middleware;

public sealed class ExceptionMiddleware(
    RequestDelegate next,
    ILogger<ExceptionMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            }
            context.Abort();
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Unhandled exception for {Method} {Path}. TraceId: {TraceId}",
                context.Request.Method, context.Request.Path, context.TraceIdentifier);

            // Headers and content already sent cannot be replaced safely.
            if (context.Response.HasStarted)
            {
                context.Abort();
                return;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.Headers.CacheControl = "no-store";

            var error = new ApiError
            {
                Title = "Internal server error",
                Message = "An unexpected error occurred. Please try again later. If the error persists, contact an administrator.",
                Code = "INTERNAL_SERVER_ERROR",
                TraceId = context.TraceIdentifier,
                Details = environment.IsDevelopment() ? exception.ToString() : null
            };

            try
            {
                await context.Response.WriteAsJsonAsync(error, context.RequestAborted);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                context.Abort();
            }
        }
    }
}
