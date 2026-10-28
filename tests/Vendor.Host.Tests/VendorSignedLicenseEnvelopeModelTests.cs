using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorSignedLicenseEnvelopeModelTests
{
    [Fact]
    public void SignedEnvelopeStorageIsVendorOwnedImmutableSequenceScopedAndConcurrencyProtected()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseSqlServer("Server=localhost;Database=VendorEnvelopeModelTest;User Id=invalid;Password=invalid;TrustServerCertificate=True")
            .Options;
        using var db = new VendorDbContext(options);

        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(VendorSignedLicenseEnvelope));
        Assert.NotNull(entity);
        Assert.Equal("VendorSignedLicenseEnvelopes", entity!.GetTableName());
        Assert.Equal(128, entity.FindProperty(nameof(VendorSignedLicenseEnvelope.KeyId))!.GetMaxLength());
        Assert.Equal(64, entity.FindProperty(nameof(VendorSignedLicenseEnvelope.PayloadSha256))!.GetMaxLength());
        Assert.True(entity.FindProperty(nameof(VendorSignedLicenseEnvelope.RowVersion))!.IsConcurrencyToken);
        Assert.Contains(entity.GetCheckConstraints(), item =>
            item.Name == "CK_VendorSignedLicenseEnvelopes_Sequence" && item.Sql == "[Sequence] >= 1");

        var sequenceIndex = Assert.Single(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(VendorSignedLicenseEnvelope.LicenseId), nameof(VendorSignedLicenseEnvelope.Sequence) }));
        Assert.True(sequenceIndex.IsUnique);

        var licenseForeignKey = Assert.Single(entity.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Single().Name == nameof(VendorSignedLicenseEnvelope.LicenseId));
        Assert.Equal(DeleteBehavior.Restrict, licenseForeignKey.DeleteBehavior);
        Assert.Equal(typeof(VendorLicense), licenseForeignKey.PrincipalEntityType.ClrType);
    }
}
