namespace Faranou.Vendor.Persistence.Domain;

/// <summary>
/// Immutable Vendor-control-plane copy of a signed offline-revocation snapshot.
/// It contains the complete signed envelope and never references a customer database.
/// </summary>
public sealed class VendorOfflineRevocationSnapshot
{
    public Guid Id { get; set; }
    public long Sequence { get; set; }
    public string KeyId { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public string EnvelopeJson { get; set; } = string.Empty;
    public string PayloadSha256 { get; set; } = string.Empty;
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
