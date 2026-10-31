using Faranou.Vendor.Persistence.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Verifies the complete Vendor audit chain against the independently permissioned checkpoint
/// store before a Production host becomes ready. Development and test hosts do not resolve the
/// production-only store.
/// </summary>
public sealed class VendorAuditCheckpointStartupService(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    VendorAuditCheckpointHealthState healthState,
    TimeProvider timeProvider,
    ILogger<VendorAuditCheckpointStartupService> logger) : IHostedService
{
    private const string StartupFailureMessage =
        "Vendor Production cannot start because the independent audit checkpoint could not be verified and advanced.";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var checkpointService = scope.ServiceProvider.GetRequiredService<VendorAuditCheckpointService>();
            await checkpointService.CreateCheckpointAsync(cancellationToken);
            healthState.MarkSuccess(timeProvider.GetUtcNow());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            healthState.MarkFailure(exception.GetType().Name);
            // Provider exception messages may contain endpoints or credential details.
            // Log only the exception type and fail startup with a stable, non-sensitive message.
            logger.LogCritical(
                "Vendor Production audit checkpoint startup verification failed. FailureType={FailureType}.",
                exception.GetType().Name);
            throw new InvalidOperationException(StartupFailureMessage);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
