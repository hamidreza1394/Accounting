using Faranou.Modules.Trade.Application.Authorization;
using Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Trade;

/// <summary>
/// Enforces Trade permissions through the existing company-role permission catalog and the
/// already-validated Core work context. It deliberately owns no period or permission storage.
/// </summary>
public sealed class TradeAuthorizationAdapter(
    Data.DatabaseContext databaseContext,
    CompanyAccessService companyAccessService,
    WorkContextService workContextService,
    IHttpContextAccessor httpContextAccessor) : ITradeAuthorization
{
    public async Task<bool> HasPermissionAsync(
        Guid userId,
        Guid tenantId,
        Guid fiscalPeriodId,
        string permissionCode,
        TradeAccessIntent accessIntent,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidRequest(userId, permissionCode, accessIntent) ||
            tenantId == Guid.Empty ||
            fiscalPeriodId == Guid.Empty)
        {
            return false;
        }

        var httpContext = GetAuthenticatedContext(userId);
        var sessionId = WorkContextCookie.GetSessionId(httpContext?.User);
        if (httpContext is null || sessionId is null)
        {
            return false;
        }

        var current = await workContextService.GetCurrentAsync(
            httpContext, userId, sessionId.Value, cancellationToken);

        if (current is null ||
            current.Company.CompanyId != tenantId ||
            current.FiscalPeriod.Id != fiscalPeriodId ||
            (accessIntent == TradeAccessIntent.Write && !current.AllowsWrites))
        {
            return false;
        }

        return await companyAccessService.HasPermissionAsync(
            userId, tenantId, permissionCode, cancellationToken);
    }

    public async Task<bool> HasPermissionForAllCompanyPeriodsAsync(
        Guid userId,
        IReadOnlyCollection<TradePeriodScope> scopes,
        string permissionCode,
        TradeAccessIntent accessIntent,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidRequest(userId, permissionCode, accessIntent) ||
            scopes is null ||
            scopes.Count == 0)
        {
            return false;
        }

        var requestedScopes = scopes.Distinct().ToArray();
        if (requestedScopes.Any(scope =>
                scope is null || scope.TenantId == Guid.Empty || scope.FiscalPeriodId == Guid.Empty))
        {
            return false;
        }

        var httpContext = GetAuthenticatedContext(userId);
        if (httpContext is null || WorkContextCookie.GetSessionId(httpContext.User) is null)
        {
            return false;
        }

        var companyIds = requestedScopes.Select(scope => scope.TenantId).Distinct().ToArray();
        var periodIds = requestedScopes.Select(scope => scope.FiscalPeriodId).Distinct().ToArray();

        var grantedCompanies = await companyAccessService.GetCompaniesWithPermissionAsync(
            userId, companyIds, permissionCode, cancellationToken);

        if (companyIds.Any(companyId => !grantedCompanies.Contains(companyId)))
        {
            return false;
        }

        // IDs are queried only within the companies for which the role permission was verified.
        // Pair matching below prevents a valid period ID from being replayed under another tenant.
        var periods = await databaseContext.FiscalPeriods
            .AsNoTracking()
            .Where(period => companyIds.Contains(period.TenantId) && periodIds.Contains(period.Id))
            .Select(period => new { period.Id, period.TenantId, period.Status })
            .ToListAsync(cancellationToken);

        return requestedScopes.All(scope =>
        {
            var period = periods.FirstOrDefault(item =>
                item.Id == scope.FiscalPeriodId && item.TenantId == scope.TenantId);

            if (period is null || !FiscalPeriodSelectionPolicy.IsSelectable(period.Status))
            {
                return false;
            }

            return accessIntent != TradeAccessIntent.Write ||
                FiscalPeriodSelectionPolicy.GetAccessMode(period.Status) == WorkAccessMode.ReadWrite;
        });
    }

    private HttpContext? GetAuthenticatedContext(Guid requestedUserId)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var authenticatedUserId = CompanyAccessService.GetUserId(httpContext?.User);

        return authenticatedUserId == requestedUserId ? httpContext : null;
    }

    private static bool IsValidRequest(
        Guid userId,
        string permissionCode,
        TradeAccessIntent accessIntent)
    {
        return userId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(permissionCode) &&
            accessIntent is TradeAccessIntent.Read or TradeAccessIntent.Write &&
            TradePermissionCodes.IsAccessIntentCompatible(permissionCode, accessIntent);
    }
}
