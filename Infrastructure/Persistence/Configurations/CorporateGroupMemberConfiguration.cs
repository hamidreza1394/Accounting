using Microsoft.EntityFrameworkCore;

namespace Data.Configurations;

internal sealed class CorporateGroupMemberConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.CorporateGroupMember>
{
	public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.CorporateGroupMember> builder)
	{
		builder.ToTable("CorporateGroupMembers");
		builder.HasKey(x => x.Id);
		builder.Property(x => x.MemberRole).IsRequired();
		builder.Property(x => x.CreatedAtUtc).IsRequired();
		// A database backstop against duplicates; overlapping periods are rejected by
		// CorporateGroupService inside a serializable transaction (D-033).
		builder.HasIndex(x => new { x.CorporateGroupId, x.CompanyId, x.ValidFrom }).IsUnique();
		builder.HasIndex(x => x.CompanyId);
		builder.HasOne(x => x.CorporateGroup).WithMany(x => x.Members)
			.HasForeignKey(x => x.CorporateGroupId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
		builder.HasOne(x => x.Company).WithMany(x => x.GroupMemberships)
			.HasForeignKey(x => x.CompanyId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
	}
}
