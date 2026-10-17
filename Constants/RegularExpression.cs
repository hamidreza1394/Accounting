namespace Constants;

/// <summary>
/// https://regex101.com/
/// </summary>
public static class RegularExpression : object
{
	static RegularExpression()
	{
	}

	public const string NationalCode = @"^\d{10}$";
	public const string CellPhoneNumber = @"^09\d{9}";
	// Product policy (explicit decision, applied consistently at every layer:
	// view models, user creation and the Users table): a username is a Latin
	// identifier of 3 to 20 characters from the allowed set of
	// IdentityOptions.User.AllowedUserNameCharacters (letters, digits and the
	// characters '-', '.', '_', '@', '+'). The Users table stores the username in
	// a varchar(20) column (IsUnicode(false)), so the policy must stay inside
	// this ASCII set. Login itself does not apply this pattern - the stored
	// username is only looked up - so a legacy account is never locked out by a
	// change of the policy.
	public const string Username = @"^[a-zA-Z0-9._@+-]{3,20}$";

	// The password policy is enforced where a password is set or changed
	// (required, Constants.MinLength.UserPassword, Constants.MaxLength.UserPassword
	// and the common-password check). A password is not limited to a character
	// pattern: any Unicode character is accepted and only the hash is stored.

	public const string AToZDigitsUnderline = @"^[a-zA-Z][a-zA-Z0-9_]*$";
	public const string EmailAddress = @"^[a-zA-Z0-9_.-]+@[a-zA-Z0-9_.-]+$";

	public const string IP =
		@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";

	public const string Url =
		@"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)";
}
