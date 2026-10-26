using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Faranou.Vendor.Persistence;

/// <summary>
/// Enables EF tooling without putting a real connection string in source control.
/// The tooling placeholder is only used to build the model; it must never be used for deployment.
/// </summary>
public sealed class DesignTimeVendorDbContextFactory : IDesignTimeDbContextFactory<VendorDbContext>
{
    public VendorDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=FaranouVendorDesignTime;User Id=design_time_only;Password=NotARealSecret!;TrustServerCertificate=True",
                sql => sql.MigrationsAssembly(typeof(VendorDbContext).Assembly.FullName))
            .Options;

        return new VendorDbContext(options);
    }
}
