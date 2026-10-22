using Microsoft.EntityFrameworkCore;

namespace Data.Configurations;

internal sealed class ConsolidationRunConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.ConsolidationRun>
{
	public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.ConsolidationRun> builder)
	{
		builder.ToTable("ConsolidationRuns");
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Kind).IsRequired();
		builder.Property(x => x.Status).IsRequired();
		builder.Property(x => x.FailureCode).HasMaxLength(80).IsUnicode(false);
		builder.Property(x => x.StartedAtUtc).IsRequired();
		builder.HasIndex(x => new { x.CorporateGroupId, x.StartedAtUtc });
		builder.HasOne(x => x.CorporateGroup).WithMany(x => x.Runs)
			.HasForeignKey(x => x.CorporateGroupId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
		builder.HasOne(x => x.ConsolidationProfile).WithMany(x => x.Runs)
			.HasForeignKey(x => new { x.ConsolidationProfileId, x.CorporateGroupId })
			.HasPrincipalKey(x => new { x.Id, x.CorporateGroupId })
			.OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
	}
}
