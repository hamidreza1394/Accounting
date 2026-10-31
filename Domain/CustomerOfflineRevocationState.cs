namespace Domain;

/// <summary>
/// Singleton durable state for the accepted offline revocation snapshot on this customer installation.
/// PendingEnvelopeJson and PendingExpectedGeneration implement crash recovery around the TPM NV counter.
/// </summary>
public sealed class CustomerOfflineRevocationState : Seedwork.Entity
{
    public long Sequence { get; set; }
    public string? SnapshotDigest { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string RevokedLicenseIdsJson { get; set; } = "[]";
    public long AnchorGeneration { get; set; }
    public string? PendingEnvelopeJson { get; set; }
    public long? PendingExpectedGeneration { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
