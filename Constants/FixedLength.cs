namespace Constants;

public static class FixedLength : object
{
	static FixedLength()
	{
	}

	public const int NationalCode = 10;
	public const int CellPhoneNumber = 11;

	/// <summary>
	/// Length of the legacy Base64 SHA-256 digest. It is kept as the minimum length of a
	/// stored credential so that accounts created by older releases stay valid.
	/// </summary>
	public const int DatabasePassword = 44;

	/// <summary>
	/// Maximum length of a stored credential. The current format (PBKDF2 produced by
	/// <c>PasswordHasher&lt;TUser&gt;</c>) is 84 characters; the column is wider than that
	/// so a future format change does not require another schema change.
	/// </summary>
	public const int PasswordHash = 255;
}
