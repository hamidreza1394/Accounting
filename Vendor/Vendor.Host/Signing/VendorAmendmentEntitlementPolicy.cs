using Faranou.Licensing.Contracts;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Final-payload policy gate for license amendments, decided exclusively with the pure
/// <see cref="EntitlementPolicyEngine"/> before any signature is produced. An amendment is
/// monotonic: it must not revoke any entitlement the current signature-verified payload grants,
/// and every entitlement the candidate carries must itself be policy-decidable and allowed.
///
/// Deliberately out of scope here (existing contracts, not new rules): feature grant values —
/// a disabled feature is a legitimate pre-grant state until a later amendment enables it, so
/// <see cref="EntitlementPolicyEngine.EvaluateFeature"/> grant semantics must not run at payload
/// time; seat capacity needs the customer-side authoritative membership count (D-054), which the
/// Vendor does not own; MaxCompanies increase semantics belong to the amendment builder. Structural
/// and successor rules remain the builder's own validations — this gate is the engine-level
/// enforcement point in front of the signer.
/// </summary>
public static class VendorAmendmentEntitlementPolicy
{
    public const string PolicyViolationCode = "amendment_payload_policy_invalid";

    /// <summary>
    /// Returns null when the candidate keeps every currently granted entitlement and only carries
    /// engine-allowed entitlements; otherwise <see cref="PolicyViolationCode"/>.
    /// </summary>
    public static string? ValidateSuccessorKeepsEntitlements(
        LicensePayload currentPayload,
        LicensePayload candidatePayload)
    {
        ArgumentNullException.ThrowIfNull(currentPayload);
        ArgumentNullException.ThrowIfNull(candidatePayload);

        if (candidatePayload.FormatVersion == LicensePayload.CurrentFormatVersion)
        {
            if (ValidateCompanyScoped(candidatePayload) is { } candidateError)
                return candidateError;

            // Company references were first introduced by payload v2; there is no defined mapping
            // from v2 grants back to v1, so a v2 → v1 step cannot be proven non-revoking.
            if (currentPayload.FormatVersion == LicensePayload.CurrentFormatVersion &&
                RevokesCompanyEntitlements(currentPayload, candidatePayload))
                return PolicyViolationCode;

            return null;
        }

        if (currentPayload.FormatVersion == LicensePayload.CurrentFormatVersion)
            return PolicyViolationCode; // unmappable downgrade; fail closed

        return ValidateLegacy(currentPayload, candidatePayload);
    }

    private static string? ValidateCompanyScoped(LicensePayload candidatePayload)
    {
        foreach (var company in candidatePayload.CompanyEntitlements ?? [])
        {
            if (company is null ||
                EntitlementPolicyEngine.ResolveCompany(candidatePayload, company.CompanyReference) is null)
                return PolicyViolationCode;

            foreach (var module in company.Modules ?? [])
                if (!EntitlementPolicyEngine.EvaluateModule(candidatePayload, company.CompanyReference, module).Allowed)
                    return PolicyViolationCode;
        }

        return null;
    }

    private static bool RevokesCompanyEntitlements(
        LicensePayload currentPayload,
        LicensePayload candidatePayload)
    {
        foreach (var company in currentPayload.CompanyEntitlements ?? [])
        {
            if (company is null ||
                EntitlementPolicyEngine.ResolveCompany(candidatePayload, company.CompanyReference) is null)
                return true;

            foreach (var module in company.Modules ?? [])
                if (!EntitlementPolicyEngine.EvaluateModule(candidatePayload, company.CompanyReference, module).Allowed)
                    return true;
        }

        return false;
    }

    private static string? ValidateLegacy(LicensePayload currentPayload, LicensePayload candidatePayload)
    {
        // No currently granted installation-wide subsystem may disappear from the candidate.
        foreach (var subsystem in currentPayload.Limits.Subsystems)
            if (!EntitlementPolicyEngine.EvaluateLegacySubsystem(candidatePayload, subsystem).Allowed)
                return PolicyViolationCode;

        // Every subsystem the candidate carries must itself be an allowed legacy grant.
        foreach (var subsystem in candidatePayload.Limits.Subsystems)
            if (!EntitlementPolicyEngine.EvaluateLegacySubsystem(candidatePayload, subsystem).Allowed)
                return PolicyViolationCode;

        return null;
    }
}
