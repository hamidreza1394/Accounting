using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Configurations;

internal sealed class CompanySetupRecordConfiguration
	: IEntityTypeConfiguration<Domain.CompanySetupRecord>
{
	public void Configure(EntityTypeBuilder<Domain.CompanySetupRecord> builder)
	{
		// The physical CompanyReference column uses the binary collation Latin1_General_BIN2
		// (vendor review L-2). This is intentionally NOT declared with UseCollation here: the
		// migration guards (scripts/generate_migrations.py) do not model that fluent call, so the
		// collation is applied by the reviewed migration epilogue of
		// 20261116070805_AddCompanySetupRecords *before* the unique reference index is built, and
		// it is pinned in the guard's HAND_WRITTEN_SQL list - the same pattern as the reviewed
		// CHECK constraints. The runtime model therefore has no collation annotation; the
		// SQL Server-gated test asserts the real database collation.
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
