namespace Data.Configurations;

internal class UserConfiguration : object,
	Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Domain.User>
{
	public UserConfiguration() : base()
	{
	}

	public void Configure
		(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Domain.User> builder)
	{
		// **************************************************
		// **************************************************
		// **************************************************
		builder
			.Property(current => current.EmailAddress)
			.IsUnicode(unicode: false)
			;

		builder
			.HasIndex(current => new { current.EmailAddress })
			.IsUnique(unique: true)
			;
		// **************************************************

		// **************************************************
		builder
			.HasIndex(current => new { current.EmailAddressVerificationKey })
			.IsUnique(unique: true)
			;
		// **************************************************

		// **************************************************
		builder
			.Property(current => current.Username)
			.IsUnicode(unicode: false)
			;

		builder
			.HasIndex(current => new { current.Username })
			.IsUnique(unique: true)
			;

		builder
			.HasIndex(current => current.DefaultCompanyId)
			;

		builder
			.HasOne<Domain.Company>()
			.WithMany()
			.HasForeignKey(current => current.DefaultCompanyId)
			.OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.SetNull)
			;

		//builder.HasIndex(current => current.Username)
		//	.IsUnique(unique: true)
		//	// HasFilter -> using Microsoft.EntityFrameworkCore;
		//	.HasFilter("[Username] IS NOT NULL");
		// **************************************************

		// **************************************************
		builder
			.Property(current => current.CellPhoneNumber)
			.IsUnicode(unicode: false)
			;

		builder
			.HasIndex(current => new { current.CellPhoneNumber })
			.IsUnique(unique: true)
			;

		//builder.HasIndex(current => current.CellPhoneNumber)
		//	.IsUnique(unique: true)
		//	// HasFilter -> using Microsoft.EntityFrameworkCore;
		//	.HasFilter("[CellPhoneNumber] IS NOT NULL");
		// **************************************************

		// **************************************************
		builder
			.Property(current => current.CellPhoneNumberVerificationKey)
			.IsUnicode(unicode: false)
			;

		builder
			.HasIndex(current => new { current.CellPhoneNumberVerificationKey })
			.IsUnique(unique: true)
			;

		//builder.HasIndex(current => current.CellPhoneNumberVerificationKey)
		//	.IsUnique(unique: true)
		//	// HasFilter -> using Microsoft.EntityFrameworkCore;
		//	.HasFilter("[CellPhoneNumberVerificationKey] IS NOT NULL");
		// **************************************************

		// **************************************************
		builder
			.Property(current => current.Password)
			.IsUnicode(unicode: false)
			;
		// **************************************************
		// **************************************************
		// **************************************************

		// **************************************************
		// **************************************************
		// **************************************************
		builder
			.HasMany(current => current.CreatedPages)
			.WithOne(other => other.CreatorUser)
			.IsRequired(required: true)
			.HasForeignKey(other => other.CreatorUserId)
			.OnDelete(deleteBehavior:
				Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade)
			;
		// **************************************************

		// **************************************************
		builder
			.HasMany(current => current.LoginLogs)
			.WithOne(other => other.User)
			.IsRequired(required: true)
			.HasForeignKey(other => other.UserId)
			.OnDelete(deleteBehavior:
				Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade)
			;
		// **************************************************
		// **************************************************
		// **************************************************

		// No hard-coded administrator or demo credentials are seeded.

	}
}
