namespace Faranou.Vendor.Persistence.Domain;

/// <summary>
/// Versioned public identity registered for a Vendor installation. Only public key material and
/// customer consent evidence are stored; the customer's private CNG key never leaves its host.
/// Retired rows are retained as identity history.
/// </summary>
public sealed class VendorInstallationIdentity
{
    public const string StateCurrent = "Current";
    public const string StateRetired = "Retired";
    public const string AssuranceHardwareTpm = "HardwareTpm";
    public const string AssuranceSoftwareProtected = "SoftwareProtected";

    public Guid Id { get; set; }
    public Guid InstallationId { get; set; }
    public string KeyId { get; set; } = string.Empty;
    public int KeyVersion { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string AssuranceLevel { get; set; } = string.Empty;
    public string OsInstallationMarkerDigest { get; set; } = string.Empty;
    public string PublicKeyFingerprint { get; set; } = string.Empty;
    public string PublicKeySubjectPublicKeyInfoBase64 { get; set; } = string.Empty;
    public DateTimeOffset? SoftwareProviderRiskAcceptedAtUtc { get; set; }
    public string? SoftwareProviderRiskAcceptedBy { get; set; }
    public string? SoftwareProviderConsentReceiptJson { get; set; }
    public string State { get; set; } = StateCurrent;
    public DateTimeOffset RegisteredAtUtc { get; set; }
    public DateTimeOffset? RetiredAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
