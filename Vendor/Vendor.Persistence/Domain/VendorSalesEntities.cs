namespace Faranou.Vendor.Persistence.Domain;

/// <summary>
/// Vendor commercial sales order (D-108). Payment state is never stored on the order; Unpaid /
/// PartiallyPaid / Paid / Overdue are derived at read time from the order amount and the sum of
/// Verified payments. Issuance is independent of payment state in both directions.
/// </summary>
public sealed class VendorSalesOrder
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public VendorCustomer Customer { get; set; } = null!;
    public Guid? ContractId { get; set; }
    public VendorContract? Contract { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public DateTimeOffset? DueAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<VendorSalesOrderLine> Lines { get; } = [];
    public ICollection<VendorPaymentRecord> Payments { get; } = [];
}

/// <summary>Typed order line; line amounts are informational and never allocate payments (D-108 §4).</summary>
public sealed class VendorSalesOrderLine
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public VendorSalesOrder Order { get; set; } = null!;
    public string ProductCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitAmount { get; set; }
    public decimal LineAmount { get; set; }
    /// <summary>Optional commercial reference to a license; it never gates or proves issuance.</summary>
    public Guid? LicenseId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

/// <summary>
/// One manually recorded payment for a sales order (D-108 §4-5). Recorded payments never reduce
/// debt; only Verified payments do. Verification is a separate approval cycle with its own
/// ResourceType (salesOrderPayment) and permission (vendor.payments.verify).
/// </summary>
public sealed class VendorPaymentRecord
{
    public const string StatusRecorded = "Recorded";
    public const string StatusVerified = "Verified";
    public const string StatusRejected = "Rejected";

    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public VendorSalesOrder Order { get; set; } = null!;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public string Method { get; set; } = string.Empty;
    public string? ExternalReference { get; set; }
    public DateTimeOffset? ClaimedPaidAtUtc { get; set; }
    public string Status { get; set; } = StatusRecorded;
    public Guid? VerifiedByUserId { get; set; }
    public DateTimeOffset? VerifiedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
