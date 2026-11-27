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
    /// <summary>Requested → PendingApproval → Issued; Rejected / Failed allow a corrected retry.</summary>
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
    public int? IssuedSequence { get; set; }
    public string? DeliveryNonce { get; set; }
    public string? DeliveryPayloadSha256 { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public string? AcknowledgementSha256 { get; set; }
    public string? AcknowledgementKeyId { get; set; }
    public string? AcknowledgementResult { get; set; }
    public DateTimeOffset? CustomerProcessedAtUtc { get; set; }
    public DateTimeOffset? AcknowledgedAtUtc { get; set; }
    public string? FailureCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
