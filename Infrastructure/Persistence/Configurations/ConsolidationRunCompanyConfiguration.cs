using Microsoft.EntityFrameworkCore;

namespace Data.Configurations;

internal sealed class ConsolidationRunCompanyConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.ConsolidationRunCompany>
{
	public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.ConsolidationRunCompany> builder)
	{
		builder.ToTable("ConsolidationRunCompanies");
		builder.HasKey(x => new { x.ConsolidationRunId, x.CompanyId });
		builder.Property(x => x.MemberRole).IsRequired();
		builder.HasOne(x => x.ConsolidationRun).WithMany(x => x.Companies)
			.HasForeignKey(x => x.ConsolidationRunId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
		builder.HasOne(x => x.Company).WithMany(x => x.ConsolidationRuns)
			.HasForeignKey(x => x.CompanyId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
		// The fiscal period must belong to the same company: (FiscalPeriodId, CompanyId)
		// references the alternate key (Id, TenantId) of FiscalPeriods.
		builder.HasOne(x => x.FiscalPeriod).WithMany(x => x.ConsolidationRuns)
			.HasForeignKey(x => new { x.FiscalPeriodId, x.CompanyId })
			.HasPrincipalKey(x => new { x.Id, x.TenantId })
			.OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
	}
}
