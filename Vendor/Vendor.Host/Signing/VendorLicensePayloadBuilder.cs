using System.Security.Cryptography;
using System.Text;
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

        if (!IsCurrentInstallationIdentity(installationIdentity, installation.Id, issuedAtUtc))
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

    private static bool IsCurrentInstallationIdentity(
        VendorInstallationIdentity identity,
        Guid installationId,
        DateTimeOffset issuedAtUtc)
    {
        if (identity.InstallationId != installationId ||
            identity.State != VendorInstallationIdentity.StateCurrent ||
            identity.KeyVersion < 1 ||
            !IsLowerHexSha256(identity.OsInstallationMarkerDigest) ||
            !IsLowerHexSha256(identity.PublicKeyFingerprint))
            return false;

        var expectedProvider = identity.AssuranceLevel switch
        {
            VendorInstallationIdentity.AssuranceHardwareTpm => "Microsoft Platform Crypto Provider",
            VendorInstallationIdentity.AssuranceSoftwareProtected => InstallationKeyConsentReceiptVerifier.SoftwareProviderName,
            _ => null
        };
        if (expectedProvider is null || !string.Equals(identity.ProviderName, expectedProvider, StringComparison.Ordinal))
            return false;

        var expectedKeyIdHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(installationId.ToString("D")))).ToLowerInvariant()[..24];
        if (!string.Equals(identity.KeyId, $"license-install-{expectedKeyIdHash}-v{identity.KeyVersion}", StringComparison.Ordinal))
            return false;

        byte[] publicKeyBytes;
        try
        {
            publicKeyBytes = Convert.FromBase64String(identity.PublicKeySubjectPublicKeyInfoBase64);
            if (!string.Equals(Convert.ToBase64String(publicKeyBytes), identity.PublicKeySubjectPublicKeyInfoBase64, StringComparison.Ordinal))
                return false;
            using var publicKey = ECDsa.Create();
            publicKey.ImportSubjectPublicKeyInfo(publicKeyBytes, out var bytesRead);
            if (bytesRead != publicKeyBytes.Length || publicKey.KeySize != 256)
                return false;
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or ArgumentException)
        {
            return false;
        }

        var actualFingerprint = Convert.ToHexString(SHA256.HashData(publicKeyBytes)).ToLowerInvariant();
        if (!string.Equals(actualFingerprint, identity.PublicKeyFingerprint, StringComparison.Ordinal))
            return false;

        if (identity.AssuranceLevel == VendorInstallationIdentity.AssuranceHardwareTpm)
        {
            return identity.SoftwareProviderRiskAcceptedAtUtc is null &&
                identity.SoftwareProviderRiskAcceptedBy is null &&
                identity.SoftwareProviderConsentReceiptJson is null;
        }

        if (string.IsNullOrWhiteSpace(identity.SoftwareProviderConsentReceiptJson) ||
            identity.SoftwareProviderRiskAcceptedAtUtc is null ||
            string.IsNullOrWhiteSpace(identity.SoftwareProviderRiskAcceptedBy))
            return false;

        InstallationKeyConsentReceipt? receipt;
        try
        {
            receipt = JsonSerializer.Deserialize<InstallationKeyConsentReceipt>(
                identity.SoftwareProviderConsentReceiptJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return false;
        }

        return receipt is not null &&
            receipt.AcceptedAtUtc == identity.SoftwareProviderRiskAcceptedAtUtc.Value &&
            string.Equals(receipt.AcceptedBy, identity.SoftwareProviderRiskAcceptedBy, StringComparison.Ordinal) &&
            InstallationKeyConsentReceiptVerifier.TryVerify(
                receipt, publicKeyBytes, installationId.ToString("D"), identity.KeyId,
                identity.PublicKeyFingerprint, issuedAtUtc, out _);
    }

    private static bool IsLowerHexSha256(string? value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool Fail(string code, out string? errorCode)
    {
        errorCode = code;
        return false;
    }
}
