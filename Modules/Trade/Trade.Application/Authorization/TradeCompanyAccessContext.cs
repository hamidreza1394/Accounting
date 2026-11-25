namespace Faranou.Modules.Trade.Application.Authorization;

/// <summary>
/// Server-validated tenant and company of one request, for operations that do not belong to a fiscal period
/// (N-5a-type, D-117 clause on period-independent Context, D-086 item 7). It is a type of its own: it has no
/// <c>FiscalPeriodId</c> and no write flag, so a period-bound operation can not be authorized with it, and it
/// replaces neither <see cref="TradeAccessContext" /> nor <see cref="TradePeriodScope" />.
/// </summary>
/// <remarks>
/// <para>
/// Produced only by the host adapter (<see cref="ITradeAuthorization.ResolveCurrentCompanyContextAsync" />) from the
/// server-side ownership of the verified work context; never built from client input, a claim or one identifier
/// derived from the other. <see cref="TenantId" /> and <see cref="CompanyId" /> are independent (D-087, D-116): the
/// type neither requires nor assumes that they are equal. The adapter rejects any instance it did not resolve itself
/// in the current request, so a context built by hand (even with equal values) is denied.
/// </para>
/// <para>
/// Only company access and the commercial entitlement are keyed by <see cref="CompanyId" />. The tenant is carried
/// for the target model (D-090) and is not verified against a tenant relation in the database until the Core tenant
/// exists (D-121, C4).
/// </para>
/// </remarks>
public sealed record TradeCompanyAccessContext
{
    public TradeCompanyAccessContext(Guid userId, Guid tenantId, Guid companyId)
    {
        UserId = userId == Guid.Empty
            ? throw new ArgumentException("A company access context needs a user.", nameof(userId))
            : userId;
        TenantId = tenantId == Guid.Empty
            ? throw new ArgumentException("A company access context needs a tenant.", nameof(tenantId))
            : tenantId;
        CompanyId = companyId == Guid.Empty
            ? throw new ArgumentException("A company access context needs a company.", nameof(companyId))
            : companyId;
    }

    public Guid UserId { get; }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }
}
