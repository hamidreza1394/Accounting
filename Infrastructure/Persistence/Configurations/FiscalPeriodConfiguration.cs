using Microsoft.EntityFrameworkCore;

namespace Data.Configurations;

internal sealed class FiscalPeriodConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.FiscalPeriod>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.FiscalPeriod> builder)
    {
        builder.ToTable("FiscalPeriods");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.StartDate, x.EndDate }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status });
        // The principal of composite foreign keys that must stay inside one company, for
        // example ConsolidationRunCompanies (FiscalPeriodId, CompanyId) (D-033).
        builder.HasAlternateKey(x => new { x.Id, x.TenantId });
        builder.HasOne(x => x.Company).WithMany(x => x.FiscalPeriods)
            .HasForeignKey(x => x.TenantId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
    }
}