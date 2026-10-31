using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Configurations;

internal sealed class InstalledLicenseConfiguration
    : IEntityTypeConfiguration<Domain.InstalledLicense>
{
    public void Configure(EntityTypeBuilder<Domain.InstalledLicense> builder)
    {
        builder.ToTable("InstalledLicenses");
        builder.HasKey(license => license.Id);

        builder.Property(license => license.InstallationId)
            .HasMaxLength(128)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(license => license.LicenseId)
            .HasMaxLength(128)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(license => license.Kid)
            .HasMaxLength(128)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(license => license.Algorithm)
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(license => license.PayloadJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();
        builder.Property(license => license.SignatureBase64)
            .HasMaxLength(128)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(license => license.EnvelopeJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();
        builder.Property(license => license.PayloadDigest)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(license => license.State)
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(license => license.Sequence).IsRequired();
        builder.Property(license => license.FormatVersion).IsRequired();
        builder.Property(license => license.ImportedAtUtc).IsRequired();
        builder.Property(license => license.RowVersion).IsRowVersion();

        builder.HasOne<Domain.InstallationIdentity>()
            .WithMany()
            .HasForeignKey(license => new { license.InstallationIdentityId, license.InstallationId })
            .HasPrincipalKey(identity => new { identity.Id, identity.InstallationId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(license => new { license.InstallationId, license.Sequence })
            .IsUnique();
        builder.HasIndex(license => new { license.LicenseId, license.Sequence })
            .IsUnique();
        builder.HasIndex(license => license.InstallationId)
            .IsUnique()
            .HasFilter("[State] = 'Current'");
    }
}
