namespace Domain;

/// <summary>
/// An accounting period belonging to exactly one tenant company.
/// </summary>
public sealed class FiscalPeriod : Seedwork.Entity
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public FiscalPeriodStatus Status { get; set; } = FiscalPeriodStatus.Open;
}

public enum FiscalPeriodStatus
{
    Open = 1,
    Closed = 2,
    Archived = 3
}