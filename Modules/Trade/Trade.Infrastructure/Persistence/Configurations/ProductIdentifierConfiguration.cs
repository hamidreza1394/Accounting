using Faranou.Modules.Trade.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faranou.Modules.Trade.Infrastructure.Persistence.Configurations;

internal sealed class ProductIdentifierConfiguration : IEntityTypeConfiguration<ProductIdentifier>
{
    public void Configure(EntityTypeBuilder<ProductIdentifier> builder)
    {
        TradeEntityConfiguration.Configure(builder);
        builder.ToTable("ProductIdentifiers", "trade");

        builder.Property(identifier => identifier.RegistrationSourceCode).HasMaxLength(64);
        builder.Property(identifier => identifier.OriginCountryCode).HasMaxLength(2).IsUnicode(false);
        builder.Property(identifier => identifier.CommodityTypeCode).HasMaxLength(64);
        builder.Property(identifier => identifier.Gtin).HasMaxLength(32).IsUnicode(false);
        builder.Property(identifier => identifier.Irc).HasMaxLength(64).IsUnicode(false);
        builder.Property(identifier => identifier.ValidUntil).HasColumnType("date").IsRequired();
        builder.Property(identifier => identifier.IsLinked).IsRequired();

        builder.HasIndex(identifier => new { identifier.TenantId, identifier.Gtin })
            .IsUnique()
            .HasDatabaseName("UX_ProductIdentifiers_Tenant_GTIN_Active")
            .HasFilter("[IsDeleted] = 0 AND [Gtin] IS NOT NULL");

        builder.HasIndex(identifier => new { identifier.TenantId, identifier.Irc })
            .IsUnique()
            .HasDatabaseName("UX_ProductIdentifiers_Tenant_IRC_Active")
            .HasFilter("[IsDeleted] = 0 AND [Irc] IS NOT NULL");

        builder.HasOne<Faranou.Modules.Trade.Domain.Parties.Party>()
            .WithMany()
            .HasForeignKey(identifier => new { identifier.TenantId, identifier.SupplierPartyId })
            .HasPrincipalKey(party => new { party.TenantId, party.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
