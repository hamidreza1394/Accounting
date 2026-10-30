using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Persistence.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorAuditCheckpointStartupGuardTests
{
    [Fact]
    public void ProductionWithoutIndependentStore_FailsClosed()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            VendorAuditCheckpointStartupGuard.EnsureConfigured(isProduction: true, services));

        Assert.Contains("independently permissioned append-only audit checkpoint store", exception.Message);
    }

    [Fact]
    public void ProductionWithOnlyGenericStore_FailsClosed()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IVendorAuditCheckpointStore, TestStore>();

        Assert.Throws<InvalidOperationException>(() =>
            VendorAuditCheckpointStartupGuard.EnsureConfigured(isProduction: true, services));
    }

    [Fact]
    public void ProductionWithMultipleProductionStores_FailsClosed()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IProductionVendorAuditCheckpointStore, TestStore>();
        services.AddSingleton<IProductionVendorAuditCheckpointStore, TestStore>();

        Assert.Throws<InvalidOperationException>(() =>
            VendorAuditCheckpointStartupGuard.EnsureConfigured(isProduction: true, services));
    }

    [Fact]
    public void DevelopmentMayRunWithoutIndependentStore()
    {
        var services = new ServiceCollection();

        VendorAuditCheckpointStartupGuard.EnsureConfigured(isProduction: false, services);
    }

    [Fact]
    public void ProductionAcceptsExplicitProductionStoreRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IProductionVendorAuditCheckpointStore, TestStore>();

        VendorAuditCheckpointStartupGuard.EnsureConfigured(isProduction: true, services);
    }

    private sealed class TestStore : IProductionVendorAuditCheckpointStore
    {
        public Task<VendorAuditCheckpoint?> ReadLatestAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<VendorAuditCheckpoint?>(null);

        public Task AppendAsync(VendorAuditCheckpoint checkpoint, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
