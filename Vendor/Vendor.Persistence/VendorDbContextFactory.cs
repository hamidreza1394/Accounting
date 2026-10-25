using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Faranou.Vendor.Persistence;

/// <summary>
/// Design-time factory for EF tooling. A connection string must be explicitly supplied;
/// no customer or local default database is selected.
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

        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new VendorDbContext(options);
    }
}
