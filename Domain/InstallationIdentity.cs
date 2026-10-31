using System;

namespace Domain;

/// <summary>
/// Versioned public identity of a customer installation. Historical rows are retained
/// when an OS reinstall or controlled re-enrollment requires a new proof key.
/// The private CNG key is never stored in this entity or in the customer database.
/// </summary>
public sealed class InstallationIdentity : Seedwork.Entity
{
    public const string StateCurrent = "Current";
    public const string StateRetired = "Retired";
    public const string StateReactivationRequired = "ReactivationRequired";

    public const string AssuranceHardwareTpm = "HardwareTpm";
    public const string AssuranceSoftwareProtected = "SoftwareProtected";

    public string InstallationId { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public int KeyVersion { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string AssuranceLevel { get; set; } = string.Empty;
    public string OsInstallationMarkerDigest { get; set; } = string.Empty;
    public string PublicKeyFingerprint { get; set; } = string.Empty;
    public string PublicKeySubjectPublicKeyInfoBase64 { get; set; } = string.Empty;
    public DateTime? SoftwareProviderRiskAcceptedAtUtc { get; set; }
    public string? SoftwareProviderRiskAcceptedBy { get; set; }
    public string State { get; set; } = StateCurrent;
    public DateTime ProvisionedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastVerifiedAtUtc { get; set; }
    public DateTime? RetiredAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
