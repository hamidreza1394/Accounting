using Microsoft.EntityFrameworkCore;

namespace Data.Configurations;

internal sealed class CompanyMembershipRequestConfiguration :
    Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.CompanyMembershipRequest>
{
    public void Configure(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.CompanyMembershipRequest> builder)
    {
        builder.ToTable("CompanyMembershipRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.RequestedAtUtc).IsRequired();
        builder.Property(x => x.DecisionReason).HasMaxLength(500);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => new { x.UserId, x.DestinationTenantId })
            .IsUnique()
            .HasFilter("[Status] = 'Pending'");
        builder.HasIndex(x => new { x.DestinationTenantId, x.Status, x.RequestedAtUtc });

        builder.HasOne(x => x.DestinationCompany).WithMany(x => x.MembershipRequestsAsDestination)
            .HasForeignKey(x => x.DestinationTenantId)
            .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovedRole).WithMany(x => x.MembershipRequests)
            .HasForeignKey(x => new { x.ApprovedRoleId, x.DestinationTenantId })
            .HasPrincipalKey(x => new { x.Id, x.TenantId })
            .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
    }
}
