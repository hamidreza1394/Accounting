using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
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

        // Register the real EF-backed Identity stores without exposing any sign-in,
        // registration, or bootstrap endpoint at this persistence-only stage.
        services.AddIdentityCore<VendorAdminUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<VendorDbContext>();

        return services;
    }
}
