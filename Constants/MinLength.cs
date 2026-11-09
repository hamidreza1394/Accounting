namespace Constants;

public static class MinLength : object
{
	static MinLength()
	{
	}

	public const int CellPhoneNumberVerificationKey = 6;

	/// <summary>
	/// Minimum length of a user credential password, set when a password is
	/// created or changed (not at login). D-078 sets the minimum to 8 characters
	/// (superseding the number 15 of D-073 §5; the other D-073 password controls
	/// stay); existing accounts are never force-rotated. The server and the React
	/// client (passwordPolicy.ts) must use the same value.
	/// </summary>
	public const int UserPassword = 8;

	public const int Username = 3;
}
