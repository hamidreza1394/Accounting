namespace Faranou.Vendor.Persistence.Auditing;

/// <summary>
/// Metadata-only append record for control-plane security and licensing events.
/// ActorUserId is intentionally not a foreign key so history survives admin deletion.
/// Hash creation, append-only database permissions, and external checkpoints are separate controls.
/// </summary>
public sealed class VendorAuditEvent
{
    public Guid Id { get; set; }

    /// <summary>Version of the canonical event-hash algorithm; existing events use version 1.</summary>
    public int HashVersion { get; set; } = 1;

    public DateTimeOffset OccurredAtUtc { get; set; }

    public Guid? ActorUserId { get; set; }

    public string ActionCode { get; set; } = string.Empty;

    public string? SubjectType { get; set; }

    public string? SubjectId { get; set; }

    public string? CorrelationId { get; set; }

    /// <summary>Allow-listed metadata only. Never put credentials, tokens, payloads or financial data here.</summary>
    public string? MetadataJson { get; set; }

    /// <summary>Lowercase hexadecimal SHA-256 of the previous event; 64 chars.</summary>
    public string PreviousHash { get; set; } = new string('0', 64);

    /// <summary>Lowercase hexadecimal SHA-256 of the canonical current event; 64 chars.</summary>
    public string EventHash { get; set; } = new string('0', 64);
}
