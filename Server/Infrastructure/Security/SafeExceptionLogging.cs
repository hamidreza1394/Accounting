using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
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
            GetSafeTraceId(context), method, route,
            exception?.GetType().FullName ?? "unknown");
    }

    private static string GetSafeTraceId(HttpContext context)
    {
        if (Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity)
        {
            return activity.TraceId.ToString();
        }

        // TraceIdentifier can be assigned by middleware and is not guaranteed to be trusted.
        // Hash it rather than writing attacker-controlled text (or log delimiters) verbatim.
        var traceIdentifier = context.TraceIdentifier;
        if (string.IsNullOrEmpty(traceIdentifier) || traceIdentifier.Length > 128)
        {
            return "unavailable";
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(traceIdentifier)))[..32];
    }
}
