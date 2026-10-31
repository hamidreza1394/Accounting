namespace Faranou.Vendor.Persistence.Domain;

/// <summary>
/// Immutable Vendor-control-plane history of a signed offline revocation snapshot.
/// The serialized envelope is retained exactly as produced; it contains no private key material.
/// </summary>
public sealed class VendorOfflineRevocationSnapshot
{
    public Guid Id { get; set; }
    public long Sequence { get; set; }
    public string ListId { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public string EnvelopeJson { get; set; } = string.Empty;
    public string PayloadSha256 { get; set; } = string.Empty;
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>
/// Singleton, Vendor-owned monotonic sequence allocator state for offline revocation snapshots.
/// Sequence increments must be performed atomically inside the same transaction as snapshot persistence.
/// </summary>
public sealed class VendorOfflineRevocationSequence
{
    public int Id { get; set; } = 1;
    public long LastSequence { get; set; }
}
