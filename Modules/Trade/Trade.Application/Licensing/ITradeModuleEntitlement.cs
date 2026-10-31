namespace Faranou.Modules.Trade.Application.Licensing;

/// <summary>Host-provided commercial entitlement port, separate from user authorization.</summary>
public interface ITradeModuleEntitlement
{
    Task<bool> IsEnabledForCompanyAsync(
        Guid tenantId,
        string moduleCode,
        CancellationToken cancellationToken = default);
}
