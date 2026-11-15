using Microsoft.EntityFrameworkCore;

namespace Template.Tests.TenantValidation;

// D5 / scenario R1: the same sample entity before and after renaming its legacy "TenantId" property to
// "CompanyId" WITHOUT pinning the constraint names. Test assembly only; never part of the application model.

public sealed class RtOwner
{
	public Guid Id { get; set; }
}

public sealed class RtPeriodBefore
{
	public Guid Id { get; set; }
	public Guid TenantId { get; set; }
	public string Status { get; set; } = "";
}

public sealed class RtPeriodAfter
{
	public Guid Id { get; set; }
	public Guid CompanyId { get; set; }
	public string Status { get; set; } = "";
}

/// <summary>A table that references the alternate key of the period (like ProvisioningRequestClaims -> FiscalPeriods).</summary>
public sealed class RtClaim
{
	public Guid Id { get; set; }
	public Guid PeriodId { get; set; }
	public Guid CompanyId { get; set; }
}

public sealed class UnpinnedBeforeContext(DbContextOptions<UnpinnedBeforeContext> options) : DbContext(options)
{
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<RtOwner>(builder => { builder.ToTable("TvrOwners"); builder.HasKey(x => x.Id); });
		modelBuilder.Entity<RtPeriodBefore>(builder =>
		{
			builder.ToTable("TvrPeriods");
			builder.HasKey(x => x.Id);
			builder.HasAlternateKey(x => new { x.Id, x.TenantId });
			builder.HasOne<RtOwner>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
			builder.HasIndex(x => new { x.TenantId, x.Status });
		});
		modelBuilder.Entity<RtClaim>(builder =>
		{
			builder.ToTable("TvrClaims");
			builder.HasKey(x => x.Id);
			builder.HasOne<RtPeriodBefore>().WithMany().HasForeignKey(x => new { x.PeriodId, x.CompanyId })
				.HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Restrict);
		});
	}
}

public sealed class UnpinnedAfterContext(DbContextOptions<UnpinnedAfterContext> options) : DbContext(options)
{
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<RtOwner>(builder => { builder.ToTable("TvrOwners"); builder.HasKey(x => x.Id); });
		modelBuilder.Entity<RtPeriodAfter>(builder =>
		{
			builder.ToTable("TvrPeriods");
			builder.HasKey(x => x.Id);
			builder.HasAlternateKey(x => new { x.Id, x.CompanyId });
			builder.HasOne<RtOwner>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
			builder.HasIndex(x => new { x.CompanyId, x.Status });
		});
		modelBuilder.Entity<RtClaim>(builder =>
		{
			builder.ToTable("TvrClaims");
			builder.HasKey(x => x.Id);
			builder.HasOne<RtPeriodAfter>().WithMany().HasForeignKey(x => new { x.PeriodId, x.CompanyId })
				.HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
		});
	}
}
