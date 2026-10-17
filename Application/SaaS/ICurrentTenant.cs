namespace Application.SaaS;

/// <summary>Request-scoped server-resolved security context. Null tenant means no company selected.</summary>
public interface ICurrentTenant
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    Guid? FiscalPeriodId { get; }
}
