using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Configurations;

internal sealed class ProvisioningRequestClaimConfiguration
    : IEntityTypeConfiguration<Domain.ProvisioningRequestClaim>
{
    public void Configure(EntityTypeBuilder<Domain.ProvisioningRequestClaim> builder)
    {
        builder.ToTable("ProvisioningRequestClaims");
        builder.HasKey(claim => claim.Id);

        builder.Property(claim => claim.NonceHash).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(claim => claim.PayloadSha256).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(claim => claim.CustomerReference).HasMaxLength(128).IsUnicode(false).IsRequired();
        builder.Property(claim => claim.InstallationId).HasMaxLength(128).IsUnicode(false).IsRequired();
        builder.Property(claim => claim.EnrollmentTokenHash).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(claim => claim.EnrollmentExpiresAtUtc).IsRequired();
        builder.Property(claim => claim.AppliedAtUtc).IsRequired();
        builder.Property(claim => claim.MustChangePassword).IsRequired();
        builder.Property(claim => claim.RowVersion).IsRowVersion();

        builder.HasIndex(claim => claim.RequestId).IsUnique();
        builder.HasIndex(claim => claim.NonceHash).IsUnique();
        builder.HasIndex(claim => claim.EnrollmentTokenHash).IsUnique();
        builder.HasIndex(claim => new { claim.AdminUserId, claim.MustChangePassword });
        builder.HasIndex(claim => new { claim.CustomerReference, claim.InstallationId });

        // The composite FK guarantees the claimed fiscal period belongs to the same company.
        builder.HasOne<Domain.FiscalPeriod>()
            .WithMany()
            .HasForeignKey(claim => new { claim.FiscalPeriodId, claim.CompanyId })
            .HasPrincipalKey(period => new { period.Id, period.TenantId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
