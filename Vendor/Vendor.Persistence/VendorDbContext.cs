using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Persistence;

public sealed class VendorDbContext(
    DbContextOptions<VendorDbContext> options)
    : IdentityDbContext<VendorAdminUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<VendorAuditEvent> AuditEvents => Set<VendorAuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("vendor");

        builder.Entity<VendorAdminUser>(entity =>
        {
            entity.ToTable("AdminUsers");
            entity.Property(user => user.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(user => user.IsEnabled).HasDefaultValue(true);
            entity.Property(user => user.CreatedAtUtc).IsRequired();
            entity.Property(user => user.DisabledAtUtc);
        });

        builder.Entity<IdentityRole<Guid>>().ToTable("AdminRoles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("AdminUserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("AdminUserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("AdminUserLogins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("AdminRoleClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("AdminUserTokens");

        builder.Entity<VendorAuditEvent>(entity =>
        {
            entity.ToTable("AuditEvents");
            entity.HasKey(auditEvent => auditEvent.Id);
            entity.Property(auditEvent => auditEvent.Action).HasMaxLength(120).IsRequired();
            entity.Property(auditEvent => auditEvent.Outcome).HasMaxLength(32).IsRequired();
            entity.Property(auditEvent => auditEvent.CorrelationId).HasMaxLength(128).IsRequired();
            entity.Property(auditEvent => auditEvent.TargetType).HasMaxLength(120).IsRequired();
            entity.Property(auditEvent => auditEvent.TargetId).HasMaxLength(200);
            entity.Property(auditEvent => auditEvent.DetailsJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(auditEvent => auditEvent.PreviousHash).HasColumnType("varbinary(32)").IsRequired();
            entity.Property(auditEvent => auditEvent.EventHash).HasColumnType("varbinary(32)").IsRequired();
            entity.HasIndex(auditEvent => auditEvent.OccurredAtUtc);
            entity.HasIndex(auditEvent => auditEvent.CorrelationId);
            // ActorUserId intentionally has no FK: audit history must survive account deletion.
        });
    }
}
