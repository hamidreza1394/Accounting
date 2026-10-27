namespace Faranou.Vendor.Persistence.Domain;

/// <summary>
/// A two-person approval request for a narrowly allow-listed Vendor operation.
/// Resource identifiers are resolved and authorized against persisted Vendor records before use.
/// </summary>
public sealed class VendorApprovalRequest
{
    public Guid Id { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public string TargetStatus { get; set; } = string.Empty;
    public byte[] ExpectedResourceRowVersion { get; set; } = [];
    public Guid RequestedByUserId { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
    public string Status { get; set; } = "Pending";
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? ReviewComment { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
