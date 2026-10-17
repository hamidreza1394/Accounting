namespace Data.Configurations;

internal sealed class FiscalPeriodConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.FiscalPeriod>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.FiscalPeriod> builder)
    {
        builder.ToTable("FiscalPeriods");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.StartDate, x.EndDate }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.Status });
        builder.HasOne(x => x.Company).WithMany(x => x.FiscalPeriods)
            .HasForeignKey(x => x.CompanyId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
    }
}