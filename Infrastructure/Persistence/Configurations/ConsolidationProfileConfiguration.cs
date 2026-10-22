using Microsoft.EntityFrameworkCore;

namespace Data.Configurations;

internal sealed class ConsolidationProfileConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.ConsolidationProfile>
{
	public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.ConsolidationProfile> builder)
	{
		builder.ToTable("ConsolidationProfiles");
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
		builder.Property(x => x.ReportingCurrencyCode).HasMaxLength(3).IsUnicode(false).IsRequired();
		builder.Property(x => x.AccountingFramework).IsRequired();
		builder.Property(x => x.FiscalPeriodMappingPolicy).IsRequired();
		builder.Property(x => x.CreatedAtUtc).IsRequired();
		builder.HasIndex(x => new { x.CorporateGroupId, x.Name }).IsUnique();
		// The principal of the composite foreign key of ConsolidationRuns: a run can only
		// reference a profile of its own group.
		builder.HasAlternateKey(x => new { x.Id, x.CorporateGroupId });
		builder.HasOne(x => x.CorporateGroup).WithMany(x => x.Profiles)
			.HasForeignKey(x => x.CorporateGroupId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
	}
}
