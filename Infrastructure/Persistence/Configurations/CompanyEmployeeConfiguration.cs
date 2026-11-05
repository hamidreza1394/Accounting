using Microsoft.EntityFrameworkCore;

namespace Data.Configurations;

internal sealed class CompanyEmployeeConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.CompanyEmployee>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.CompanyEmployee> builder)
    {
        builder.ToTable("CompanyEmployees");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EmployeeNumber).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.TenantId, x.EmployeeNumber }).IsUnique();
        // One membership of the company can be the login anchor of at most one employee.
        builder.HasIndex(x => new { x.UserCompanyMembershipId, x.TenantId }).IsUnique()
            .HasFilter("[UserCompanyMembershipId] IS NOT NULL");
        builder.HasAlternateKey(x => new { x.Id, x.TenantId });
        builder.HasOne(x => x.Company).WithMany(x => x.Employees)
            .HasForeignKey(x => x.TenantId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        builder.HasOne(x => x.Membership).WithMany(x => x.Employees)
            .HasForeignKey(x => new { x.UserCompanyMembershipId, x.TenantId })
            .HasPrincipalKey(x => new { x.Id, x.TenantId })
            .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
    }
}
