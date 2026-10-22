using Microsoft.EntityFrameworkCore;

namespace Data.Configurations;

internal sealed class CorporateGroupConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.CorporateGroup>
{
	public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.CorporateGroup> builder)
	{
		builder.ToTable("CorporateGroups");
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Code).HasMaxLength(40).IsUnicode(false).IsRequired();
		builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
		builder.Property(x => x.CreatedAtUtc).IsRequired();
		// Unique per owner company, not per installation: a duplicate code must not reveal
		// that another company has a group with that code (D-034).
		builder.HasIndex(x => new { x.OwnerCompanyId, x.Code }).IsUnique();
		builder.HasIndex(x => new { x.OwnerCompanyId, x.IsActive });
		builder.HasOne(x => x.OwnerCompany).WithMany(x => x.AdministeredGroups)
			.HasForeignKey(x => x.OwnerCompanyId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
	}
}
