namespace Faranou.Modules.Trade.Application.Licensing;

/// <summary>
/// Host-provided commercial entitlement port, separate from user authorization.
/// The adapter must evaluate TradeModuleIdentity.ModuleCode; callers cannot select another module.
/// </summary>
public interface ITradeModuleEntitlement
{
    Task<bool> IsEnabledForCompanyAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
