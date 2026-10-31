namespace Domain;

/// <summary>
/// Singleton durable state for the accepted offline revocation snapshot on this customer installation.
/// PendingEnvelopeJson and PendingExpectedGeneration implement crash recovery around the TPM NV counter.
/// </summary>
public sealed class CustomerOfflineRevocationState : Seedwork.Entity
{
    public static readonly Guid SingletonId = new("5f7f1e73-6c8a-4c45-8c6f-6a8b3a5d0001");

    public CustomerOfflineRevocationState()
    {
        Id = SingletonId;
    }

    public long Sequence { get; set; }
    public string? SnapshotDigest { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string RevokedLicenseIdsJson { get; set; } = "[]";
    public long AnchorGeneration { get; set; }
    public string? PendingEnvelopeJson { get; set; }
    public long? PendingExpectedGeneration { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
