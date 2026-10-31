using Faranou.Modules.Trade.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faranou.Modules.Trade.Infrastructure.Persistence.Configurations;

internal sealed class TradeOperationalPeriodConfiguration : IEntityTypeConfiguration<TradeOperationalPeriod>
{
    public void Configure(EntityTypeBuilder<TradeOperationalPeriod> builder)
    {
        builder.ToTable("OperationalPeriods", table =>
            table.HasCheckConstraint("CK_TradeOperationalPeriods_DateRange", "[StartDate] <= [EndDate]"));

        builder.HasKey(period => period.Id);

        builder.Property(period => period.Code)
            .HasMaxLength(64).IsUnicode(false).IsRequired();

        builder.Property(period => period.Title)
            .HasMaxLength(160).IsRequired();

        builder.Property(period => period.Status)
            .HasConversion<string>().HasMaxLength(16).IsUnicode(false).IsRequired();

        builder.HasIndex(period => new { period.TenantId, period.Code })
            .IsUnique().HasDatabaseName("UX_TradeOperationalPeriods_Tenant_Code");

        builder.HasIndex(period => new { period.TenantId, period.Status, period.StartDate })
            .HasDatabaseName("IX_TradeOperationalPeriods_Tenant_Status_StartDate");
    }
}
