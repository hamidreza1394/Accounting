using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Faranou.Licensing.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Provisioning;

/// <summary>
/// Resolves the company entitlement data a company-bound (schema v5) provisioning package is
/// validated against (Issue #392 / D-059 §14): the current accepted signed license of the bound
/// license ID on this installation. Company references are immutable across amendments, so the
/// current revision is authoritative for the entitled reference set; any resolution failure is
/// fail-closed.
/// </summary>
public static class CustomerCompanyEntitlementResolver
{
    /// <summary>
    /// Loads the payload of the current accepted license envelope for <paramref name="licenseId"/>.
    /// Returns null when no current envelope exists for this installation, the stored payload can
    /// no longer be parsed, or the payload does not belong to this installation or license.
    /// </summary>
    public static async Task<LicensePayload?> ResolveCurrentLicensePayloadAsync(
        Data.DatabaseContext database,
        string installationId,
        string licenseId,
        CancellationToken cancellationToken)
    {
        var installed = await database.InstalledLicenses.AsNoTracking()
            .Where(item => item.LicenseId == licenseId &&
                item.State == Domain.InstalledLicense.StateCurrent &&
                item.InstallationId == installationId)
            .OrderByDescending(item => item.Sequence)
            .FirstOrDefaultAsync(cancellationToken);
        if (installed is null)
            return null;

        if (!CanonicalJson.TryParsePayload(installed.PayloadJson, out var payload, out _) || payload is null)
            return null;

        return string.Equals(payload.InstallationId, installationId, StringComparison.Ordinal) &&
            string.Equals(payload.LicenseId, licenseId, StringComparison.Ordinal)
            ? payload
            : null;
    }

    /// <summary>
    /// True when the signed entitlement list of the license covers <paramref name="companyReference"/>.
    /// Entries without a reference are placeholders and never entitle anything; the comparison is
    /// ordinal and matches the binary-collated mapping column.
    /// </summary>
    public static bool IsReferenceEntitled(LicensePayload? license, string companyReference)
    {
        if (license is null)
            return false;

        return license.CompanyEntitlements?.Any(entitlement =>
            entitlement?.CompanyReference is not null &&
            string.Equals(entitlement.CompanyReference, companyReference, StringComparison.Ordinal)) == true;
    }

    /// <summary>
    /// The signed installation company cap, or null when the vendor signed no numeric cap. An
    /// absent cap is a legitimate signed state: the entitlement list stays the binding boundary.
    /// </summary>
    public static int? GetMaxCompanies(LicensePayload? license) =>
        license?.Limits?.MaxCompanies;
}
