using Microsoft.EntityFrameworkCore;

namespace Data.Configurations;

internal sealed class UserSessionConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.UserSession>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.UserSession> builder)
    {
        builder.ToTable("UserSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.LastSeenAtUtc).IsRequired();
        builder.Property(x => x.RevocationReasonCode).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.RowVersion).IsRowVersion();
        // The list of active sessions of one user and the idle-window check both read by
        // user; no tenant column exists by design (D-069).
        builder.HasIndex(x => new { x.UserId, x.LastSeenAtUtc });
        builder.HasIndex(x => x.LastSeenAtUtc);
        builder.HasOne(x => x.User).WithMany(x => x.Sessions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
    }
}
