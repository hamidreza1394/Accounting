using Faranou.Modules.Trade.Application.Licensing;

namespace Infrastructure.Trade;

/// <summary>
/// The only production entitlement: the Trade module is never available. Resolving TRADE_IMPORT
/// from the signed licence is blocked by #390; until it ships, access stays closed. There is
/// intentionally no environment branching, configuration switch or dependency here.
/// </summary>
public sealed class DenyAllTradeModuleEntitlement : ITradeModuleEntitlement
{
    public Task<bool> IsEnabledForCompanyAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
