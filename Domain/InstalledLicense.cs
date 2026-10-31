using System;

namespace Domain;

/// <summary>
/// An immutable, validated license envelope installed for a customer installation.
/// Superseded envelopes remain available for audit and rollback/downgrade detection.
/// </summary>
public sealed class InstalledLicense : Seedwork.Entity
{
    public const string StateCurrent = "Current";
    public const string StateSuperseded = "Superseded";

    public Guid InstallationIdentityId { get; set; }
    public string InstallationId { get; set; } = string.Empty;
    public string LicenseId { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public int FormatVersion { get; set; }
    public string Kid { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string SignatureBase64 { get; set; } = string.Empty;
    public string EnvelopeJson { get; set; } = string.Empty;
    public string PayloadDigest { get; set; } = string.Empty;
    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;
    public string State { get; set; } = StateCurrent;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
