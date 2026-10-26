namespace Faranou.Vendor.Persistence.Auditing;

/// <summary>
/// Append-oriented audit record for sensitive control-plane actions.
/// Hash chaining makes accidental or partial tampering detectable, but is not
/// a substitute for externally protected checkpoints or restrictive SQL permissions.
/// </summary>
public sealed class VendorAuditEvent
{
    public long Id { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public Guid? ActorUserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public string TargetType { get; set; } = string.Empty;

    public string? TargetId { get; set; }

    public string DetailsJson { get; set; } = "{}";

    public byte[] PreviousHash { get; set; } = [];

    public byte[] EventHash { get; set; } = [];
}
