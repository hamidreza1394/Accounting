namespace Faranou.Modules.Trade.Domain;

/// <summary>
/// Trade-owned operational period. TenantId is the core Company.Id (D-013);
/// this project deliberately does not reference accounting Company or FiscalPeriod types.
/// </summary>
public sealed class TradeOperationalPeriod
{
    private TradeOperationalPeriod()
    {
        Code = string.Empty;
        Title = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Code { get; private set; }
    public string Title { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public TradeOperationalPeriodStatus Status { get; private set; }

    public static TradeOperationalPeriod Create(
        Guid tenantId,
        string code,
        string title,
        DateOnly startDate,
        DateOnly endDate)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A valid company tenant is required.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 64)
        {
            throw new ArgumentException("The period code must contain between 1 and 64 characters.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 160)
        {
            throw new ArgumentException("The period title must contain between 1 and 160 characters.", nameof(title));
        }

        if (startDate > endDate)
        {
            throw new ArgumentException("The period start date must not be after its end date.", nameof(endDate));
        }

        return new TradeOperationalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code.Trim().ToUpperInvariant(),
            Title = title.Trim(),
            StartDate = startDate,
            EndDate = endDate,
            Status = TradeOperationalPeriodStatus.Draft,
        };
    }
}
