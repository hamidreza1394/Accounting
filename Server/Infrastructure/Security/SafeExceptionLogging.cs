using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Security;

/// <summary>Logs only server-generated failure identifiers and fixed diagnostic metadata.</summary>
public static class SafeExceptionLogging
{
    public static void LogRequestFailure(ILogger logger, HttpContext context, Exception? exception)
    {
        var method = context.Request.Method switch
        {
            "GET" => "GET",
            "POST" => "POST",
            "PUT" => "PUT",
            "PATCH" => "PATCH",
            "DELETE" => "DELETE",
            "HEAD" => "HEAD",
            "OPTIONS" => "OPTIONS",
            _ => "OTHER",
        };

        // Do not log TraceIdentifier, Activity.TraceId, request paths, route metadata, or exception
        // objects. Trace identifiers may originate from caller-controlled headers and route metadata
        // can be supplied by dynamically registered endpoints.
        var failureId = Guid.NewGuid().ToString("N");
        logger.LogError(new EventId(521, "UnexpectedRequestFailure"),
            "Unexpected request failure. FailureId={FailureId}; Method={Method}; ExceptionType={ExceptionType}.",
            failureId, method, exception?.GetType().FullName ?? "unknown");
    }
}
