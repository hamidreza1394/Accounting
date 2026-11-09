using Microsoft.EntityFrameworkCore;

namespace Data.Configurations;

internal sealed class CompanyEmployeePhotoConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.CompanyEmployeePhoto>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.CompanyEmployeePhoto> builder)
    {
        builder.ToTable("CompanyEmployeePhotos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ContentType).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.Content).IsRequired();
        // One photo per employee, and only for an employee of the SAME company: the unique index
        // plus the composite foreign key to the employee's (Id, TenantId) alternate key.
        builder.HasIndex(x => new { x.EmployeeId, x.TenantId }).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasOne<Domain.CompanyEmployee>().WithMany()
            .HasForeignKey(x => new { x.EmployeeId, x.TenantId })
            .HasPrincipalKey(x => new { x.Id, x.TenantId })
            .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
    }
}
