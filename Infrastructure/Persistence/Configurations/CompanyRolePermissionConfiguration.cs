using Microsoft.EntityFrameworkCore;

namespace Data.Configurations;

internal sealed class CompanyRolePermissionConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.CompanyRolePermission>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.CompanyRolePermission> builder)
    {
        builder.ToTable("CompanyRolePermissions");
        builder.HasKey(x => new { x.RoleId, x.PermissionId });
        builder.HasOne(x => x.Role).WithMany(x => x.RolePermissions)
            .HasForeignKey(x => x.RoleId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade);
        builder.HasOne(x => x.Permission).WithMany(x => x.RolePermissions)
            .HasForeignKey(x => x.PermissionId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
    }
}