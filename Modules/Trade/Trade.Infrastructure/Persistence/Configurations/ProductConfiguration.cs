using Faranou.Modules.Trade.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faranou.Modules.Trade.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        TradeEntityConfiguration.Configure(builder);
        builder.ToTable("Products", "trade");

        builder.Property(product => product.Code).HasMaxLength(64).IsRequired();
        builder.Property(product => product.Name).HasMaxLength(200).IsRequired();
        builder.Property(product => product.ProductGroupCode).HasMaxLength(64);
        builder.Property(product => product.HsCode).HasMaxLength(16).IsUnicode(false);
        builder.Property(product => product.BaseUnitCode).HasMaxLength(16).IsUnicode(false).IsRequired();
        builder.Property(product => product.IsActive).IsRequired();

        builder.HasIndex(product => new { product.TenantId, product.Code })
            .IsUnique()
            .HasDatabaseName("UX_Products_Tenant_Code_Active")
            .HasFilter("[IsDeleted] = 0");

        builder.HasMany(product => product.Identifiers)
            .WithOne()
            .HasForeignKey(identifier => new { identifier.TenantId, identifier.ProductId })
            .HasPrincipalKey(product => new { product.TenantId, product.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(product => product.Identifiers).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
