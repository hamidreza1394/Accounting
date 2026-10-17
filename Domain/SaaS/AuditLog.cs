namespace Domain.SaaS;

/// <summary>Audit metadata only. Never store passwords, tokens or unnecessary personal data.</summary>
public sealed class AuditLog
{
    public long Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? CorrelationId { get; set; }
    public string? Details { get; set; }
}
