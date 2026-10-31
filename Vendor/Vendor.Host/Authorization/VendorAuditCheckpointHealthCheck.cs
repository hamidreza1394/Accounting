using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Reports only whether a recent full audit-chain/checkpoint verification succeeded.
/// No endpoint, credential, hash or event content is exposed.
/// </summary>
public sealed class VendorAuditCheckpointHealthCheck(
    VendorAuditCheckpointHealthState healthState,
    IOptions<VendorAuditCheckpointOptions> options,
    TimeProvider timeProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        settings.Validate();
        var snapshot = healthState.Snapshot();

        if (snapshot.LastFailureType is not null)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Independent audit checkpoint verification failed."));
        }

        if (snapshot.LastSuccessUtc is null)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "No successful independent audit checkpoint verification has completed."));
        }

        if (timeProvider.GetUtcNow() - snapshot.LastSuccessUtc.Value >
            TimeSpan.FromSeconds(settings.MaximumStalenessSeconds))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Independent audit checkpoint verification is stale."));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "Independent audit checkpoint verification is current."));
    }
}
