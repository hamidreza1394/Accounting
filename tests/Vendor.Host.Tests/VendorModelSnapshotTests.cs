using Faranou.Vendor.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorModelSnapshotTests
{
    [Fact]
    public void SnapshotHasNoPendingRelationalModelChanges()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseSqlServer("Server=localhost;Database=VendorModelSnapshotTest;User Id=invalid;Password=invalid;TrustServerCertificate=True")
            .Options;
        using var db = new VendorDbContext(options);
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot);
        var differ = db.GetService<IMigrationsModelDiffer>();
        var snapshotModel = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot!.Model, designTime: true);
        var operations = differ.GetDifferences(snapshotModel.GetRelationalModel(), db.Model.GetRelationalModel());
        var summary = string.Join(Environment.NewLine, operations.Select(operation => operation.GetType().Name));
        Assert.True(operations.Count == 0, "Pending EF model operations:" + Environment.NewLine + summary);
    }
}
