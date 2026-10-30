using Faranou.Vendor.Persistence.Auditing;
using Microsoft.Extensions.DependencyInjection;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Prevents a Production Vendor host from starting without an explicitly registered independent
/// audit checkpoint adapter. Development/test environments may use test stores or no store.
/// </summary>
public static class VendorAuditCheckpointStartupGuard
{
    public static void EnsureConfigured(bool isProduction, IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!isProduction)
        {
            return;
        }

        var productionStoreRegistrations = services.Count(descriptor =>
            descriptor.ServiceType == typeof(IProductionVendorAuditCheckpointStore));
        if (productionStoreRegistrations != 1)
        {
            throw new InvalidOperationException(
                "Vendor Production requires exactly one independently permissioned append-only audit checkpoint store. " +
                "Configure one approved provider with separate credentials and retention controls; no local-file or Vendor-database fallback is permitted.");
        }
    }
}
