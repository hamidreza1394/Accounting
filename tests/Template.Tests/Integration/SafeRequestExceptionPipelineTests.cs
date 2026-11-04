using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Infrastructure.Middlewares;
using Xunit;

namespace Template.Tests.Integration;

public sealed class SafeRequestExceptionPipelineTests
{
    private const string Secret = "SYNTHETIC_PASSWORD_TOKEN_521";

    [Theory]
    [InlineData("/api/fail", "application/problem+json")]
    [InlineData("/page/fail", "text/plain")]
    public async Task UnhandledException_GivesGenericResponse_WithoutRawExceptionInAnyLog(string path, string contentType)
    {
        var logs = new CaptureProvider();
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureLogging(builder => builder.ClearProviders().AddFilter("Microsoft.AspNetCore", LogLevel.Warning).AddProvider(logs))
            .Configure(app =>
            {
                app.UseMiddleware<SafeRequestExceptionMiddleware>();
                app.Run(_ => throw new InvalidOperationException("Password=" + Secret,
                    new Exception("Server=fake;token=" + Secret)));
            }));
        using var client = server.CreateClient();
        using var response = await client.GetAsync(path + "?token=" + Secret);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(500, (int)response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.DoesNotContain(Secret, body);
        Assert.Contains(logs.Entries, entry => entry.Message.Contains("Unexpected request failure"));
        Assert.All(logs.Entries, entry =>
        {
            Assert.Null(entry.Exception);
            Assert.DoesNotContain(Secret, entry.Message);
        });
    }

    [Fact]
    public async Task InvalidRequest_ReturnsGenericBadRequestWithoutErrorLog()
    {
        var logs = new CaptureProvider();
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureLogging(builder => builder.ClearProviders().AddFilter("Microsoft.AspNetCore", LogLevel.Warning).AddProvider(logs))
            .Configure(app =>
            {
                app.UseMiddleware<SafeRequestExceptionMiddleware>();
                app.Run(_ => throw new BadHttpRequestException(Secret, StatusCodes.Status413PayloadTooLarge));
            }));
        using var client = server.CreateClient();
        using var response = await client.GetAsync("/api/fail");
        Assert.Equal(413, (int)response.StatusCode);
        Assert.DoesNotContain(Secret, await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(logs.Entries, entry => entry.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task ExceptionAfterResponseStarted_AbortsAndLogsOnlySafeMetadata()
    {
        var logs = new CaptureProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new StartedResponse());
        var lifetime = new RequestLifetime();
        context.Features.Set<IHttpRequestLifetimeFeature>(lifetime);
        var middleware = new SafeRequestExceptionMiddleware(
            _ => throw new InvalidOperationException(Secret, new Exception(Secret)),
            factory.CreateLogger<SafeRequestExceptionMiddleware>());

        await middleware.InvokeAsync(context);

        Assert.True(lifetime.Aborted);
        Assert.Contains(logs.Entries, entry => entry.Message.Contains("Unexpected request failure"));
        Assert.All(logs.Entries, entry =>
        {
            Assert.Null(entry.Exception);
            Assert.DoesNotContain(Secret, entry.Message);
        });
    }

    private sealed class RequestLifetime : IHttpRequestLifetimeFeature
    {
        public CancellationToken RequestAborted { get; set; }
        public bool Aborted { get; private set; }
        public void Abort() => Aborted = true;
    }

    private sealed class StartedResponse : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted => true;
        public void OnStarting(Func<object, Task> callback, object state) { }
        public void OnCompleted(Func<object, Task> callback, object state) { }
    }

    private sealed record Entry(LogLevel Level, string Message, Exception? Exception);
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
            Func<TState, Exception?, string> formatter) => entries.Enqueue(new Entry(logLevel, formatter(state, exception), exception));
    }
}
