namespace Constants;

public static class MaxLength : object
{
	static MaxLength()
	{
	}

	public const int IP = 15;

	public const int Name = 50;
	public const int Title = 50;

	public const int Link = 500;
	public const int Path = 200;

	public const int Icon = 100;

	public const int Username = 20;
	public const int Password = 20;

	/// <summary>
	/// Maximum length of a user credential password that is entered by a human.
	/// Aligned with the default maximum password length of ASP.NET Core Identity.
	/// Note: <see cref="Password"/> is the length of the legacy CMS page password
	/// column and is not the user credential policy.
	/// </summary>
	public const int UserPassword = 128;

	public const int LastName = 50;
	public const int FirstName = 50;

	public const int EmailAddress = 254;

	public const int CellPhoneNumberVerificationKey = 10;

}
