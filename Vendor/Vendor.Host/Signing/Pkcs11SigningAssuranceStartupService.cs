using Faranou.Vendor.Persistence.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Emits the configured signing assurance at every host startup. Production assurance selection
/// is durably audited before the host starts serving; Software also requires explicit risk acceptance.
/// </summary>
public sealed class Pkcs11SigningAssuranceStartupService(
    IOptions<Pkcs11SigningOptions> options,
    IHostEnvironment environment,
    IServiceScopeFactory scopeFactory,
    ILogger<Pkcs11SigningAssuranceStartupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var configured = options.Value;
        configured.Validate(environment.IsProduction());

        logger.LogInformation(
            "Vendor signing assurance configured. AssuranceLevel={AssuranceLevel} Environment={EnvironmentName}.",
            configured.AssuranceLevel,
            environment.EnvironmentName);

        if (!environment.IsProduction())
        {
            return;
        }

        // Audit the selected Production assurance level before the host starts serving.
        // The Hardware event records configuration selection only; provider readiness remains
        // independently enforced by the signing health check and the PKCS#11 token validation.
        var actionCode = string.Equals(configured.AssuranceLevel, "Software", StringComparison.Ordinal)
            ? "vendor.signing.assurance.software-risk-accepted"
            : "vendor.signing.assurance.hardware-selected";

        await using var scope = scopeFactory.CreateAsyncScope();
        var auditWriter = scope.ServiceProvider.GetRequiredService<IVendorAuditWriter>();
        await auditWriter.AppendAsync(
            new VendorAuditWriteRequest(
                null,
                actionCode,
                "signing_assurance",
                null,
                null),
            cancellationToken);

        if (string.Equals(configured.AssuranceLevel, "Software", StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Vendor Production is running with explicitly accepted Software signing assurance.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
