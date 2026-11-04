using System.Collections.Concurrent;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Server.Api;
using Xunit;

namespace Template.Tests.Integration;

public sealed class SafeExceptionLoggingTests
{
    private const string Secret = "FAKE_PASSWORD_TOKEN_CONNECTION_SECRET";

    [Fact]
    public async Task ApiExceptionHandler_DoesNotLogExceptionOrRequestData_AndPreservesDiagnostics()
    {
        var capture = new CaptureProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(capture));
        using var services = new ServiceCollection().AddSingleton<ILoggerFactory>(factory).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "trace-safe-521-" + Secret };
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/" + Secret;
        context.Response.Body = new MemoryStream();
        context.Features.Set<IExceptionHandlerFeature>(new ErrorFeature(new InvalidOperationException(
            "Server=example;Password=" + Secret, new Exception("token=" + Secret))));

        await ApiEndpoints.WriteUnexpectedErrorAsync(context);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.DoesNotContain(Secret, body);
        Assert.NotEmpty(capture.Entries);
        Assert.All(capture.Entries, logged =>
        {
            Assert.Null(logged.Exception);
            Assert.DoesNotContain(Secret, logged.Message);
            Assert.DoesNotContain(context.TraceIdentifier, logged.Message);
            Assert.DoesNotContain(context.Request.Path.Value!, logged.Message);
        });
        var entry = Assert.Single(capture.Entries, logged => logged.Message.Contains("Unexpected request failure"));
        Assert.Null(entry.Exception);
        Assert.DoesNotContain(Secret, entry.Message);
        Assert.Contains("InvalidOperationException", entry.Message);
        Assert.Matches(@"FailureId=[0-9a-f]{32};", entry.Message);
        Assert.Contains("POST", entry.Message);
    }

    [Fact]
    public async Task ApiFilter_DoesNotLogRouteOrRawRequestPath()
    {
        var capture = new CaptureProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(capture));
        var context = new DefaultHttpContext { TraceIdentifier = "trace-filter-521-" + Secret };
        context.Request.Method = "GET";
        context.Request.Path = "/api/items/" + Secret;
        context.SetEndpoint(CreateEndpoint());
        var filter = new ApiResponseFilter(factory.CreateLogger<ApiResponseFilter>());

        var result = await filter.InvokeAsync(EndpointFilterInvocationContext.Create(context),
            _ => throw new InvalidOperationException(Secret, new Exception(Secret)));

        Assert.NotNull(result);
        Assert.All(capture.Entries, logged =>
        {
            Assert.Null(logged.Exception);
            Assert.DoesNotContain(Secret, logged.Message);
            Assert.DoesNotContain("/api/items", logged.Message);
            Assert.DoesNotContain(context.TraceIdentifier, logged.Message);
        });
        var entry = Assert.Single(capture.Entries, logged => logged.Message.Contains("Unexpected request failure"));
        Assert.Null(entry.Exception);
        Assert.Matches(@"FailureId=[0-9a-f]{32};", entry.Message);
        Assert.Contains("GET", entry.Message);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Handler_DoesNotLogOriginalEndpointOrRequestPath()
    {
        var capture = new CaptureProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(capture));
        using var services = new ServiceCollection().AddSingleton<ILoggerFactory>(factory).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "trace-handler-" + Secret };
        context.Request.Method = "GET";
        context.Request.Path = "/api/items/" + Secret;
        context.Response.Body = new MemoryStream();
        context.Features.Set<IExceptionHandlerFeature>(new ErrorFeature(new InvalidOperationException(Secret), CreateEndpoint()));

        await ApiEndpoints.WriteUnexpectedErrorAsync(context);

        Assert.All(capture.Entries, logged =>
        {
            Assert.Null(logged.Exception);
            Assert.DoesNotContain(Secret, logged.Message);
            Assert.DoesNotContain("/api/items", logged.Message);
            Assert.DoesNotContain(context.TraceIdentifier, logged.Message);
        });
        var entry = Assert.Single(capture.Entries, logged => logged.Message.Contains("Unexpected request failure"));
        Assert.Matches(@"FailureId=[0-9a-f]{32};", entry.Message);
        Assert.Null(entry.Exception);
    }

    private static Endpoint CreateEndpoint() => new RouteEndpointBuilder(
        _ => Task.CompletedTask, RoutePatternFactory.Parse("/api/items/{id}"), 0).Build();

    [Fact]
    public void RequestLogger_UsesServerGeneratedFailureIdInsteadOfUntrustedRequestData()
    {
        var capture = new CaptureProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(capture));
        var context = new DefaultHttpContext { TraceIdentifier = "trace-" + Secret + "\r\nforged" };
        context.Request.Method = "GET";

        Infrastructure.Security.SafeExceptionLogging.LogRequestFailure(
            factory.CreateLogger("test"), context, new InvalidOperationException(Secret));

        var entry = Assert.Single(capture.Entries);
        Assert.Matches(@"FailureId=[0-9a-f]{32};", entry.Message);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain(Secret, entry.Message);
        Assert.DoesNotContain(context.TraceIdentifier, entry.Message);
        Assert.DoesNotContain("\r", entry.Message);
        Assert.DoesNotContain("\n", entry.Message);
    }

    [Fact]
    public async Task LegacyMiddleware_RethrowsButLogsOnlySafeMetadata()
    {
        var capture = new CaptureProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(capture));
        var context = new DefaultHttpContext { TraceIdentifier = "trace-middleware-521-" + Secret };
        context.Request.Method = "GET";
        context.Request.Path = "/private/" + Secret;
        var middleware = new Infrastructure.Middlewares.GlobalExceptionHandlerMiddleware(
            _ => throw new InvalidOperationException(Secret, new Exception(Secret)),
            factory.CreateLogger<Infrastructure.Middlewares.GlobalExceptionHandlerMiddleware>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
        Assert.All(capture.Entries, logged =>
        {
            Assert.Null(logged.Exception);
            Assert.DoesNotContain(Secret, logged.Message);
            Assert.DoesNotContain(context.TraceIdentifier, logged.Message);
            Assert.DoesNotContain("/private/", logged.Message);
        });
        var entry = Assert.Single(capture.Entries, logged => logged.Message.Contains("Unexpected request failure"));
        Assert.Null(entry.Exception);
        Assert.DoesNotContain(Secret, entry.Message);
        Assert.Contains("InvalidOperationException", entry.Message);
        Assert.Matches(@"FailureId=[0-9a-f]{32};", entry.Message);
    }

    private sealed class ErrorFeature(Exception error, Endpoint? endpoint = null) : IExceptionHandlerFeature
    {
        public Exception Error { get; } = error;
        public Endpoint? Endpoint { get; } = endpoint;
    }

    private sealed record Entry(string Message, Exception? Exception);
    private sealed class CaptureProvider : ILoggerProvider
    {
        public ConcurrentQueue<Entry> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Entries);
        public void Dispose() { }
    }
    private sealed class CaptureLogger(ConcurrentQueue<Entry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => entries.Enqueue(new Entry(formatter(state, exception), exception));
    }
}
