namespace Faranou.Modules.Trade.Application.Authorization;

/// <summary>
/// Host-provided authorization port. The adapter must resolve active membership through
/// CompanyAccessService and must never trust tenant/user identifiers supplied by an untrusted client.
/// </summary>
public interface ITradeAuthorization
{
    Task<bool> HasPermissionAsync(
        Guid userId,
        Guid tenantId,
        Guid? operationalPeriodId,
        string permissionCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True only when every distinct company in the requested scope is authorized.
    /// Empty scopes fail closed; partial authorization is not a successful report scope.
    /// </summary>
    Task<bool> HasPermissionForAllCompaniesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> tenantIds,
        string permissionCode,
        CancellationToken cancellationToken = default);
}
