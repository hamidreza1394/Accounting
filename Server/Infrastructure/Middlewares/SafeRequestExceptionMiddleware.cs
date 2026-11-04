using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Server.Api;

namespace Infrastructure.Middlewares;

/// <summary>
/// Terminates unexpected request failures without passing exception objects to framework
/// exception handlers, diagnostic listeners, or the server's unhandled-exception logger.
/// A response that has already started is aborted because it cannot safely be replaced.
/// </summary>
public sealed class SafeRequestExceptionMiddleware(RequestDelegate next, ILogger<SafeRequestExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (
            (exception is OperationCanceledException or IOException) && context.RequestAborted.IsCancellationRequested)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            }
            else
            {
                context.Abort();
            }
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                Infrastructure.Security.SafeExceptionLogging.LogRequestFailure(logger, context, exception);
                context.Abort();
                return;
            }

            try
            {
                context.Response.Clear();
                context.Response.Headers.CacheControl = "no-store";
                if (ApiEndpoints.IsApiRequest(context.Request.Path))
                {
                    context.Features.Set<IExceptionHandlerFeature>(new SafeExceptionFeature(exception));
                    await ApiEndpoints.WriteUnexpectedErrorAsync(context);
                }
                else
                {
                    if (exception is not BadHttpRequestException)
                    {
                        Infrastructure.Security.SafeExceptionLogging.LogRequestFailure(logger, context, exception);
                    }
                    context.Response.StatusCode = exception is BadHttpRequestException badRequest
                        ? badRequest.StatusCode
                        : StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "text/plain; charset=utf-8";
                    await context.Response.WriteAsync("An unexpected error occurred.");
                }
            }
            catch (Exception handlerException)
            {
                // Error rendering can itself fail (e.g. the client disconnects). Do not allow
                // either exception to escape to an unhandled-exception logger.
                Infrastructure.Security.SafeExceptionLogging.LogRequestFailure(logger, context, handlerException);
                context.Abort();
            }
        }
    }

    private sealed class SafeExceptionFeature(Exception exception) : IExceptionHandlerFeature
    {
        public Exception Error { get; } = exception;
    }
}
