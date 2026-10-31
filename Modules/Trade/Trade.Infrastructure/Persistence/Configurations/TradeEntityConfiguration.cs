using Faranou.Modules.Trade.Domain.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faranou.Modules.Trade.Infrastructure.Persistence.Configurations;

internal static class TradeEntityConfiguration
{
    public static void Configure<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : TradeEntity
    {
        builder.HasKey(entity => entity.Id);
        builder.HasAlternateKey(entity => new { entity.TenantId, entity.Id });

        builder.Property(entity => entity.TenantId).IsRequired();
        builder.Property(entity => entity.CreatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(entity => entity.CreatedByUserId).IsRequired();
        builder.Property(entity => entity.UpdatedAtUtc).HasColumnType("datetime2");
        builder.Property(entity => entity.UpdatedByUserId);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.Property(entity => entity.IsDeleted).HasDefaultValue(false).IsRequired();
        builder.Property(entity => entity.DeletedAtUtc).HasColumnType("datetime2");
        builder.Property(entity => entity.DeletedByUserId);
    }
}
