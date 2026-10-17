using Microsoft.EntityFrameworkCore;

namespace Data.Configurations;

internal sealed class SecurityAuditEventConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.SecurityAuditEvent>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.SecurityAuditEvent> builder)
    {
        builder.ToTable("SecurityAuditEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(120).IsUnicode(false).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(120).IsUnicode(false);
        builder.Property(x => x.EntityId).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.CorrelationId).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.IpAddress).HasMaxLength(45).IsUnicode(false);
        builder.Property(x => x.DetailsJson).HasMaxLength(4000);
        builder.HasIndex(x => new { x.TenantId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.UserId, x.OccurredAtUtc });
        builder.HasIndex(x => x.EventType);
    }
}