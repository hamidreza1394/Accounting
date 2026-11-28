namespace Faranou.Vendor.Persistence.Domain;

/// <summary>
/// One explicit, operator-initiated entitlement amendment request originating from a purchased
/// sales order line (D-108/D-109). Payment verification never creates this row and issuing an
/// amendment never changes payment state; the two cycles stay independent. Only the request row
/// persists here — the candidate payload is rebuilt by the official amendment builder from the
/// latest signature-verified envelope at issuance time, so no derived entitlement data is stored.
/// </summary>
public sealed class VendorLicenseAmendmentRequest
{
    /// <summary>
    /// Requested → PendingApproval → Issued → Delivered → Applied. Rejected / Failed allow a
    /// corrected retry. Delivered means the exact signed envelope bytes were exported to the
    /// customer with a per-delivery nonce; Applied is set only by a valid signed customer
    /// acknowledgement (never by delivery itself), mirroring the S-B owner decision (#393).
    /// </summary>
    public const string StatusRequested = "Requested";
    public const string StatusPendingApproval = "PendingApproval";
    public const string StatusIssued = "Issued";
    public const string StatusDelivered = "Delivered";
    public const string StatusApplied = "Applied";
    public const string StatusRejected = "Rejected";
    public const string StatusFailed = "Failed";

    public const string KindMaxCompanies = "maxCompanies";
    public const string KindSupportUpdates = "supportUpdates";

    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public VendorSalesOrder Order { get; set; } = null!;
    /// <summary>One active amendment per purchased line; uniqueness is enforced by a filtered index.</summary>
    public Guid OrderLineId { get; set; }
    public VendorSalesOrderLine OrderLine { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public VendorCustomer Customer { get; set; } = null!;
    public Guid LicenseId { get; set; }
    public VendorLicense License { get; set; } = null!;
    public string Kind { get; set; } = KindMaxCompanies;
    public int? RequestedMaxCompanies { get; set; }
    public DateTimeOffset? RequestedSupportUntilUtc { get; set; }
    public DateTimeOffset? RequestedUpdatesUntilUtc { get; set; }
    public string Status { get; set; } = StatusRequested;
    public Guid RequestedByUserId { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public string? FailureCode { get; set; }
    /// <summary>
    /// Sequence of the envelope this request produced at issuance time. Delivery and every
    /// acknowledgement bind to the exact signed payload through (LicenseId, IssuedSequence) →
    /// StoredEnvelope.PayloadSha256; without it a later issuance of the same license could be
    /// delivered for an older request.
    /// </summary>
    public int? IssuedSequence { get; set; }
    /// <summary>Per-delivery nonce generated when the envelope bytes were first exported.</summary>
    public string? DeliveredNonce { get; set; }
    /// <summary>SHA-256 of the exact signed license payload delivered (matches the stored envelope).</summary>
    public string? DeliveredPayloadSha256 { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    /// <summary>SHA-256 digest of the first accepted acknowledgement envelope; first ACK wins.</summary>
    public string? AcknowledgementSha256 { get; set; }
    public string? AcknowledgementKeyId { get; set; }
    /// <summary>Customer-signed outcome: Applied, AlreadyApplied or Rejected.</summary>
    public string? AcknowledgementResult { get; set; }
    /// <summary>Stable customer rejection code; never free-form exception text or PII.</summary>
    public string? AcknowledgementErrorCode { get; set; }
    public DateTimeOffset? CustomerProcessedAtUtc { get; set; }
    public DateTimeOffset? AcknowledgedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
