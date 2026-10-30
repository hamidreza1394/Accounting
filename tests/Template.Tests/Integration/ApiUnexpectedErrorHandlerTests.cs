using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Server.Api;
using Xunit;

namespace Template.Tests.Integration;

public sealed class ApiUnexpectedErrorHandlerTests
{
    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status413PayloadTooLarge)]
    public async Task BadHttpRequestException_PreservesItsStatus_ReturnsGenericProblem_AndIsNotErrorLogged(int statusCode)
    {
        const string parserDetail = "sensitive request-body parser detail";
        var exception = new BadHttpRequestException(parserDetail, statusCode);
        var logProvider = new RecordingLoggerProvider();

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logProvider));
        using var services = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(loggerFactory)
            .BuildServiceProvider();

        var context = CreateContext(services, exception);

        await ApiEndpoints.WriteUnexpectedErrorAsync(context);

        Assert.Equal(statusCode, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var responseBody = await reader.ReadToEndAsync();

        Assert.Contains("درخواست نامعتبر است", responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain(parserDetail, responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain(LogLevel.Error, logProvider.Levels);
    }

    [Fact]
    public async Task UnexpectedException_Remains500_AndIsErrorLogged()
    {
        var logProvider = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logProvider));
        using var services = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(loggerFactory)
            .BuildServiceProvider();

        var context = CreateContext(services, new InvalidOperationException("unexpected failure"));

        await ApiEndpoints.WriteUnexpectedErrorAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains(LogLevel.Error, logProvider.Levels);
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services, Exception exception)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services,
        };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/v1/provisioning/apply";
        context.Response.Body = new MemoryStream();
        context.Features.Set<IExceptionHandlerFeature>(new TestExceptionHandlerFeature(exception));
        return context;
    }

    private sealed class TestExceptionHandlerFeature(Exception error) : IExceptionHandlerFeature
    {
        public Exception Error { get; } = error;
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogLevel> levels = new();

        public IReadOnlyCollection<LogLevel> Levels => levels.ToArray();

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(levels);

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger(ConcurrentQueue<LogLevel> levels) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            levels.Enqueue(logLevel);
        }
    }
}
