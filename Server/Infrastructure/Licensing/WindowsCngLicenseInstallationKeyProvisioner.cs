using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Infrastructure.Licensing;

/// <summary>
/// Describes the protection level of the local installation proof key.
/// SoftwareProtected is not equivalent to a hardware TPM key.
/// </summary>
public enum LicenseInstallationKeyAssurance
{
    HardwareTpm = 1,
    SoftwareProtected = 2,
}

/// <summary>
/// Explicit Windows CNG provider selection for the customer installation proof key.
/// The default is TPM; selecting the software KSP requires an explicit per-operation consent.
/// </summary>
public sealed class WindowsCngLicenseInstallationKeyOptions
{
    public const string SectionName = "Licensing:InstallationIdentity:WindowsCng";

    public string ProviderName { get; set; } = "Microsoft Platform Crypto Provider";

    public int KeyVersion { get; set; } = 1;

    public bool MachineKey { get; set; } = true;
}

/// <summary>
/// Public, non-secret metadata returned by the one-time installation-key ceremony.
/// The private key is never returned or persisted in the application database.
/// </summary>
public sealed record WindowsCngLicenseInstallationKeyProvisioningResult(
    string KeyName,
    string KeyId,
    string ProviderName,
    LicenseInstallationKeyAssurance Assurance,
    byte[] PublicKeySubjectPublicKeyInfo,
    string PublicKeyFingerprint,
    string OsInstallationMarkerDigest,
    bool SoftwareProviderRiskAccepted);

/// <summary>
/// Provisions and uses a non-exportable ECDSA P-256 installation proof key in an explicitly
/// selected Windows CNG KSP. This provider never falls back between TPM and software KSPs.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowsCngLicenseInstallationKeyProvisioner
{
    public const string TpmProviderName = "Microsoft Platform Crypto Provider";
    public const string SoftwareProviderName = "Microsoft Software Key Storage Provider";

    public static WindowsCngLicenseInstallationKeyProvisioningResult Create(
        WindowsCngLicenseInstallationKeyOptions options,
        string installationId,
        bool confirmSoftwareProviderRisk)
    {
        ArgumentNullException.ThrowIfNull(options);
        EnsureWindows();
        var assurance = ValidateOptions(options, installationId, confirmSoftwareProviderRisk);

        var keyName = GetKeyName(installationId, options.KeyVersion);
        var parameters = new CngKeyCreationParameters
        {
            Provider = new CngProvider(options.ProviderName),
            KeyUsage = CngKeyUsages.Signing,
            ExportPolicy = CngExportPolicies.None,
            KeyCreationOptions = options.MachineKey
                ? CngKeyCreationOptions.MachineKey
                : CngKeyCreationOptions.None,
        };

        // A key name is immutable for a version. Re-provisioning must use the existing key
        // or an explicitly versioned rotation; never overwrite an installation identity key.
        using var key = CngKey.Create(CngAlgorithm.ECDsaP256, keyName, parameters);
        EnsureKeyContract(key);
        using var signer = new ECDsaCng(key);
        var publicKey = signer.ExportSubjectPublicKeyInfo();

        return CreateResult(
            installationId,
            options,
            assurance,
            keyName,
            publicKey,
            softwareProviderRiskAccepted: assurance == LicenseInstallationKeyAssurance.SoftwareProtected);
    }

    /// <summary>
    /// Reads metadata for an already-provisioned key. The consent argument must come from durable, audited installation state. It never creates or repairs a missing key.
    /// </summary>
    public static WindowsCngLicenseInstallationKeyProvisioningResult ReadExisting(
        WindowsCngLicenseInstallationKeyOptions options,
        string installationId,
        bool softwareProviderRiskConsentRecorded)
    {
        ArgumentNullException.ThrowIfNull(options);
        EnsureWindows();
        var assurance = ValidateOptions(options, installationId, softwareProviderRiskConsentRecorded);
        var keyName = GetKeyName(installationId, options.KeyVersion);
        using var key = CngKey.Open(
            keyName,
            new CngProvider(options.ProviderName),
            options.MachineKey ? CngKeyOpenOptions.MachineKey : CngKeyOpenOptions.None);
        EnsureKeyContract(key);
        using var signer = new ECDsaCng(key);
        return CreateResult(
            installationId,
            options,
            assurance,
            keyName,
            signer.ExportSubjectPublicKeyInfo(),
            softwareProviderRiskAccepted: assurance == LicenseInstallationKeyAssurance.SoftwareProtected);
    }

    /// <summary>
    /// Proves possession of the existing private key by signing caller-provided challenge bytes.
    /// It does not provision a replacement key when the configured key is absent.
    /// </summary>
    public static byte[] SignChallenge(
        WindowsCngLicenseInstallationKeyOptions options,
        string installationId,
        ReadOnlySpan<byte> challenge,
        bool softwareProviderRiskConsentRecorded)
    {
        ArgumentNullException.ThrowIfNull(options);
        EnsureWindows();
        if (challenge.Length is < 32 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(challenge), "The installation proof challenge must contain 32 to 4096 bytes.");

        _ = ValidateOptions(options, installationId, softwareProviderRiskConsentRecorded);
        using var key = CngKey.Open(
            GetKeyName(installationId, options.KeyVersion),
            new CngProvider(options.ProviderName),
            options.MachineKey ? CngKeyOpenOptions.MachineKey : CngKeyOpenOptions.None);
        EnsureKeyContract(key);
        using var signer = new ECDsaCng(key);
        return signer.SignData(
            challenge,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    public static string GetKeyName(string installationId, int keyVersion)
    {
        ValidateInstallationId(installationId, keyVersion);
        var installationDigest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(installationId))).ToLowerInvariant();
        return "Faranou.License.Installation." + installationDigest + ".v" +
            keyVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string GetKeyId(string installationId, int keyVersion)
    {
        ValidateInstallationId(installationId, keyVersion);
        var installationDigest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(installationId))).ToLowerInvariant();
        return "license-install-" + installationDigest[..24] + "-v" +
            keyVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string GetOsInstallationMarkerDigest()
    {
        EnsureWindows();
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", writable: false);
        var machineGuid = key?.GetValue("MachineGuid") as string;
        if (string.IsNullOrWhiteSpace(machineGuid))
            throw new CryptographicException("The Windows OS installation marker is unavailable.");

        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(machineGuid.Trim().ToUpperInvariant()))).ToLowerInvariant();
    }

    private static LicenseInstallationKeyAssurance ValidateOptions(
        WindowsCngLicenseInstallationKeyOptions options,
        string installationId,
        bool confirmSoftwareProviderRisk)
    {
        ValidateInstallationId(installationId, options.KeyVersion);
        if (options.ProviderName == TpmProviderName)
            return LicenseInstallationKeyAssurance.HardwareTpm;

        if (options.ProviderName == SoftwareProviderName)
        {
            // The Microsoft Software KSP protects machine-scoped key material through the Windows
            // software key store/DPAPI boundary. This is software protection, not TPM assurance.
            if (!confirmSoftwareProviderRisk)
                throw new InvalidOperationException(
                    "Software CNG protection requires explicit operator confirmation; no automatic fallback is permitted.");
            return LicenseInstallationKeyAssurance.SoftwareProtected;
        }

        throw new ArgumentException("The configured Windows CNG provider is not approved.", nameof(options));
    }

    private static void ValidateInstallationId(string installationId, int keyVersion)
    {
        if (string.IsNullOrWhiteSpace(installationId) ||
            installationId.Length > 128 ||
            !Regex.IsMatch(installationId, "^[A-Za-z0-9._-]{8,128}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("The configured installation identifier is invalid.", nameof(installationId));

        if (keyVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(keyVersion), "Key versions must be positive.");
    }

    private static WindowsCngLicenseInstallationKeyProvisioningResult CreateResult(
        string installationId,
        WindowsCngLicenseInstallationKeyOptions options,
        LicenseInstallationKeyAssurance assurance,
        string keyName,
        byte[] publicKey,
        bool softwareProviderRiskAccepted)
    {
        var publicKeyFingerprint = Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant();
        return new WindowsCngLicenseInstallationKeyProvisioningResult(
            keyName,
            GetKeyId(installationId, options.KeyVersion),
            options.ProviderName,
            assurance,
            publicKey,
            publicKeyFingerprint,
            GetOsInstallationMarkerDigest(),
            softwareProviderRiskAccepted);
    }

    private static void EnsureKeyContract(CngKey key)
    {
        if (key.Algorithm != CngAlgorithm.ECDsaP256 ||
            key.KeySize != 256 ||
            key.ExportPolicy != CngExportPolicies.None ||
            key.KeyUsage != CngKeyUsages.Signing)
            throw new CryptographicException("The configured installation proof key does not satisfy the required key policy.");
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Customer installation identity keys require Windows CNG.");
    }
}
