using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Faranou.Vendor.Persistence;

/// <summary>Registers only Vendor persistence and its isolated ASP.NET Core Identity stores.</summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "VendorDb";

    public static IServiceCollection AddVendorPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<VendorDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Vendor persistence is not configured. Set FARANO_VENDOR_ConnectionStrings__VendorDb using a secret store or environment variable.");
            }

            options.UseSqlServer(
                connectionString,
                sql => sql.MigrationsAssembly(typeof(VendorDbContext).Assembly.FullName));
        });

        // Identity is configured in one place so runtime authentication and EF tooling
        // cannot silently register competing IdentityOptions for the same DbContext.
        services.AddIdentityCore<VendorAdminUser>(options =>
            {
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 14;
                options.Password.RequiredUniqueChars = 4;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<VendorDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        return services;
    }
}
