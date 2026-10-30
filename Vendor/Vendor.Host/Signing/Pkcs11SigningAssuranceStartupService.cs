using Faranou.Vendor.Persistence.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Emits the configured signing assurance at every host startup. Production software assurance
/// requires explicit risk acceptance and a durable audit event before the host starts serving.
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

        if (!environment.IsProduction() ||
            !string.Equals(configured.AssuranceLevel, "Software", StringComparison.Ordinal))
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var auditWriter = scope.ServiceProvider.GetRequiredService<IVendorAuditWriter>();
        await auditWriter.AppendAsync(
            new VendorAuditWriteRequest(
                null,
                "vendor.signing.assurance.software-risk-accepted",
                "signing_assurance",
                null,
                null),
            cancellationToken);

        logger.LogWarning(
            "Vendor Production is running with explicitly accepted Software signing assurance.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
