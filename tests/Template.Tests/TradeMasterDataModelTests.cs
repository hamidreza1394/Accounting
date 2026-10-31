using Faranou.Modules.Trade.Domain.Common;
using Faranou.Modules.Trade.Domain.Parties;
using Faranou.Modules.Trade.Domain.Products;
using Faranou.Modules.Trade.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Template.Tests;

public sealed class TradeMasterDataModelTests
{
    private static TradeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TradeDbContext>()
            .UseSqlServer("Server=localhost;Database=Trade_ModelMetadata;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True")
            .Options;

        return new TradeDbContext(options);
    }

    [Fact]
    public void MasterDataEntities_AreMappedOnlyToTradeSchema_AndHaveNoFiscalPeriod()
    {
        using var context = CreateContext();
        var entityTypes = new[]
        {
            typeof(Party),
            typeof(PartyRoleAssignment),
            typeof(PartyBankAccount),
            typeof(Product),
            typeof(ProductIdentifier),
        };

        Assert.Null(context.Model.FindEntityType(typeof(TradeEntity)));

        foreach (var clrType in entityTypes)
        {
            var entity = context.Model.FindEntityType(clrType);
            Assert.NotNull(entity);
            Assert.Equal("trade", entity.GetSchema());
            Assert.NotNull(entity.GetTableName());
            Assert.Null(entity.FindProperty("FiscalPeriodId"));
            Assert.False(entity.FindProperty(nameof(TradeEntity.TenantId))!.IsNullable);
            Assert.True(entity.FindProperty(nameof(TradeEntity.RowVersion))!.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate,
                entity.FindProperty(nameof(TradeEntity.RowVersion))!.ValueGenerated);
        }
    }

    [Fact]
    public void PartyAndProductCodes_AreUniquePerTenantOnlyForActiveRows()
    {
        using var context = CreateContext();

        AssertActiveTenantUniqueIndex<Party>(context, nameof(Party.Code), "UX_Parties_Tenant_Code_Active");
        AssertActiveTenantUniqueIndex<Product>(context, nameof(Product.Code), "UX_Products_Tenant_Code_Active");
    }

    [Fact]
    public void ProductIdentifiers_KeepGtinAndIrcAsSeparateTextColumns()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(ProductIdentifier))!;

        var gtin = entity.FindProperty(nameof(ProductIdentifier.Gtin))!;
        var irc = entity.FindProperty(nameof(ProductIdentifier.Irc))!;

        Assert.Equal(typeof(string), gtin.ClrType);
        Assert.Equal(typeof(string), irc.ClrType);
        Assert.NotSame(gtin, irc);
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(TradeEntity.TenantId), nameof(ProductIdentifier.Gtin) }) &&
            index.GetFilter() == "[IsDeleted] = 0 AND [Gtin] IS NOT NULL");
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(TradeEntity.TenantId), nameof(ProductIdentifier.Irc) }) &&
            index.GetFilter() == "[IsDeleted] = 0 AND [Irc] IS NOT NULL");
    }

    [Fact]
    public void PartyChildren_UseTenantAwareForeignKeys()
    {
        using var context = CreateContext();
        var role = context.Model.FindEntityType(typeof(PartyRoleAssignment))!;
        var bank = context.Model.FindEntityType(typeof(PartyBankAccount))!;
        var identifier = context.Model.FindEntityType(typeof(ProductIdentifier))!;

        AssertTenantAwareForeignKey<PartyRoleAssignment, Party>(role, nameof(PartyRoleAssignment.PartyId));
        AssertTenantAwareForeignKey<PartyBankAccount, Party>(bank, nameof(PartyBankAccount.PartyId));
        AssertTenantAwareForeignKey<ProductIdentifier, Product>(identifier, nameof(ProductIdentifier.ProductId));
    }

    private static void AssertActiveTenantUniqueIndex<TEntity>(
        TradeDbContext context,
        string businessKey,
        string expectedName)
    {
        var entity = context.Model.FindEntityType(typeof(TEntity))!;
        var index = Assert.Single(entity.GetIndexes(), candidate =>
            candidate.IsUnique &&
            candidate.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(TradeEntity.TenantId), businessKey }));

        Assert.Equal(expectedName, index.GetDatabaseName());
        Assert.Equal("[IsDeleted] = 0", index.GetFilter());
    }

    private static void AssertTenantAwareForeignKey<TChild, TParent>(IEntityType child, string childIdProperty)
    {
        var foreignKey = Assert.Single(child.GetForeignKeys(), candidate =>
            candidate.PrincipalEntityType.ClrType == typeof(TParent) &&
            candidate.Properties.Any(property => property.Name == childIdProperty));

        Assert.Equal(
            new[] { nameof(TradeEntity.TenantId), childIdProperty },
            foreignKey.Properties.Select(property => property.Name));
        Assert.Equal(
            new[] { nameof(TradeEntity.TenantId), nameof(TradeEntity.Id) },
            foreignKey.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }
}
