namespace Faranou.Vendor.Persistence.Domain;

/// <summary>
/// Durable Vendor-control-plane copy of a signed license envelope. The payload and signature are
/// retained as an immutable serialized envelope; this record never references a customer database.
/// </summary>
public sealed class VendorSignedLicenseEnvelope
{
    public Guid Id { get; set; }
    public Guid LicenseId { get; set; }
    public int Sequence { get; set; }
    public string KeyId { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public string EnvelopeJson { get; set; } = string.Empty;
    public string PayloadSha256 { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
