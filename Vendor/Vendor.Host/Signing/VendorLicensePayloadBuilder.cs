using System.Text.Json;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Persistence.Domain;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Builds the exact v2 payload from Vendor control-plane metadata and the current registered installation identity. This does not sign, persist,
/// or change license state; those actions remain gated behind the approval workflow.
/// </summary>
public static class VendorLicensePayloadBuilder
{
    public static bool TryBuild(
        VendorLicense license,
        VendorInstallation installation,
        VendorInstallationIdentity installationIdentity,
        DateTimeOffset issuedAtUtc,
        out LicensePayload? payload,
        out string? errorCode)
    {
        ArgumentNullException.ThrowIfNull(license);
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(installationIdentity);
        payload = null;
        errorCode = null;

        if (license.Id == Guid.Empty || installation.Id == Guid.Empty ||
            license.InstallationId != installation.Id)
            return Fail("license_installation_mismatch", out errorCode);

        if (license.Status != "Draft" ||
            installation.Status != "Active" ||
            license.CustomerId != installation.CustomerId)
            return Fail("license_not_issuable", out errorCode);

        if (installationIdentity.InstallationId != installation.Id ||
            installationIdentity.State != VendorInstallationIdentity.StateCurrent ||
            installationIdentity.KeyVersion < 1 ||
            !System.Text.RegularExpressions.Regex.IsMatch(
                installationIdentity.PublicKeyFingerprint, "^[a-f0-9]{64}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            return Fail("installation_identity_not_registered", out errorCode);

        if (string.IsNullOrWhiteSpace(license.ProductCode) ||
            license.ProductCode.Length > 64 ||
            string.IsNullOrWhiteSpace(license.LicenseNumber) ||
            license.GraceDays is < 0 or > LicensePayload.MaxGraceDaysCeiling ||
            license.LastIssuedSequence < 0 ||
            license.LastIssuedSequence == int.MaxValue)
            return Fail("license_metadata_invalid", out errorCode);

        if (license.LicenseKind == LicensePayload.KindPerpetual && license.ValidUntilUtc is not null ||
            license.LicenseKind == LicensePayload.KindTerm && license.ValidUntilUtc is null ||
            license.LicenseKind is not (LicensePayload.KindPerpetual or LicensePayload.KindTerm) ||
            license.ValidUntilUtc is not null && license.ValidUntilUtc <= license.ValidFromUtc)
            return Fail("license_term_invalid", out errorCode);

        string[] subsystems;
        try
        {
            subsystems = JsonSerializer.Deserialize<string[]>(license.SubsystemsJson) ?? [];
        }
        catch (JsonException)
        {
            return Fail("license_subsystems_invalid", out errorCode);
        }

        if (subsystems.Length > 16 ||
            subsystems.Any(code => string.IsNullOrWhiteSpace(code) || !LicenseSubsystemCodes.IsAllowed(code)) ||
            subsystems.Distinct(StringComparer.Ordinal).Count() != subsystems.Length)
            return Fail("license_subsystems_invalid", out errorCode);

        if (license.MaxCompanies is < 1)
            return Fail("license_company_limit_invalid", out errorCode);

        payload = new LicensePayload(
            FormatVersion: LicenseAlgorithms.CurrentFormatVersion,
            LicenseId: license.Id.ToString("D"),
            InstallationId: installation.Id.ToString("D"),
            ProductCode: license.ProductCode,
            LicenseKind: license.LicenseKind,
            IssuedAtUtc: issuedAtUtc.ToUniversalTime(),
            NotBeforeUtc: license.ValidFromUtc.ToUniversalTime(),
            ExpiresAtUtc: license.ValidUntilUtc?.ToUniversalTime(),
            GraceDays: license.GraceDays,
            Sequence: license.LastIssuedSequence + 1,
            Limits: new LicenseLimits(
                license.MaxCompanies,
                subsystems.OrderBy(code => code, StringComparer.Ordinal).ToArray()),
            InstallationKeyFingerprint: installationIdentity.PublicKeyFingerprint);

        return true;
    }

    private static bool Fail(string code, out string? errorCode)
    {
        errorCode = code;
        return false;
    }
}
