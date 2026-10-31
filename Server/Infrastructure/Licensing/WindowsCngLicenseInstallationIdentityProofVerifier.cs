using System.Security.Cryptography;
using System.Runtime.Versioning;
using Faranou.Licensing.Contracts;
using Microsoft.Extensions.Options;

namespace Infrastructure.Licensing;

public enum InstallationIdentityProofStatus
{
    Valid = 1,
    InstallationChanged = 2,
    Unavailable = 3
}

public interface IInstallationIdentityProofVerifier
{
    Task<InstallationIdentityProofStatus> VerifyAsync(
        Domain.InstallationIdentity identity,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Revalidates the current Windows OS marker and proves possession of the already-provisioned
/// non-exportable CNG installation key. It never creates or repairs a missing key.
/// </summary>
public sealed class WindowsCngLicenseInstallationIdentityProofVerifier
    : IInstallationIdentityProofVerifier
{
    private readonly WindowsCngLicenseInstallationKeyOptions _options;

    public WindowsCngLicenseInstallationIdentityProofVerifier(
        IOptions<WindowsCngLicenseInstallationKeyOptions> options)
    {
        _options = options.Value;
    }

    public Task<InstallationIdentityProofStatus> VerifyAsync(
        Domain.InstallationIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
            return Task.FromResult(InstallationIdentityProofStatus.Unavailable);

        try
        {
            return Task.FromResult(VerifyWindows(identity));
        }
        catch (Exception exception) when (exception is CryptographicException or
            UnauthorizedAccessException or InvalidOperationException or
            ArgumentException or FormatException or PlatformNotSupportedException)
        {
            return Task.FromResult(InstallationIdentityProofStatus.Unavailable);
        }
    }

    [SupportedOSPlatform("windows")]
    private InstallationIdentityProofStatus VerifyWindows(Domain.InstallationIdentity identity)
    {
        var currentOsMarker = WindowsCngLicenseInstallationKeyProvisioner.GetOsInstallationMarkerDigest();
        if (!string.Equals(currentOsMarker, identity.OsInstallationMarkerDigest, StringComparison.Ordinal))
            return InstallationIdentityProofStatus.InstallationChanged;

        var softwareConsentRecorded =
            identity.AssuranceLevel == Domain.InstallationIdentity.AssuranceSoftwareProtected &&
            identity.SoftwareProviderRiskAcceptedAtUtc.HasValue &&
            !string.IsNullOrWhiteSpace(identity.SoftwareProviderRiskAcceptedBy);

        if (identity.AssuranceLevel == Domain.InstallationIdentity.AssuranceHardwareTpm &&
            _options.ProviderName != WindowsCngLicenseInstallationKeyProvisioner.TpmProviderName)
            return InstallationIdentityProofStatus.Unavailable;

        if (identity.AssuranceLevel == Domain.InstallationIdentity.AssuranceSoftwareProtected &&
            (_options.ProviderName != WindowsCngLicenseInstallationKeyProvisioner.SoftwareProviderName ||
             !softwareConsentRecorded))
            return InstallationIdentityProofStatus.Unavailable;

        if (identity.AssuranceLevel is not (
            Domain.InstallationIdentity.AssuranceHardwareTpm or
            Domain.InstallationIdentity.AssuranceSoftwareProtected) ||
            identity.ProviderName != _options.ProviderName ||
            identity.KeyVersion != _options.KeyVersion ||
            !_options.MachineKey)
            return InstallationIdentityProofStatus.Unavailable;

        var metadata = WindowsCngLicenseInstallationKeyProvisioner.ReadExisting(
            _options,
            identity.InstallationId,
            softwareConsentRecorded);

        if (!string.Equals(metadata.KeyId, identity.KeyId, StringComparison.Ordinal) ||
            !string.Equals(metadata.ProviderName, identity.ProviderName, StringComparison.Ordinal) ||
            !string.Equals(metadata.Assurance.ToString(), identity.AssuranceLevel, StringComparison.Ordinal) ||
            !string.Equals(metadata.PublicKeyFingerprint, identity.PublicKeyFingerprint, StringComparison.Ordinal) ||
            !string.Equals(metadata.OsInstallationMarkerDigest, identity.OsInstallationMarkerDigest, StringComparison.Ordinal) ||
            !CryptographicOperations.FixedTimeEquals(
                metadata.PublicKeySubjectPublicKeyInfo,
                System.Convert.FromBase64String(identity.PublicKeySubjectPublicKeyInfoBase64)))
            return InstallationIdentityProofStatus.InstallationChanged;

        var challenge = RandomNumberGenerator.GetBytes(32);
        var signature = WindowsCngLicenseInstallationKeyProvisioner.SignChallenge(
            _options,
            identity.InstallationId,
            challenge,
            softwareConsentRecorded);

        var storedPublicKey = System.Convert.FromBase64String(identity.PublicKeySubjectPublicKeyInfoBase64);
        using var verifier = ECDsa.Create();
        verifier.ImportSubjectPublicKeyInfo(storedPublicKey, out var bytesRead);
        if (bytesRead != storedPublicKey.Length ||
            !verifier.VerifyData(
                challenge,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            return InstallationIdentityProofStatus.Unavailable;

        return InstallationIdentityProofStatus.Valid;
    }
}
