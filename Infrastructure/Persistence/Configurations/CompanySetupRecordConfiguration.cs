using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Configurations;

internal sealed class CompanySetupRecordConfiguration
	: IEntityTypeConfiguration<Domain.CompanySetupRecord>
{
	public void Configure(EntityTypeBuilder<Domain.CompanySetupRecord> builder)
	{
		builder.ToTable("CompanySetupRecords");
		builder.HasKey(record => record.Id);

		builder.Property(record => record.CompanyId).IsRequired();
		builder.Property(record => record.CompanyReference)
			.HasMaxLength(450)
			.IsRequired();
		builder.Property(record => record.InstallationId)
			.HasMaxLength(128)
			.IsUnicode(false)
			.IsRequired();
		builder.Property(record => record.CreatedAtUtc).IsRequired();
		builder.Property(record => record.RowVersion).IsRowVersion();

		builder.HasIndex(record => record.CompanyId).IsUnique();
		builder.HasIndex(record => record.CompanyReference).IsUnique();

		builder.HasOne<Domain.Company>()
			.WithMany()
			.HasForeignKey(record => record.CompanyId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}
