using Faranou.Vendor.Host.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorAuditCheckpointHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_IsUnhealthyUntilAFullVerificationSucceeds()
    {
        var now = DateTimeOffset.Parse("2026-10-31T12:00:00Z");
        var time = new ManualTimeProvider(now);
        var state = new VendorAuditCheckpointHealthState();
        var check = CreateCheck(state, time);

        var initial = await check.CheckHealthAsync(null!);
        Assert.Equal(HealthStatus.Unhealthy, initial.Status);

        state.MarkSuccess(now);
        var healthy = await check.CheckHealthAsync(null!);
        Assert.Equal(HealthStatus.Healthy, healthy.Status);

        state.MarkFailure("IOException");
        var failed = await check.CheckHealthAsync(null!);
        Assert.Equal(HealthStatus.Unhealthy, failed.Status);
        Assert.DoesNotContain("private-connection-detail", failed.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_IsUnhealthyWhenLastVerificationIsStale()
    {
        var verifiedAt = DateTimeOffset.Parse("2026-10-31T12:00:00Z");
        var time = new ManualTimeProvider(verifiedAt);
        var state = new VendorAuditCheckpointHealthState();
        state.MarkSuccess(verifiedAt);

        time.SetUtcNow(verifiedAt.AddMinutes(16));
        var check = CreateCheck(state, time);

        var result = await check.CheckHealthAsync(null!);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public void Options_RejectMaximumStalenessShorterThanTwoIntervals()
    {
        var options = new VendorAuditCheckpointOptions
        {
            IntervalSeconds = 300,
            MaximumStalenessSeconds = 500
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    private static VendorAuditCheckpointHealthCheck CreateCheck(
        VendorAuditCheckpointHealthState state,
        ManualTimeProvider time) =>
        new(
            state,
            Options.Create(new VendorAuditCheckpointOptions
            {
                IntervalSeconds = 300,
                MaximumStalenessSeconds = 900
            }),
            time);

    private sealed class ManualTimeProvider(DateTimeOffset current) : TimeProvider
    {
        private DateTimeOffset _current = current;

        public override DateTimeOffset GetUtcNow() => _current;

        public void SetUtcNow(DateTimeOffset value) => _current = value;
    }
}
