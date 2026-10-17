namespace Constants;

public static class MinLength : object
{
	static MinLength()
	{
	}

	public const int CellPhoneNumberVerificationKey = 6;

	/// <summary>
	/// Minimum length of a user credential password, set when a password is
	/// created or changed (not at login). Aligned with the security guidance for
	/// password-only authentication.
	/// </summary>
	public const int UserPassword = 8;

	public const int Username = 3;
}
