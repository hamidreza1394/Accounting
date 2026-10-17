namespace Data.Configurations;

internal sealed class CompanyConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.Company>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.Company> builder)
    {
        builder.ToTable("Companies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NationalId).HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.NationalId).IsUnique().HasFilter("[NationalId] IS NOT NULL");
        builder.HasIndex(x => x.IsActive);
    }
}