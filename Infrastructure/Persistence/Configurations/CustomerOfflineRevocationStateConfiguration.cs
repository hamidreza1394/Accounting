using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Configurations;

internal sealed class CustomerOfflineRevocationStateConfiguration
    : IEntityTypeConfiguration<Domain.CustomerOfflineRevocationState>
{
    public void Configure(EntityTypeBuilder<Domain.CustomerOfflineRevocationState> builder)
    {
        builder.ToTable("CustomerOfflineRevocationStates");
        builder.HasKey(state => state.Id);

        builder.Property(state => state.SnapshotDigest)
            .HasMaxLength(64)
            .IsUnicode(false);
        builder.Property(state => state.RevokedLicenseIdsJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();
        builder.Property(state => state.PendingEnvelopeJson)
            .HasColumnType("nvarchar(max)");
        builder.Property(state => state.Sequence).IsRequired();
        builder.Property(state => state.AnchorGeneration).IsRequired();
        builder.Property(state => state.RowVersion).IsRowVersion();

        builder.HasIndex(state => state.Sequence);
    }
}
