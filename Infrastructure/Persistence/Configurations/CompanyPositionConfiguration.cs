using Microsoft.EntityFrameworkCore;

namespace Data.Configurations;

internal sealed class CompanyPositionConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.CompanyPosition>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.CompanyPosition> builder)
    {
        builder.ToTable("CompanyPositions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasAlternateKey(x => new { x.Id, x.TenantId });
        builder.HasOne(x => x.Company).WithMany(x => x.Positions)
            .HasForeignKey(x => x.TenantId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
    }
}
