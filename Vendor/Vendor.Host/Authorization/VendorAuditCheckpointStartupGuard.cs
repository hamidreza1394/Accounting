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

        if (!services.Any(descriptor =>
                descriptor.ServiceType == typeof(IProductionVendorAuditCheckpointStore)))
        {
            throw new InvalidOperationException(
                "Vendor Production requires an independently permissioned append-only audit checkpoint store. " +
                "Configure an approved provider with separate credentials and retention controls; no local-file or Vendor-database fallback is permitted.");
        }
    }
}
