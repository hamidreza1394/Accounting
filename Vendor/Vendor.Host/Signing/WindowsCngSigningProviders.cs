using System.Diagnostics;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Persistence.Auditing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Metadata-only audit and structured logging shared by the purpose-specific Windows CNG signing
/// providers. A signature is never returned when its audit event cannot be persisted.
/// </summary>
public static class VendorSigningAuditExecution
{
    public static async ValueTask<LicenseSigningResult> SignWithAuditAsync(
        Func<ValueTask<LicenseSigningResult>> signingOperation,
        IVendorAuditWriter auditWriter,
        ILogger logger,
        string purpose,
        string? keyId)
    {
        var auditKeyId = string.IsNullOrWhiteSpace(keyId) ? "unconfigured-v0" : keyId;
        var result = await signingOperation().ConfigureAwait(false);
        var outcome = result.Succeeded ? "succeeded" : "failed";

        try
        {
            await auditWriter.AppendAsync(
                new VendorAuditWriteRequest(
                    null,
                    $"vendor.signing.{purpose}.{auditKeyId}.{outcome}",
                    "signing_operation",
                    null,
                    Activity.Current?.TraceId.ToString()),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            logger.LogCritical(
                "Vendor signing audit append failed; signature result was discarded. Purpose={Purpose} KeyId={KeyId} FailureType={FailureType}.",
                purpose,
                auditKeyId,
                exception.GetType().Name);
            return LicenseSigningResult.Unavailable();
        }

        if (result.Succeeded)
        {
            logger.LogInformation(
                "Vendor signing operation completed. Purpose={Purpose} KeyId={KeyId} Result={Result}.",
                purpose,
                auditKeyId,
                outcome);
        }
        else
        {
            logger.LogWarning(
                "Vendor signing operation did not complete. Purpose={Purpose} KeyId={KeyId} Result={Result}.",
                purpose,
                auditKeyId,
                outcome);
        }

        return result;
    }
}

/// <summary>Windows CNG-backed Vendor license signer using its license-only certificate and KeyId.</summary>
public sealed class WindowsCngLicenseSigningProvider(
    IWindowsCngSigningModule module,
    IOptions<WindowsCngSigningOptions> options,
    IVendorAuditWriter auditWriter,
    ILogger<WindowsCngLicenseSigningProvider> logger) : ILicenseSigningProvider
{
    public ValueTask<LicenseSigningResult> SignAsync(
        ReadOnlyMemory<byte> canonicalPayload,
        CancellationToken cancellationToken = default) =>
        VendorSigningAuditExecution.SignWithAuditAsync(
            () => module.SignAsync(options.Value.LicenseKey, canonicalPayload, cancellationToken),
            auditWriter,
            logger,
            "license",
            options.Value.LicenseKey?.KeyId);
}

/// <summary>Windows CNG-backed Vendor provisioning-package signer using its package-only certificate and KeyId.</summary>
public sealed class WindowsCngProvisioningSigningProvider(
    IWindowsCngSigningModule module,
    IOptions<WindowsCngSigningOptions> options,
    IVendorAuditWriter auditWriter,
    ILogger<WindowsCngProvisioningSigningProvider> logger) : IProvisioningSigningProvider
{
    public ValueTask<LicenseSigningResult> SignAsync(
        ReadOnlyMemory<byte> canonicalPayload,
        CancellationToken cancellationToken = default) =>
        VendorSigningAuditExecution.SignWithAuditAsync(
            () => module.SignAsync(options.Value.ProvisioningKey, canonicalPayload, cancellationToken),
            auditWriter,
            logger,
            "provisioning",
            options.Value.ProvisioningKey?.KeyId);
}

/// <summary>Readiness probe for both purpose-separated Windows CNG certificates.</summary>
public sealed class WindowsCngSigningHealthCheck(
    IWindowsCngSigningModule module,
    IOptions<WindowsCngSigningOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var available = await module.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
        var data = new Dictionary<string, object>
        {
            ["assuranceLevel"] = WindowsCngSigningOptions.RequiredAssuranceLevel
        };
        return available
            ? HealthCheckResult.Healthy("Windows CNG signing provider is available.", data)
            : HealthCheckResult.Unhealthy("Windows CNG signing provider is unavailable.", data: data);
    }
}

/// <summary>
/// Emits the configured SoftwareProtected signing assurance at every host startup and auditedly
/// records the D-107 accepted risk in Production: a local administrator can misuse the signer
/// without extracting the key, so the host must be dedicated with restricted administrative access.
/// </summary>
public sealed class WindowsCngSigningAssuranceStartupService(
    IOptions<WindowsCngSigningOptions> options,
    IHostEnvironment environment,
    IServiceScopeFactory scopeFactory,
    ILogger<WindowsCngSigningAssuranceStartupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Validation failure is deliberately fatal: an explicitly enabled signer that cannot be
        // validated must fail startup instead of silently falling back to unavailable providers.
        options.Value.Validate(environment.IsProduction(), OperatingSystem.IsWindows());

        logger.LogInformation(
            "Vendor signing assurance configured. AssuranceLevel={AssuranceLevel} Provider={Provider} Environment={EnvironmentName}.",
            WindowsCngSigningOptions.RequiredAssuranceLevel,
            "WindowsCngCertificateStore",
            environment.EnvironmentName);

        if (!environment.IsProduction())
        {
            return;
        }

        logger.LogWarning(
            "Vendor Production runs with SoftwareProtected signing assurance (D-107). Local administrator access to this host can trigger signatures without extracting the key; the host must be dedicated and administratively restricted.");

        // Audit the Production assurance selection before the host starts serving. Provider
        // readiness remains independently enforced by the signing health check and signing calls.
        await using var scope = scopeFactory.CreateAsyncScope();
        var auditWriter = scope.ServiceProvider.GetRequiredService<IVendorAuditWriter>();
        await auditWriter.AppendAsync(
            new VendorAuditWriteRequest(
                null,
                "vendor.signing.assurance.software-protected-selected",
                "signing_assurance",
                null,
                null),
            cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
