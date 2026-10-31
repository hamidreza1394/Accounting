using Faranou.Vendor.Persistence.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Periodically verifies the full chain and advances the independent checkpoint.
/// Failures are visible through the audit-checkpoint health endpoint and never trigger a local fallback.
/// </summary>
public sealed class VendorAuditCheckpointBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<VendorAuditCheckpointOptions> options,
    VendorAuditCheckpointHealthState healthState,
    TimeProvider timeProvider,
    ILogger<VendorAuditCheckpointBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        settings.Validate();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.IntervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var checkpointService = scope.ServiceProvider.GetRequiredService<VendorAuditCheckpointService>();
                    await checkpointService.CreateCheckpointAsync(stoppingToken);
                    healthState.MarkSuccess(timeProvider.GetUtcNow());
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    healthState.MarkFailure(exception.GetType().Name);
                    logger.LogCritical(
                        "Vendor audit checkpoint verification/advance failed. FailureType={FailureType}.",
                        exception.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
    }
}
