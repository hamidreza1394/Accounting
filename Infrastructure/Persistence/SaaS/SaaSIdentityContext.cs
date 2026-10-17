using Application.SaaS;
using Domain.SaaS;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Data.SaaS;

/// <summary>SaaS identity and tenant schema; migration history is separate from the legacy context.</summary>
public sealed class SaaSIdentityContext(DbContextOptions<SaaSIdentityContext> options, ICurrentTenant currentTenant)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    private Guid? CurrentTenantId => currentTenant.TenantId;

    public DbSet<TenantCompany> Companies => Set<TenantCompany>();
    public DbSet<CompanyMembership> CompanyMemberships => Set<CompanyMembership>();
    public DbSet<FiscalPeriod> FiscalPeriods => Set<FiscalPeriod>();
    public DbSet<CompanyRole> CompanyRoles => Set<CompanyRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<CompanyRolePermission> CompanyRolePermissions => Set<CompanyRolePermission>();
    public DbSet<AccountingDocument> AccountingDocuments => Set<AccountingDocument>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("saas");
        base.OnModelCreating(b);
        b.Entity<ApplicationUser>(e => { e.ToTable("Users"); e.Property(x => x.DisplayName).HasMaxLength(200); e.Property(x => x.CreatedAtUtc).IsRequired(); e.HasIndex(x => x.NormalizedEmail); });
        b.Entity<TenantCompany>(e => { e.ToTable("Companies"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(200).IsRequired(); e.Property(x => x.NationalId).HasMaxLength(20); e.HasIndex(x => x.NationalId); e.Property(x => x.CreatedAtUtc).IsRequired(); });
        b.Entity<CompanyMembership>(e => { e.ToTable("CompanyMemberships"); e.HasKey(x => new { x.TenantId, x.UserId }); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); e.HasOne<TenantCompany>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict); e.HasOne<CompanyRole>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyRoleId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict); e.HasIndex(x => new { x.UserId, x.IsActive }); });
        b.Entity<FiscalPeriod>(e => { e.ToTable("FiscalPeriods"); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id }); e.Property(x => x.Name).HasMaxLength(100).IsRequired(); e.Property(x => x.Status).HasConversion<int>(); e.HasOne<TenantCompany>().WithMany().HasForeignKey(x => x.TenantId).HasPrincipalKey(x => x.Id).OnDelete(DeleteBehavior.Restrict); e.HasIndex(x => new { x.TenantId, x.StartDate, x.EndDate }); e.Property(x => x.CreatedAtUtc).IsRequired(); e.HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId); });
        b.Entity<CompanyRole>(e => { e.ToTable("CompanyRoles"); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.TenantId, x.Id }); e.Property(x => x.Name).HasMaxLength(100).IsRequired(); e.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired(); e.HasIndex(x => new { x.TenantId, x.NormalizedName }).IsUnique(); e.HasOne<TenantCompany>().WithMany().HasForeignKey(x => x.TenantId).HasPrincipalKey(x => x.Id).OnDelete(DeleteBehavior.Restrict); e.HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId); });
        b.Entity<Permission>(e => { e.ToTable("Permissions"); e.HasKey(x => x.Id); e.Property(x => x.Code).HasMaxLength(150).IsRequired(); e.Property(x => x.Name).HasMaxLength(200).IsRequired(); e.Property(x => x.Description).HasMaxLength(500); e.HasIndex(x => x.Code).IsUnique(); });
        b.Entity<CompanyRolePermission>(e => { e.ToTable("CompanyRolePermissions"); e.HasKey(x => new { x.TenantId, x.CompanyRoleId, x.PermissionId }); e.HasOne<CompanyRole>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyRoleId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade); e.HasOne<Permission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict); e.HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId); });
        b.Entity<AccountingDocument>(e => { e.ToTable("AccountingDocuments"); e.HasKey(x => x.Id); e.Property(x => x.DocumentNumber).HasMaxLength(50).IsRequired(); e.Property(x => x.Description).HasMaxLength(1000).IsRequired(); e.Property(x => x.Status).HasConversion<int>(); e.HasOne<FiscalPeriod>().WithMany().HasForeignKey(x => new { x.TenantId, x.FiscalPeriodId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict); e.HasIndex(x => new { x.TenantId, x.FiscalPeriodId, x.DocumentNumber }).IsUnique(); e.HasIndex(x => new { x.TenantId, x.DocumentDate }); e.Property(x => x.CreatedAtUtc).IsRequired(); e.HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId); });
        b.Entity<AuditLog>(e => { e.ToTable("AuditLogs"); e.HasKey(x => x.Id); e.Property(x => x.EventType).HasMaxLength(150).IsRequired(); e.Property(x => x.EntityType).HasMaxLength(150); e.Property(x => x.EntityId).HasMaxLength(100); e.Property(x => x.CorrelationId).HasMaxLength(100); e.Property(x => x.Details).HasMaxLength(2000); e.HasIndex(x => new { x.TenantId, x.OccurredAtUtc }); e.HasIndex(x => new { x.UserId, x.OccurredAtUtc }); });
    }
}
