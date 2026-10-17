using System.Security.Claims;
using Application.SaaS;
using Microsoft.AspNetCore.Http;

namespace Data.SaaS;

/// <summary>Reads tenant context from a protected auth cookie. Membership/permissions must still be revalidated.</summary>
public sealed class HttpCurrentTenant(IHttpContextAccessor accessor) : ICurrentTenant
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;
    public Guid? UserId => ReadGuidClaim(ClaimTypes.NameIdentifier);
    public Guid? TenantId => ReadGuidClaim("tarazava:tenant_id");
    public Guid? FiscalPeriodId => ReadGuidClaim("tarazava:fiscal_period_id");

    private Guid? ReadGuidClaim(string type)
    {
        var value = User?.FindFirst(type)?.Value;
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}
