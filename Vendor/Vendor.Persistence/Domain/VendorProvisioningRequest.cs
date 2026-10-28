namespace Faranou.Vendor.Persistence.Domain;

/// <summary>
/// Durable Vendor-side record of a provisioning attempt. It stores configuration and signed-package
/// metadata only; passwords, password hashes and reusable bootstrap secrets are never persisted.
/// </summary>
public sealed class VendorProvisioningRequest
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid InstallationId { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string PayloadSha256 { get; set; } = string.Empty;
    public string Status { get; set; } = "AwaitingSigner";
    public string? KeyId { get; set; }
    public string? Algorithm { get; set; }
    public string? SignatureBase64 { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? SignedAtUtc { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
    public string? FailureCode { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
