namespace Faranou.Modules.Trade.Application.Authorization;

/// <summary>
/// Host-provided authorization port. The adapter must resolve active membership through
/// CompanyAccessService, validate the shared FiscalPeriod belongs to the company, and never
/// trust tenant/user identifiers supplied by an untrusted client.
/// </summary>
/// <remarks>
/// Period selection and period lifecycle are shared with Accounting through WorkContext and
/// FiscalPeriodSelectionPolicy. This port adds Trade operation permissions to that verified
/// context; it does not create a second period-access system.
/// </remarks>
public interface ITradeAuthorization
{
    Task<bool> HasPermissionAsync(
        Guid userId,
        Guid tenantId,
        Guid fiscalPeriodId,
        string permissionCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True only when every distinct company/period pair in the requested scope is authorized.
    /// Empty scopes fail closed; partial authorization is not a successful report scope.
    /// </summary>
    Task<bool> HasPermissionForAllCompanyPeriodsAsync(
        Guid userId,
        IReadOnlyCollection<TradePeriodScope> scopes,
        string permissionCode,
        CancellationToken cancellationToken = default);
}
