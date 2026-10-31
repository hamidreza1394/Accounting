using Faranou.Vendor.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorOfflineRevocationSnapshotModelTests
{
    [Fact]
    public void SnapshotStorageHasGlobalUniqueSequenceImmutablePayloadAndSingletonCounter()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=FaranouVendorModelOnly;Trusted_Connection=True;")
            .Options;
        using var db = new VendorDbContext(options);

        var snapshot = db.Model.FindEntityType("Faranou.Vendor.Persistence.Domain.VendorOfflineRevocationSnapshot");
        Assert.NotNull(snapshot);
        Assert.Equal("VendorOfflineRevocationSnapshots", snapshot!.GetTableName());
        Assert.Contains(snapshot.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(["Sequence"]));
        Assert.Contains(snapshot.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(["ListId"]));
        Assert.Contains(snapshot.GetCheckConstraints(), constraint =>
            constraint.Name == "CK_VendorOfflineRevocationSnapshots_Sequence");
        Assert.Contains(snapshot.GetCheckConstraints(), constraint =>
            constraint.Name == "CK_VendorOfflineRevocationSnapshots_Expiry");

        var sequence = db.Model.FindEntityType("Faranou.Vendor.Persistence.Domain.VendorOfflineRevocationSequence");
        Assert.NotNull(sequence);
        Assert.Equal("VendorOfflineRevocationSequences", sequence!.GetTableName());
        var seed = Assert.Single(sequence.GetSeedData());
        Assert.Equal(1, seed[nameof(Domain.VendorOfflineRevocationSequence.Id)]);
        Assert.Equal(0L, seed[nameof(Domain.VendorOfflineRevocationSequence.LastSequence)]);
    }
}
