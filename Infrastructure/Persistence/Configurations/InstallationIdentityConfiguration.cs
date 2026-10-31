using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Configurations;

internal sealed class InstallationIdentityConfiguration
    : IEntityTypeConfiguration<Domain.InstallationIdentity>
{
    public void Configure(EntityTypeBuilder<Domain.InstallationIdentity> builder)
    {
        builder.ToTable("InstallationIdentities");
        builder.HasKey(identity => identity.Id);

        builder.Property(identity => identity.InstallationId)
            .HasMaxLength(128)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(identity => identity.KeyId)
            .HasMaxLength(128)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(identity => identity.ProviderName)
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(identity => identity.AssuranceLevel)
            .HasMaxLength(24)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(identity => identity.OsInstallationMarkerDigest)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(identity => identity.PublicKeyFingerprint)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(identity => identity.PublicKeySubjectPublicKeyInfoBase64)
            .HasMaxLength(512)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(identity => identity.SoftwareProviderRiskAcceptedBy)
            .HasMaxLength(256)
            .IsUnicode(false);
        builder.Property(identity => identity.State)
            .HasMaxLength(24)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(identity => identity.KeyVersion).IsRequired();
        builder.Property(identity => identity.ProvisionedAtUtc).IsRequired();
        builder.Property(identity => identity.RowVersion).IsRowVersion();

        builder.HasIndex(identity => new { identity.InstallationId, identity.KeyVersion })
            .IsUnique();
        builder.HasIndex(identity => identity.KeyId)
            .IsUnique();
        builder.HasIndex(identity => identity.InstallationId)
            .IsUnique()
            .HasFilter("[State] = 'Current'");
    }
}
