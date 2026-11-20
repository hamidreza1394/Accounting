using System.Text.RegularExpressions;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Configuration for the D-107 SoftwareProtected Vendor signer backed by the Windows Certificate
/// Store and CNG. The first version deliberately supports only the central License and
/// Provisioning/Amendment purposes; offline-revocation snapshot signing stays on a real
/// Production HSM per D-055 and is intentionally absent from this configuration.
/// </summary>
public sealed class WindowsCngSigningOptions
{
    private static readonly Regex VersionedKeyIdPattern = new(
        "^[a-z0-9][a-z0-9-]{0,49}-v[1-9][0-9]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ThumbprintPattern = new(
        "^[0-9a-fA-F]{40}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Fixed assurance level of this provider; it never claims hardware assurance.</summary>
    public const string RequiredAssuranceLevel = "SoftwareProtected";

    public string AssuranceLevel { get; set; } = string.Empty;
    public string StoreLocation { get; set; } = "LocalMachine";
    public string StoreName { get; set; } = "My";
    public WindowsCngSigningKeyOptions LicenseKey { get; set; } = new();
    public WindowsCngSigningKeyOptions ProvisioningKey { get; set; } = new();

    /// <summary>
    /// Validates non-secret configuration without exposing certificate material in exceptions or logs.
    /// Production requires the machine certificate store (MachineKey per D-107); non-Windows hosts
    /// must never enable this provider (D-107: no Linux signer is supported in the first version).
    /// </summary>
    public void Validate(bool isProduction, bool isWindowsHost)
    {
        if (!isWindowsHost)
        {
            throw new InvalidOperationException(
                "The Windows CNG certificate-store signer requires a Windows host (D-107).");
        }

        if (!string.Equals(AssuranceLevel, RequiredAssuranceLevel, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Windows CNG signing assurance must be explicitly configured as SoftwareProtected.");
        }

        if (!string.Equals(StoreName, "My", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Windows CNG signing requires the personal certificate store (My).");
        }

        if (!string.Equals(StoreLocation, "LocalMachine", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(StoreLocation, "CurrentUser", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Windows CNG signing store location must be LocalMachine or CurrentUser.");
        }

        if (isProduction && !string.Equals(StoreLocation, "LocalMachine", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Production Windows CNG signing requires the LocalMachine (MachineKey) certificate store (D-107).");
        }

        ValidateKey(LicenseKey);
        ValidateKey(ProvisioningKey);

        if (string.Equals(LicenseKey.KeyId, ProvisioningKey.KeyId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("License and provisioning signing keys must have distinct versioned KeyIds.");
        }

        if (string.Equals(
                LicenseKey.Thumbprint,
                ProvisioningKey.Thumbprint,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("License and provisioning signing keys must use distinct certificates.");
        }
    }

    private void ValidateKey(WindowsCngSigningKeyOptions? key)
    {
        if (key is null)
        {
            throw new InvalidOperationException("Separate license and provisioning signing keys are required.");
        }

        if (key.KeyId is null || !VersionedKeyIdPattern.IsMatch(key.KeyId))
        {
            throw new InvalidOperationException(
                "Windows CNG signing KeyId must be a unique, versioned lower-case identifier.");
        }

        if (key.Thumbprint is null || !ThumbprintPattern.IsMatch(key.Thumbprint))
        {
            throw new InvalidOperationException("Windows CNG signing certificate thumbprint must be a 40-character hex value.");
        }
    }

    /// <summary>Thumbprint normalised for store lookups; input stays unmodified.</summary>
    public string ThumbprintNormalised(WindowsCngSigningKeyOptions key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key?.Thumbprint);
        return key.Thumbprint.ToUpperInvariant();
    }
}

/// <summary>Certificate selected for one signing purpose; identified by thumbprint and versioned KeyId.</summary>
public sealed class WindowsCngSigningKeyOptions
{
    public string KeyId { get; set; } = string.Empty;
    public string Thumbprint { get; set; } = string.Empty;
}
