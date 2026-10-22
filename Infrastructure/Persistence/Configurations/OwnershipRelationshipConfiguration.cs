using Microsoft.EntityFrameworkCore;

namespace Data.Configurations;

internal sealed class OwnershipRelationshipConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.OwnershipRelationship>
{
	public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.OwnershipRelationship> builder)
	{
		builder.ToTable("OwnershipRelationships");
		builder.HasKey(x => x.Id);
		// Percentages 0..100 with six decimals (for example 33.333333).
		builder.Property(x => x.OwnershipPercentage).HasColumnType("decimal(9,6)");
		builder.Property(x => x.VotingPercentage).HasColumnType("decimal(9,6)");
		builder.Property(x => x.ControlBasis).IsRequired();
		builder.Property(x => x.CreatedAtUtc).IsRequired();
		builder.HasIndex(x => new { x.ParentCompanyId, x.SubsidiaryCompanyId, x.ValidFrom }).IsUnique();
		builder.HasIndex(x => x.SubsidiaryCompanyId);
		// The control edges are read inside a serializable transaction to detect cycles.
		builder.HasIndex(x => x.ControlBasis);
		builder.HasOne(x => x.ParentCompany).WithMany(x => x.OwnedCompanies)
			.HasForeignKey(x => x.ParentCompanyId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
		builder.HasOne(x => x.SubsidiaryCompany).WithMany(x => x.Owners)
			.HasForeignKey(x => x.SubsidiaryCompanyId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
	}
}
