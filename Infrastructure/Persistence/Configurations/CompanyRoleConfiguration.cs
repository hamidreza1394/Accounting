namespace Data.Configurations;

internal sealed class CompanyRoleConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.CompanyRole>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.CompanyRole> builder)
    {
        builder.ToTable("CompanyRoles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasAlternateKey(x => new { x.Id, x.TenantId });
        builder.HasOne(x => x.Company).WithMany(x => x.Roles)
            .HasForeignKey(x => x.TenantId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
    }
}