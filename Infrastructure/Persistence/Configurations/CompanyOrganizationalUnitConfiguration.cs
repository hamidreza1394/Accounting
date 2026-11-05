using Microsoft.EntityFrameworkCore;

namespace Data.Configurations;

internal sealed class CompanyOrganizationalUnitConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.CompanyOrganizationalUnit>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.CompanyOrganizationalUnit> builder)
    {
        builder.ToTable("CompanyOrganizationalUnits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ParentUnitId });
        builder.HasAlternateKey(x => new { x.Id, x.TenantId });
        builder.HasOne(x => x.Company).WithMany(x => x.OrganizationalUnits)
            .HasForeignKey(x => x.TenantId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        builder.HasOne(x => x.ParentUnit).WithMany(x => x.ChildUnits)
            .HasForeignKey(x => new { x.ParentUnitId, x.TenantId })
            .HasPrincipalKey(x => new { x.Id, x.TenantId })
            .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
    }
}
