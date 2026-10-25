using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Persistence;

/// <summary>Owns only Vendor control-plane identity and audit state; never reuse the customer DbContext.</summary>
public sealed class VendorDbContext(DbContextOptions<VendorDbContext> options)
    : IdentityDbContext<VendorAdminUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<VendorAuditEvent> AuditEvents => Set<VendorAuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<VendorAdminUser>(entity =>
        {
            entity.ToTable("VendorAdminUsers");
            entity.Property(user => user.IsEnabled).HasDefaultValue(false);
            entity.Property(user => user.MustChangePassword).HasDefaultValue(true);
            entity.Property(user => user.CreatedAtUtc)
                .HasDefaultValueSql("CONVERT(datetimeoffset, SYSUTCDATETIME())");
        });

        builder.Entity<IdentityRole<Guid>>().ToTable("VendorAdminRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("VendorAdminUserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("VendorAdminUserLogins");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("VendorAdminUserRoles");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("VendorAdminUserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("VendorAdminRoleClaims");

        builder.Entity<VendorAuditEvent>(entity =>
        {
            entity.ToTable("VendorAuditEvents");
            entity.HasKey(auditEvent => auditEvent.Id);
            entity.Property(auditEvent => auditEvent.ActionCode).HasMaxLength(100).IsRequired();
            entity.Property(auditEvent => auditEvent.SubjectType).HasMaxLength(100);
            entity.Property(auditEvent => auditEvent.SubjectId).HasMaxLength(200);
            entity.Property(auditEvent => auditEvent.CorrelationId).HasMaxLength(100);
            entity.Property(auditEvent => auditEvent.MetadataJson).HasMaxLength(4000);
            entity.Property(auditEvent => auditEvent.PreviousHash)
                .HasMaxLength(64).IsUnicode(false).IsFixedLength().IsRequired();
            entity.Property(auditEvent => auditEvent.EventHash)
                .HasMaxLength(64).IsUnicode(false).IsFixedLength().IsRequired();
            entity.HasIndex(auditEvent => auditEvent.OccurredAtUtc);
            entity.HasIndex(auditEvent => auditEvent.ActorUserId);
            entity.HasIndex(auditEvent => auditEvent.ActionCode);
            entity.Property(auditEvent => auditEvent.OccurredAtUtc)
                .HasDefaultValueSql("CONVERT(datetimeoffset, SYSUTCDATETIME())");
        });
    }
}
