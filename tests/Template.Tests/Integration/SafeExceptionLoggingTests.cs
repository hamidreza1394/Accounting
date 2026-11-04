using System.Collections.Concurrent;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Server.Api;
using Xunit;

namespace Template.Tests.Integration;

public sealed class SafeExceptionLoggingTests
{
    private const string Secret = "FAKE_PASSWORD_TOKEN_CONNECTION_SECRET";

    [Fact]
    public async Task ApiExceptionHandler_DoesNotLogExceptionOrRequestPath_AndPreservesDiagnostics()
    {
        var capture = new CaptureProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(capture));
        using var services = new ServiceCollection().AddSingleton<ILoggerFactory>(factory).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "trace-safe-521" };
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
        var entry = Assert.Single(capture.Entries);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain(Secret, entry.Message);
        Assert.Contains("InvalidOperationException", entry.Message);
        Assert.Contains("trace-safe-521", entry.Message);
        Assert.Contains("POST", entry.Message);
    }

    [Fact]
    public async Task LegacyMiddleware_RethrowsButLogsOnlySafeMetadata()
    {
        var capture = new CaptureProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(capture));
        var context = new DefaultHttpContext { TraceIdentifier = "trace-middleware-521" };
        context.Request.Method = "GET";
        context.Request.Path = "/private/" + Secret;
        var middleware = new Infrastructure.Middlewares.GlobalExceptionHandlerMiddleware(
            _ => throw new InvalidOperationException(Secret, new Exception(Secret)),
            factory.CreateLogger<Infrastructure.Middlewares.GlobalExceptionHandlerMiddleware>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
        var entry = Assert.Single(capture.Entries);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain(Secret, entry.Message);
        Assert.Contains("InvalidOperationException", entry.Message);
        Assert.Contains("trace-middleware-521", entry.Message);
    }

    private sealed class ErrorFeature(Exception error) : IExceptionHandlerFeature
    {
        public Exception Error { get; } = error;
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
