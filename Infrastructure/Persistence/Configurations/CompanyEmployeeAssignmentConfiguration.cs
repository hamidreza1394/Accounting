using Microsoft.EntityFrameworkCore;

namespace Data.Configurations;

internal sealed class CompanyEmployeeAssignmentConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.CompanyEmployeeAssignment>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.CompanyEmployeeAssignment> builder)
    {
        builder.ToTable("CompanyEmployeeAssignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();
        // Exactly one active assignment per employee of the company (D-072); the
        // application service pre-checks the same fact inside its transaction and the
        // filtered index is the database-side backstop for concurrent requests.
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId }).IsUnique()
            .HasFilter("[Status] = 1 AND [EndOnUtc] IS NULL");
        builder.HasIndex(x => new { x.TenantId, x.OrganizationalUnitId });
        builder.HasIndex(x => new { x.TenantId, x.PositionId });
        builder.HasOne(x => x.Company).WithMany(x => x.EmployeeAssignments)
            .HasForeignKey(x => x.TenantId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        builder.HasOne(x => x.Employee).WithMany(x => x.Assignments)
            .HasForeignKey(x => new { x.EmployeeId, x.TenantId })
            .HasPrincipalKey(x => new { x.Id, x.TenantId })
            .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        builder.HasOne(x => x.OrganizationalUnit).WithMany(x => x.Assignments)
            .HasForeignKey(x => new { x.OrganizationalUnitId, x.TenantId })
            .HasPrincipalKey(x => new { x.Id, x.TenantId })
            .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        builder.HasOne(x => x.Position).WithMany(x => x.Assignments)
            .HasForeignKey(x => new { x.PositionId, x.TenantId })
            .HasPrincipalKey(x => new { x.Id, x.TenantId })
            .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        builder.HasOne(x => x.ManagerEmployee).WithMany(x => x.ManagedAssignments)
            .HasForeignKey(x => new { x.ManagerEmployeeId, x.TenantId })
            .HasPrincipalKey(x => new { x.Id, x.TenantId })
            .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
    }
}
