using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Faranou.Vendor.Persistence;

/// <summary>
/// Design-time factory for EF tooling. A connection string must be explicitly supplied;
/// no customer or local default database is selected. It reuses the production persistence
/// registration so the design-time model includes the same Identity store options as runtime.
/// </summary>
public sealed class VendorDbContextFactory : IDesignTimeDbContextFactory<VendorDbContext>
{
    public VendorDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("FARANO_VENDOR_ConnectionStrings__VendorDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set FARANO_VENDOR_ConnectionStrings__VendorDb to a dedicated disposable Vendor database connection for EF tooling.");
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = connectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddVendorPersistence(configuration);

        // EF tooling owns and disposes the returned context. Keep the design-time root
        // provider alive for this short-lived CLI process so the context uses its services.
        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<VendorDbContext>();
    }
}
