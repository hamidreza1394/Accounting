using System.Collections.Generic;

namespace ViewModels.Validation
{
	/// <summary>
	/// Rejects passwords that appear in the well-known lists of common and
	/// breached passwords. The check runs where a password is created or changed;
	/// at login the entered password is only verified against the stored hash, so
	/// an existing password is never rejected by a policy change.
	/// </summary>
	[System.AttributeUsage
		(System.AttributeTargets.Property | System.AttributeTargets.Field,
		AllowMultiple = false, Inherited = true)]
	public sealed class NotCommonPasswordAttribute :
		System.ComponentModel.DataAnnotations.ValidationAttribute
	{
		/// <summary>
		/// A compact deny list of the most common passwords (length 8 or more,
		/// shorter ones are already rejected by the minimum length rule).
		/// </summary>
		private static readonly System.Collections.Generic.HashSet<string>
			CommonPasswords = new(System.StringComparer.Ordinal)
			{
				"12345678", "123456789", "1234567890", "123123123", "12345678910",
				"password", "password1", "password123", "passw0rd", "p@ssw0rd",
				"p@ssword", "passwort", "qwertyui", "qwerty123", "qwerty1234",
				"iloveyou", "iloveyou1", "princess", "princess1", "sunshine",
				"football", "football1", "baseball", "basketball", "superman",
				"trustno1", "starwars", "welcome1", "welcome12", "welcome123",
				"admin123", "administrator", "letmein1", "letmein123", "monkey12",
				"dragon123", "master123", "shadow123", "michael1", "jennifer1",
				"jordan23", "harleyquinn", "charlie1", "thomas123", "robert123",
				"11111111", "00000000", "aaaaaaaa", "abc12345", "abcd1234",
				"qazwsxedc", "asdfghjkl", "zxcvbnm1", "1qaz2wsx", "1q2w3e4r",
				"987654321", "11223344", "55555555", "786786786", "123qweasd",
				"amir123456", "mohammad", "mohammad1", "ali123456", "hossein12",
				"parsian12", "tehran123", "esfahan12", "shiraz123", "passwordpassword",
			};

		public NotCommonPasswordAttribute() : base()
		{
		}

		public override bool IsValid(object? value)
		{
			if (value is null)
			{
				return true;
			}

			if (value is not string text)
			{
				return false;
			}

			if (text.Length == 0)
			{
				return true;
			}

			return !CommonPasswords.Contains(item: text.ToLowerInvariant());
		}
	}
}
