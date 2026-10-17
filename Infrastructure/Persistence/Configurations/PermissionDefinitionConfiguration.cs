namespace Data.Configurations;

internal sealed class PermissionDefinitionConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.PermissionDefinition>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.PermissionDefinition> builder)
    {
        builder.ToTable("PermissionDefinitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(120).IsUnicode(false).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.HasIndex(x => x.Code).IsUnique();
    }
}