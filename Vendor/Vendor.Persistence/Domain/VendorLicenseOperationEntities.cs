namespace Faranou.Vendor.Persistence.Domain;

/// <summary>
/// Maker-checker request for issuing a signed license or a signed revocation notice.
/// The expected SQL rowversion binds approval to the exact persisted license metadata.
/// </summary>
public sealed class VendorLicenseOperationRequest
{
    public Guid Id { get; set; }
    public Guid LicenseId { get; set; }
    public VendorLicense License { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public byte[] ExpectedLicenseRowVersion { get; set; } = [];
    public byte[] ExpectedCustomerRowVersion { get; set; } = [];
    public byte[]? ExpectedInstallationRowVersion { get; set; }
    public byte[]? ExpectedContractRowVersion { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
    public string Status { get; set; } = "Pending";
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? ReviewComment { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>
/// Exact signed artifact produced only after a second administrator approves the operation.
/// The envelope is public-key-verifiable output; it never contains private key material.
/// </summary>
public sealed class VendorLicenseArtifact
{
    public Guid Id { get; set; }
    public Guid LicenseId { get; set; }
    public VendorLicense License { get; set; } = null!;
    public Guid OperationRequestId { get; set; }
    public VendorLicenseOperationRequest OperationRequest { get; set; } = null!;
    public string ArtifactType { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string Kid { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public string EnvelopeJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
