namespace Domain;

/// <summary>
/// Records security-sensitive actions without storing passwords, tokens, or other secrets.
/// </summary>
public sealed class SecurityAuditEvent : Seedwork.Entity
{
    public Guid? UserId { get; set; }
    public Guid? CompanyId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
    public string? DetailsJson { get; set; }
}