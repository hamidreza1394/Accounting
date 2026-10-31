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
    public DbSet<Domain.VendorCustomer> Customers => Set<Domain.VendorCustomer>();
    public DbSet<Domain.VendorInstallation> Installations => Set<Domain.VendorInstallation>();
    public DbSet<Domain.VendorContract> Contracts => Set<Domain.VendorContract>();
    public DbSet<Domain.VendorLicense> Licenses => Set<Domain.VendorLicense>();
    public DbSet<Domain.VendorApprovalRequest> ApprovalRequests => Set<Domain.VendorApprovalRequest>();
    public DbSet<Domain.VendorSignedLicenseEnvelope> SignedLicenseEnvelopes => Set<Domain.VendorSignedLicenseEnvelope>();
    public DbSet<Domain.VendorOfflineRevocationSnapshot> OfflineRevocationSnapshots => Set<Domain.VendorOfflineRevocationSnapshot>();
    public DbSet<Domain.VendorOfflineRevocationSequence> OfflineRevocationSequences => Set<Domain.VendorOfflineRevocationSequence>();
    public DbSet<Domain.VendorProvisioningRequest> ProvisioningRequests => Set<Domain.VendorProvisioningRequest>();

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
            // Keep the established Identity schema contract explicit and snapshot-stable.
            entity.Property(user => user.PhoneNumber).HasMaxLength(256);
        });

        builder.Entity<IdentityRole<Guid>>().ToTable("VendorAdminRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("VendorAdminUserClaims");
        builder.Entity<IdentityUserLogin<Guid>>(entity =>
        {
            entity.ToTable("VendorAdminUserLogins");
            entity.Property(login => login.LoginProvider).HasMaxLength(128);
            entity.Property(login => login.ProviderKey).HasMaxLength(128);
        });
        builder.Entity<IdentityUserRole<Guid>>().ToTable("VendorAdminUserRoles");
        builder.Entity<IdentityUserToken<Guid>>(entity =>
        {
            entity.ToTable("VendorAdminUserTokens");
            entity.Property(token => token.LoginProvider).HasMaxLength(128);
            entity.Property(token => token.Name).HasMaxLength(128);
        });
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("VendorAdminRoleClaims");
        builder.Entity<IdentityUserPasskey<Guid>>(entity =>
        {
            entity.HasKey(passkey => passkey.CredentialId);
            entity.ToTable("VendorAdminUserPasskeys");
            entity.Property(passkey => passkey.CredentialId).HasMaxLength(1024);
            entity.HasIndex(passkey => passkey.UserId);
            entity.HasOne<VendorAdminUser>()
                .WithMany()
                .HasForeignKey(passkey => passkey.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.OwnsOne(passkey => passkey.Data).ToJson();
        });

        builder.Entity<Domain.VendorCustomer>(entity =>
        {
            entity.ToTable("VendorCustomers");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.CustomerNumber).HasMaxLength(32).IsRequired();
            entity.Property(item => item.LegalName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.CreatedAtUtc).HasDefaultValueSql("CONVERT(datetimeoffset, SYSUTCDATETIME())");
            entity.Property(item => item.UpdatedAtUtc).IsRequired();
            entity.Property(item => item.RowVersion).IsRowVersion();
            entity.HasIndex(item => item.CustomerNumber).IsUnique();
        });

        builder.Entity<Domain.VendorInstallation>(entity =>
        {
            entity.ToTable("VendorInstallations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.InstallationCode).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Environment).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.CreatedAtUtc).HasDefaultValueSql("CONVERT(datetimeoffset, SYSUTCDATETIME())");
            entity.Property(item => item.RowVersion).IsRowVersion();
            entity.HasIndex(item => item.InstallationCode).IsUnique();
            entity.HasIndex(item => item.CustomerId);
            entity.HasOne(item => item.Customer).WithMany(customer => customer.Installations)
                .HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Domain.VendorContract>(entity =>
        {
            entity.ToTable("VendorContracts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ContractNumber).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.CurrencyCode).HasMaxLength(3).IsUnicode(false).IsRequired();
            entity.Property(item => item.CreatedAtUtc).HasDefaultValueSql("CONVERT(datetimeoffset, SYSUTCDATETIME())");
            entity.Property(item => item.RowVersion).IsRowVersion();
            entity.HasIndex(item => new { item.CustomerId, item.ContractNumber }).IsUnique();
            entity.HasIndex(item => item.CustomerId);
            entity.HasOne(item => item.Customer).WithMany(customer => customer.Contracts)
                .HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Domain.VendorLicense>(entity =>
        {
            entity.ToTable("VendorLicenses", table =>
            {
                table.HasCheckConstraint("CK_VendorLicenses_LicenseKind", "[LicenseKind] IN (N'term', N'perpetual')");
                table.HasCheckConstraint("CK_VendorLicenses_KindExpiry", "([LicenseKind] = N'term' AND [ValidUntilUtc] IS NOT NULL) OR ([LicenseKind] = N'perpetual' AND [ValidUntilUtc] IS NULL)");
                table.HasCheckConstraint("CK_VendorLicenses_MaxCompanies", "[MaxCompanies] IS NULL OR [MaxCompanies] >= 1");
                table.HasCheckConstraint("CK_VendorLicenses_GraceDays", "[GraceDays] >= 0 AND [GraceDays] <= 90");
                table.HasCheckConstraint("CK_VendorLicenses_LastIssuedSequence", "[LastIssuedSequence] >= 0");
                table.HasCheckConstraint("CK_VendorLicenses_SubsystemsJson", "ISJSON([SubsystemsJson]) = 1");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.LicenseNumber).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ProductCode).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.LicenseKind).HasMaxLength(16).HasDefaultValue("perpetual").IsRequired();
            entity.Property(item => item.SubsystemsJson).HasMaxLength(512).HasDefaultValue("[]").IsRequired();
            entity.Property(item => item.GraceDays).HasDefaultValue(30).IsRequired();
            entity.Property(item => item.LastIssuedSequence).HasDefaultValue(0).IsRequired();
            entity.Property(item => item.CreatedAtUtc).HasDefaultValueSql("CONVERT(datetimeoffset, SYSUTCDATETIME())");
            entity.Property(item => item.RowVersion).IsRowVersion();
            entity.HasIndex(item => item.LicenseNumber).IsUnique();
            entity.HasIndex(item => item.CustomerId);
            entity.HasIndex(item => item.ContractId);
            entity.HasIndex(item => item.InstallationId);
            entity.HasOne(item => item.Customer).WithMany(customer => customer.Licenses)
                .HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Contract).WithMany(contract => contract.Licenses)
                .HasForeignKey(item => item.ContractId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Installation).WithMany(installation => installation.Licenses)
                .HasForeignKey(item => item.InstallationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Domain.VendorProvisioningRequest>(entity =>
        {
            entity.ToTable("VendorProvisioningRequests", table =>
                table.HasCheckConstraint("CK_VendorProvisioningRequests_Status",
                    "[Status] IN (N'AwaitingSigner', N'PendingDelivery', N'Confirmed', N'Failed')"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.RequestId).HasMaxLength(36).IsRequired();
            entity.Property(item => item.Nonce).HasMaxLength(128).IsRequired();
            entity.Property(item => item.PayloadJson).IsRequired();
            entity.Property(item => item.PayloadSha256).IsUnicode(false).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.Status).HasMaxLength(24).IsRequired();
            entity.Property(item => item.KeyId).HasMaxLength(128);
            entity.Property(item => item.Algorithm).HasMaxLength(32).IsUnicode(false);
            entity.Property(item => item.SignatureBase64).HasMaxLength(128).IsUnicode(false);
            entity.Property(item => item.FailureCode).HasMaxLength(128).IsUnicode(false);
            entity.Property(item => item.AcknowledgementSha256).HasMaxLength(64).IsUnicode(false).IsFixedLength();
            entity.Property(item => item.AcknowledgementKeyId).HasMaxLength(128);
            entity.Property(item => item.AcknowledgementResult).HasMaxLength(16).IsUnicode(false);
            entity.Property(item => item.CreatedAtUtc)
                .HasDefaultValueSql("CONVERT(datetimeoffset, SYSUTCDATETIME())");
            entity.Property(item => item.RowVersion).IsRowVersion();
            entity.HasIndex(item => item.RequestId).IsUnique();
            entity.HasIndex(item => item.Nonce).IsUnique();
            entity.HasIndex(item => item.CustomerId);
            entity.HasIndex(item => item.InstallationId);
            entity.HasOne<Domain.VendorCustomer>().WithMany()
                .HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Domain.VendorInstallation>().WithMany()
                .HasForeignKey(item => item.InstallationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Domain.VendorSignedLicenseEnvelope>(entity =>
        {
            entity.ToTable("VendorSignedLicenseEnvelopes", table =>
                table.HasCheckConstraint("CK_VendorSignedLicenseEnvelopes_Sequence", "[Sequence] >= 1"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.KeyId).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Algorithm).HasMaxLength(32).IsUnicode(false).IsRequired();
            entity.Property(item => item.EnvelopeJson).IsRequired();
            entity.Property(item => item.PayloadSha256).HasMaxLength(64).IsUnicode(false).IsFixedLength().IsRequired();
            entity.Property(item => item.CreatedAtUtc).HasDefaultValueSql("CONVERT(datetimeoffset, SYSUTCDATETIME())");
            entity.Property(item => item.RowVersion).IsRowVersion();
            entity.HasIndex(item => new { item.LicenseId, item.Sequence }).IsUnique();
            entity.HasIndex(item => item.PayloadSha256);
            entity.HasOne<Domain.VendorLicense>()
                .WithMany()
                .HasForeignKey(item => item.LicenseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Domain.VendorOfflineRevocationSnapshot>(entity =>
        {
            entity.ToTable("VendorOfflineRevocationSnapshots", table =>
            {
                table.HasCheckConstraint("CK_VendorOfflineRevocationSnapshots_Sequence", "[Sequence] >= 1");
                table.HasCheckConstraint("CK_VendorOfflineRevocationSnapshots_Expiry", "[ExpiresAtUtc] > [IssuedAtUtc]");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ListId).HasMaxLength(128).IsRequired();
            entity.Property(item => item.KeyId).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Algorithm).HasMaxLength(32).IsUnicode(false).IsRequired();
            entity.Property(item => item.EnvelopeJson).IsRequired();
            entity.Property(item => item.PayloadSha256).HasMaxLength(64).IsUnicode(false).IsFixedLength().IsRequired();
            entity.Property(item => item.CreatedAtUtc)
                .HasDefaultValueSql("CONVERT(datetimeoffset, SYSUTCDATETIME())");
            entity.Property(item => item.RowVersion).IsRowVersion();
            entity.HasIndex(item => item.Sequence).IsUnique();
            entity.HasIndex(item => item.ListId).IsUnique();
            entity.HasIndex(item => item.PayloadSha256);
        });

        builder.Entity<Domain.VendorOfflineRevocationSequence>(entity =>
        {
            entity.ToTable("VendorOfflineRevocationSequences", table =>
            {
                table.HasCheckConstraint("CK_VendorOfflineRevocationSequences_Singleton", "[Id] = 1");
                table.HasCheckConstraint("CK_VendorOfflineRevocationSequences_LastSequence", "[LastSequence] >= 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.LastSequence).IsRequired();
            entity.HasData(new Domain.VendorOfflineRevocationSequence { Id = 1, LastSequence = 0 });
        });

        builder.Entity<Domain.VendorApprovalRequest>(entity =>
        {
            entity.ToTable("VendorApprovalRequests");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ResourceType).HasMaxLength(30).IsRequired();
            entity.Property(item => item.TargetStatus).HasMaxLength(20).IsRequired();
            entity.Property(item => item.ExpectedResourceRowVersion).HasColumnType("binary(8)").IsRequired();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.ReviewComment).HasMaxLength(500);
            entity.Property(item => item.RowVersion).IsRowVersion();
            entity.HasIndex(item => new { item.ResourceType, item.ResourceId })
                .IsUnique()
                .HasFilter("[Status] = N'Pending'");
            entity.HasIndex(item => new { item.Status, item.RequestedAtUtc });
            entity.HasIndex(item => item.RequestedByUserId);
            entity.HasIndex(item => item.ReviewedByUserId);
        });

        builder.Entity<VendorAuditEvent>(entity =>
        {
            entity.ToTable("VendorAuditEvents");
            entity.HasKey(auditEvent => auditEvent.Id);
            entity.Property(auditEvent => auditEvent.ActionCode).HasMaxLength(100).IsRequired();
            entity.Property(auditEvent => auditEvent.HashVersion).HasDefaultValue(1).IsRequired();
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
            entity.HasIndex(auditEvent => auditEvent.PreviousHash).IsUnique();
            entity.HasIndex(auditEvent => auditEvent.EventHash).IsUnique();
            entity.Property(auditEvent => auditEvent.OccurredAtUtc)
                .HasDefaultValueSql("CONVERT(datetimeoffset, SYSUTCDATETIME())");
        });
    }
}
