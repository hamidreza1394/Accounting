using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Faranou.Vendor.Persistence;

public static class DependencyInjection
{
    public const string ConnectionStringName = "VendorDb";

    public static IServiceCollection AddVendorPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Vendor persistence is not configured. Set FARANO_VENDOR_ConnectionStrings__VendorDb using a secret store or environment variable.");
        }

        services.AddDbContext<VendorDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(VendorDbContext).Assembly.FullName)));

        return services;
    }
}
