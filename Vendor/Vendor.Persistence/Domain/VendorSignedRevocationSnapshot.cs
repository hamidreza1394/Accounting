namespace Faranou.Vendor.Persistence.Domain;

/// <summary>
/// Immutable Vendor-side record of a signed global offline-revocation snapshot. It contains only
/// public envelope data and references the maker-checker approval that caused the snapshot.
/// </summary>
public sealed class VendorSignedRevocationSnapshot
{
    public Guid Id { get; set; }
    public Guid ApprovalRequestId { get; set; }
    public Guid TriggeringLicenseId { get; set; }
    public long Sequence { get; set; }
    public string ListId { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public string EnvelopeJson { get; set; } = string.Empty;
    public string PayloadSha256 { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
