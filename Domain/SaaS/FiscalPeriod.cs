namespace Domain.SaaS;

public enum FiscalPeriodStatus { Open = 1, Closed = 2, Archived = 3 }

public sealed class FiscalPeriod : ITenantScopedEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public FiscalPeriodStatus Status { get; set; } = FiscalPeriodStatus.Open;
    public DateTime CreatedAtUtc { get; set; }
}
