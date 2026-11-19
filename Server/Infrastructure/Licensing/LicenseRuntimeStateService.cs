using Infrastructure.Provisioning;
using Faranou.Licensing.Contracts;
using Microsoft.Extensions.Options;

namespace Infrastructure.Licensing;

/// <summary>
/// Server-side projection of the currently installed license state. The validated payload is
/// carried only after full signature/trust/binding/revocation validation; rejected results never
/// expose payload content. Decisions come from the signed payload, never from client input.
/// </summary>
public sealed record LicenseRuntimeStateSnapshot(
    LicenseRuntimeState State,
    string DeploymentMode,
    LicenseStatus? Status,
    string? ErrorCode,
    LicensePayload? ValidatedPayload,
    DateTimeOffset ResolvedAtUtc)
{
    /// <summary>Metadata-only text for logs; never includes payload or entitlement details.</summary>
    public override string ToString() =>
        $"LicenseRuntimeStateSnapshot {{ State = {State}, DeploymentMode = {DeploymentMode}, Status = {Status}, ErrorCode = {ErrorCode} }}";
}

public interface ILicenseRuntimeStateService
{
    Task<LicenseRuntimeStateSnapshot> ResolveAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Maps the revocation-aware runtime evaluation (D-058/D-059) onto the deployment state machine
/// of D-060 §1-6 and D-098. Re-resolves on every call; there is deliberately no cache that could
/// outlive a license import, revocation or expiry change.
/// </summary>
public sealed class LicenseRuntimeStateService(
    ICustomerLicenseRuntimeEvaluator evaluator,
    IOptions<CustomerProvisioningSettings> settings,
    IOptions<LicenseRuntimeGateOptions> options) : ILicenseRuntimeStateService
{
    public async Task<LicenseRuntimeStateSnapshot> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var deploymentMode = options.Value.NormalisedDeploymentMode;
        var installationId = settings.Value.InstallationId;
        var result = string.IsNullOrWhiteSpace(installationId)
            ? CustomerLicenseRuntimeResult.Rejected("license_missing")
            : await evaluator.EvaluateCurrentAsync(installationId, cancellationToken);

        if (result.IsUsable)
        {
            // Valid/ExpiringSoon are normal operation; Restricted is the expiry state of
            // D-059 §7 whose concrete behaviour differs per deployment mode (D-098).
            var state = result.Status is LicenseStatus.Restricted
                ? LicenseRuntimeState.Restricted
                : LicenseRuntimeState.Active;
            return new LicenseRuntimeStateSnapshot(
                state,
                deploymentMode,
                result.Status,
                null,
                result.ValidatedPayload,
                DateTimeOffset.UtcNow);
        }

        // A missing envelope (or a missing installation identity) is Unlicensed; any other
        // rejection is a fail-closed Invalid state that requires a corrected license.
        var rejectedState = result.ErrorCode is "license_missing" or "installation_id_invalid"
            ? LicenseRuntimeState.Unlicensed
            : LicenseRuntimeState.Invalid;
        return new LicenseRuntimeStateSnapshot(
            rejectedState,
            deploymentMode,
            result.Status,
            result.ErrorCode,
            null,
            DateTimeOffset.UtcNow);
    }
}
