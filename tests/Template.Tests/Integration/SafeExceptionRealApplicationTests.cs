using System.Collections.Concurrent;
using Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Template.Tests.Integration;

public sealed class SafeExceptionRealApplicationTests
{
    private const string Secret = "FAKE_REAL_PIPELINE_PASSWORD_521";

    [Theory]
    [InlineData("Development", "/api/fail", "application/problem+json")]
    [InlineData("Production", "/api/fail", "application/problem+json")]
    [InlineData("Development", "/page/fail", "text/plain")]
    [InlineData("Production", "/page/fail", "text/plain")]
    public async Task ApplicationPipeline_DoesNotExposeMiddlewareFailure(string environment, string path, string contentType)
    {
        var logs = new CaptureProvider();
        using var original = new FaranouApplicationFactory();
        await original.InitializeAsync();
        using var application = original.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IOptionsMonitor<SecurityHeadersSettings>>();
                services.AddSingleton<IOptionsMonitor<SecurityHeadersSettings>>(new ThrowingOptions());
            });
        });
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        logs.Entries.Clear();

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

    private sealed class ThrowingOptions : IOptionsMonitor<SecurityHeadersSettings>
    {
        public SecurityHeadersSettings CurrentValue => throw new InvalidOperationException(
            "Password=" + Secret, new Exception("Server=fake;token=" + Secret));
        public SecurityHeadersSettings Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<SecurityHeadersSettings, string?> listener) => null;
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
