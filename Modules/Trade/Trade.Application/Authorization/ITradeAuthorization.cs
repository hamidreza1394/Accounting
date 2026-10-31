namespace Faranou.Modules.Trade.Application.Authorization;

/// <summary>
/// Host-provided authorization port. The adapter must resolve active membership through
/// CompanyAccessService, validate the shared FiscalPeriod belongs to the company, and never
/// trust tenant/user identifiers supplied by an untrusted client.
/// </summary>
/// <remarks>
/// Period selection and period lifecycle are shared with Accounting through WorkContext and
/// FiscalPeriodSelectionPolicy. This port adds Trade operation permissions to that verified
/// context; it does not create a second period-access system. The adapter enforces WorkContext.AllowsWrites so a closed period cannot be modified even when a Trade write permission is granted.
/// </remarks>
public interface ITradeAuthorization
{
    /// <summary>
    /// Resolves the server-validated context of the current request (session, active membership and
    /// fiscal period are re-validated against the database). Returns null when the user is empty,
    /// is not the authenticated principal, has no sign-in session or has no valid context.
    /// The returned instance is the only one <see cref="HasPermissionAsync" /> and
    /// <see cref="GetGrantedTradePermissionsAsync" /> accept.
    /// </summary>
    Task<TradeAccessContext?> ResolveCurrentContextAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fails closed unless <paramref name="context" /> is the exact instance resolved for this request,
    /// the permission matches the intent, a Write intent is allowed by the period, and the company role holds the permission.
    /// This is a user-permission check only; the commercial entitlement is checked by TradeAccessGate.
    /// </summary>
    Task<bool> HasPermissionAsync(
        TradeAccessContext context,
        string permissionCode,
        TradeAccessIntent accessIntent,
        CancellationToken cancellationToken = default);

    /// <summary>Granted <c>trade.*</c> codes of the resolved context; empty for a forged or foreign context.</summary>
    Task<IReadOnlyList<string>> GetGrantedTradePermissionsAsync(
        TradeAccessContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True only when every distinct company/period pair in the requested scope is authorized.
    /// Empty scopes fail closed; partial authorization is not a successful report scope.
    /// </summary>
    /// <remarks>
    /// NOT entitlement-aware: it checks user permission and period state only. It must not be
    /// exposed by any endpoint until an entitlement-aware multi-company path exists.
    /// </remarks>
    Task<bool> HasPermissionForAllCompanyPeriodsAsync(
        Guid userId,
        IReadOnlyCollection<TradePeriodScope> scopes,
        string permissionCode,
        TradeAccessIntent accessIntent,
        CancellationToken cancellationToken = default);
}
