using Microsoft.EntityFrameworkCore;

namespace Data.Configurations;

internal sealed class UserCompanyMembershipConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.UserCompanyMembership>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.UserCompanyMembership> builder)
    {
        builder.ToTable("UserCompanyMemberships");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.TenantId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IsActive });
        builder.HasOne(x => x.Company).WithMany(x => x.Memberships)
            .HasForeignKey(x => x.TenantId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        builder.HasOne(x => x.Role).WithMany(x => x.Memberships)
            .HasForeignKey(x => new { x.RoleId, x.TenantId })
            .HasPrincipalKey(x => new { x.Id, x.TenantId })
            .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
    }
}