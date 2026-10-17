namespace Data.Configurations;

internal sealed class CompanyRoleConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.CompanyRole>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.CompanyRole> builder)
    {
        builder.ToTable("CompanyRoles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        builder.HasOne(x => x.Company).WithMany(x => x.Roles)
            .HasForeignKey(x => x.CompanyId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
    }
}