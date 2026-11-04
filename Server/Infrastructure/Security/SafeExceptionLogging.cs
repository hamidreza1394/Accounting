using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Security;

/// <summary>Logs request failure metadata only; exception payload and raw request data are never logged.</summary>
public static class SafeExceptionLogging
{
    public static void LogRequestFailure(ILogger logger, HttpContext context, Exception? exception)
    {
        var method = context.Request.Method switch
        {
            "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS" => context.Request.Method,
            _ => "OTHER",
        };
        var endpoint = context.GetEndpoint() ?? context.Features.Get<IExceptionHandlerFeature>()?.Endpoint;
        var route = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
        logger.LogError(new EventId(521, "UnexpectedRequestFailure"),
            "Unexpected request failure. TraceId={TraceId}; Method={Method}; Route={Route}; ExceptionType={ExceptionType}.",
            Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier, method, route,
            exception?.GetType().FullName ?? "unknown");
    }
}
