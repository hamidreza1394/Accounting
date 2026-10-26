using Faranou.Vendor.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Faranou.Vendor.Host.Tests;

internal static class VendorTestServiceProviderFactory
{
    public static ServiceProvider Create(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:VendorDb"] = connectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVendorPersistence(configuration);

        return services.BuildServiceProvider();
    }
}
