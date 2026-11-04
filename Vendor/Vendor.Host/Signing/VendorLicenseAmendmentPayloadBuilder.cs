using Faranou.Licensing.Contracts;
using Faranou.Vendor.Persistence.Domain;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Builds an installation-level maxCompanies increase from an already signature-verified current
/// payload. This pure builder does not authenticate the source envelope, record a purchase,
/// request approval, sign, persist, or advance the durable sequence.
/// </summary>
public static class VendorLicenseAmendmentPayloadBuilder
{
    public static bool TryBuildMaxCompaniesIncrease(
        LicensePayload currentPayload,
        VendorLicense license,
        VendorInstallation installation,
        VendorCustomer customer,
        int newMaxCompanies,
        DateTimeOffset issuedAtUtc,
        out LicensePayload? amendmentPayload,
        out string? errorCode)
    {
        ArgumentNullException.ThrowIfNull(currentPayload);
        ArgumentNullException.ThrowIfNull(license);
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(customer);

        amendmentPayload = null;
        errorCode = null;

        if (license.Id == Guid.Empty || installation.Id == Guid.Empty || customer.Id == Guid.Empty ||
            license.InstallationId != installation.Id ||
            license.CustomerId != customer.Id ||
            installation.CustomerId != customer.Id ||
            !string.Equals(currentPayload.LicenseId, license.Id.ToString("D"), StringComparison.Ordinal) ||
            !string.Equals(currentPayload.InstallationId, installation.Id.ToString("D"), StringComparison.Ordinal))
        {
            return Fail("license_installation_mismatch", out errorCode);
        }

        if (!string.Equals(customer.Status, "Active", StringComparison.Ordinal) ||
            !string.Equals(installation.Status, "Active", StringComparison.Ordinal) ||
            !string.Equals(license.Status, "Issued", StringComparison.Ordinal))
        {
            return Fail("license_not_amendable", out errorCode);
        }

        if (StructuralValidation.ValidatePayload(currentPayload).Count > 0)
            return Fail("current_license_payload_invalid", out errorCode);

        if (license.LastIssuedSequence != currentPayload.Sequence)
            return Fail("license_sequence_stale", out errorCode);

        if (!string.Equals(license.ProductCode, currentPayload.ProductCode, StringComparison.Ordinal) ||
            !string.Equals(license.LicenseKind, currentPayload.LicenseKind, StringComparison.Ordinal) ||
            license.GraceDays != currentPayload.GraceDays ||
            license.MaxCompanies != currentPayload.Limits.MaxCompanies ||
            license.ValidFromUtc.ToUniversalTime() != currentPayload.NotBeforeUtc?.ToUniversalTime() ||
            license.ValidUntilUtc?.ToUniversalTime() != currentPayload.ExpiresAtUtc?.ToUniversalTime())
        {
            return Fail("license_metadata_stale", out errorCode);
        }

        if (currentPayload.Limits.MaxCompanies is not int currentMaxCompanies)
            return Fail("current_company_limit_missing", out errorCode);

        if (newMaxCompanies <= currentMaxCompanies)
            return Fail("company_limit_not_increased", out errorCode);

        if (currentPayload.Sequence == int.MaxValue)
            return Fail("license_sequence_exhausted", out errorCode);

        if (issuedAtUtc == default ||
            issuedAtUtc.ToUniversalTime() < currentPayload.IssuedAtUtc.ToUniversalTime())
        {
            return Fail("amendment_issued_at_invalid", out errorCode);
        }

        var candidate = currentPayload with
        {
            IssuedAtUtc = issuedAtUtc.ToUniversalTime(),
            Sequence = currentPayload.Sequence + 1,
            Limits = currentPayload.Limits with { MaxCompanies = newMaxCompanies }
        };

        if (StructuralValidation.ValidatePayload(candidate).Count > 0 ||
            !LicenseRevisionRules.TryValidateSuccessor(currentPayload, candidate, out _))
        {
            return Fail("amendment_payload_invalid", out errorCode);
        }

        amendmentPayload = candidate;
        return true;
    }

    private static bool Fail(string code, out string? errorCode)
    {
        errorCode = code;
        return false;
    }
}
