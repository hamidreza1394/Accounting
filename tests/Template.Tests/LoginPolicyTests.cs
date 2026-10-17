using System.Linq;
using Xunit;

namespace Template.Tests;

/// <summary>
/// The login form must never reject a stored credential because of a policy
/// change: at login the username and the password are only required and bounded,
/// while the structural rules are enforced where a credential is created.
/// </summary>
public sealed class LoginPolicyTests
{
	private static System.Collections.Generic.List<System.ComponentModel.DataAnnotations.ValidationResult>
		Validate(ViewModels.Pages.Account.LoginViewModel viewModel)
	{
		var results = new System.Collections.Generic.List<System.ComponentModel.DataAnnotations.ValidationResult>();

		System.ComponentModel.DataAnnotations.Validator.TryValidateObject
			(instance: viewModel,
			validationContext: new System.ComponentModel.DataAnnotations.ValidationContext(instance: viewModel),
			validationResults: results,
			validateAllProperties: true);

		return results;
	}

	[Fact]
	public void EmptyCredentials_ProduceRequiredErrors()
	{
		var results = Validate(new ViewModels.Pages.Account.LoginViewModel
		{
			Username = string.Empty,
			Password = string.Empty,
		});

		Assert.Equal(2, results.Count);
	}

	[Fact]
	public void MissingCredentials_ProduceRequiredErrors()
	{
		var results = Validate(new ViewModels.Pages.Account.LoginViewModel());

		Assert.Equal(2, results.Count);
	}

	[Fact]
	public void Login_AcceptsAnyPasswordCharacters_NoStructuralPolicyAtLogin()
	{
		// An old account may hold any password; a policy change must not lock it out.
		var results = Validate(new ViewModels.Pages.Account.LoginViewModel
		{
			Username = "dariush",
			Password = "پ@ss w0rd-#$%^&*()",
		});

		Assert.Empty(results);
	}

	[Fact]
	public void Login_AcceptsALegacyUsername_WithoutPatternRestriction()
	{
		var results = Validate(new ViewModels.Pages.Account.LoginViewModel
		{
			Username = "کاربر-قدیمی",
			Password = "12345678",
		});

		Assert.Empty(results);
	}

	[Fact]
	public void Login_AcceptsACommonPassword_ThePolicyRunsAtSetTimeOnly()
	{
		var results = Validate(new ViewModels.Pages.Account.LoginViewModel
		{
			Username = "someone",
			Password = "12345678",
		});

		Assert.Empty(results);
	}

	[Fact]
	public void Username_LongerThanTheDatabaseColumn_IsRejected()
	{
		var results = Validate(new ViewModels.Pages.Account.LoginViewModel
		{
			Username = new string('a', Constants.MaxLength.Username + 1),
			Password = "whatever",
		});

		var error = Assert.Single(results);
		Assert.Contains("Username", Assert.Single(error.MemberNames));
	}

	[Fact]
	public void Password_LongerThanTheInputBound_IsRejected()
	{
		var results = Validate(new ViewModels.Pages.Account.LoginViewModel
		{
			Username = "someone",
			Password = new string('a', Constants.MaxLength.UserPassword + 1),
		});

		var error = Assert.Single(results);
		Assert.Contains("Password", Assert.Single(error.MemberNames));
	}

	[Fact]
	public void LoginViewModel_DoesNotCarryAStructuralPasswordPattern()
	{
		var attributes = typeof(ViewModels.Pages.Account.LoginViewModel)
			.GetProperty(name: "Password")!
			.GetCustomAttributes(inherit: true);

		Assert.DoesNotContain
			(attributes, attribute => attribute is System.ComponentModel.DataAnnotations.RegularExpressionAttribute);

		Assert.DoesNotContain
			(attributes, attribute => attribute is ViewModels.Validation.NotCommonPasswordAttribute);
	}
}

/// <summary>
/// The username policy of the creation flows: a Latin identifier inside the
/// allowed set of IdentityOptions.User.AllowedUserNameCharacters, 3 to 20
/// characters, applied consistently by registration and by the admin user forms.
/// </summary>
public sealed class UsernamePolicyTests
{
	private static bool IsMatch(string username)
	{
		return System.Text.RegularExpressions.Regex.IsMatch
			(input: username, pattern: Constants.RegularExpression.Username);
	}

	[Theory]
	[InlineData("ali")]
	[InlineData("user_1")]
	[InlineData("first.last")]
	[InlineData("user@site")]
	[InlineData("a+b-c")]
	[InlineData("USER123")]
	[InlineData("007")]
	public void Accepts_IdentifiersOfTheAllowedSet(string username)
	{
		Assert.True(IsMatch(username));
	}

	[Theory]
	[InlineData("")]
	[InlineData("ab")]
	[InlineData("ali reza")]
	[InlineData("ali;drop")]
	[InlineData("a_b_c_d_e_f_g_h_i_j_k")] // 21 characters
	public void Rejects_TooShortTooLongOrUnknownCharacters(string username)
	{
		Assert.False(IsMatch(username));
	}

	[Fact]
	public void Rejects_NonAscii_ExplicitProductPolicyOfTheVarcharColumn()
	{
		// The Users table stores the username in a varchar(20) column, so the
		// documented product policy keeps usernames inside the ASCII identifier set.
		Assert.False(IsMatch(username: "کاربر"));
	}

	[Fact]
	public void CreationFlows_UseTheSameUsernamePolicy()
	{
		var patternOfRegistration =
			GetUsernamePattern(type: typeof(ViewModels.Pages.Account.RegisterViewModel));

		var patternOfAdminForms =
			GetUsernamePattern(type: typeof(ViewModels.Pages.Admin.Users.CommonViewModel));

		Assert.Equal(Constants.RegularExpression.Username, patternOfRegistration);
		Assert.Equal(Constants.RegularExpression.Username, patternOfAdminForms);
	}

	private static string? GetUsernamePattern(System.Type type)
	{
		var attribute =
			type.GetProperty(name: "Username")!
			.GetCustomAttributes(inherit: true)
			.OfType<System.ComponentModel.DataAnnotations.RegularExpressionAttribute>()
			.SingleOrDefault();

		return attribute?.Pattern;
	}
}

/// <summary>
/// The password policy of the creation flows: a minimum length and a
/// common-password check, without any character pattern.
/// </summary>
public sealed class PasswordPolicyTests
{
	private static System.Collections.Generic.List<System.ComponentModel.DataAnnotations.ValidationResult>
		ValidateProperty(object instance, string propertyName, object? value)
	{
		var results = new System.Collections.Generic.List<System.ComponentModel.DataAnnotations.ValidationResult>();

		var context =
			new System.ComponentModel.DataAnnotations.ValidationContext(instance: instance)
			{
				MemberName = propertyName,
			};

		System.ComponentModel.DataAnnotations.Validator.TryValidateProperty
			(value: value, validationContext: context, validationResults: results);

		return results;
	}

	[Theory]
	[InlineData("12345678")]
	[InlineData("password")]
	[InlineData("PASSWORD1")]
	[InlineData("qwerty123")]
	[InlineData("admin123")]
	public void Rejects_CommonPasswords(string password)
	{
		var results = ValidateProperty
			(instance: new ViewModels.Pages.Admin.Users.CreateViewModel(),
			propertyName: nameof(ViewModels.Pages.Admin.Users.CreateViewModel.Password),
			value: password);

		// The common-password check is the only rule that can fail here
		// (the value is 8+ characters and shorter than the maximum).
		var error = Assert.Single(results);
		Assert.NotNull(error.ErrorMessage);
	}

	[Fact]
	public void Rejects_TooShortPasswords()
	{
		var results = ValidateProperty
			(instance: new ViewModels.Pages.Admin.Users.CreateViewModel(),
			propertyName: nameof(ViewModels.Pages.Admin.Users.CreateViewModel.Password),
			value: "Ab1!xyz");

		Assert.Single(results);
	}

	[Fact]
	public void Accepts_AStrongPassword_WithAnyCharacters()
	{
		var results = ValidateProperty
			(instance: new ViewModels.Pages.Admin.Users.CreateViewModel(),
			propertyName: nameof(ViewModels.Pages.Admin.Users.CreateViewModel.Password),
			value: "C0rrect-Phrase-۱۴۰۵!");

		Assert.Empty(results);
	}

	[Fact]
	public void CreationFlows_DoNotUseACharacterPatternForPasswords()
	{
		foreach (var type in new[]
		{
			typeof(ViewModels.Pages.Account.RegisterViewModel),
			typeof(ViewModels.Pages.Admin.Users.CreateViewModel),
		})
		{
			var attributes =
				type.GetProperty(name: "Password")!
				.GetCustomAttributes(inherit: true);

			Assert.DoesNotContain
				(attributes, attribute => attribute is System.ComponentModel.DataAnnotations.RegularExpressionAttribute);

			Assert.Contains
				(attributes, attribute => attribute is ViewModels.Validation.NotCommonPasswordAttribute);
		}
	}
}

/// <summary>
/// The pure helpers of the authentication service: fixed-time digest comparison
/// and the normalisation of the client address for the audit trail.
/// </summary>
public sealed class AuthenticationHelperTests
{
	[Fact]
	public void IsFixedTimeEqual_ComparesDigests()
	{
		var digest = Dtat.Security.Hashing.GetSha256(text: "abc");

		Assert.True(Infrastructure.Security.AuthenticationService
			.IsFixedTimeEqual(left: digest, right: digest));

		Assert.False(Infrastructure.Security.AuthenticationService
			.IsFixedTimeEqual(left: digest, right: Dtat.Security.Hashing.GetSha256(text: "abd")));

		Assert.False(Infrastructure.Security.AuthenticationService
			.IsFixedTimeEqual(left: digest, right: null));

		Assert.False(Infrastructure.Security.AuthenticationService
			.IsFixedTimeEqual(left: null, right: null));
	}

	[Fact]
	public void NormalizeLoginLogIP_KeepsIPv4AndReducesMappedIPv6()
	{
		Assert.Equal("192.168.1.10", Infrastructure.Security.AuthenticationService
			.NormalizeLoginLogIP(userIP: "192.168.1.10"));

		Assert.Equal("127.0.0.1", Infrastructure.Security.AuthenticationService
			.NormalizeLoginLogIP(userIP: "::ffff:127.0.0.1"));

		// A real IPv6 address does not fit the varchar(15) UserIP column.
		Assert.Equal("0.0.0.0", Infrastructure.Security.AuthenticationService
			.NormalizeLoginLogIP(userIP: "2001:db8::1"));

		Assert.Equal("0.0.0.0", Infrastructure.Security.AuthenticationService
			.NormalizeLoginLogIP(userIP: null));
	}

	[Fact]
	public void GetAuditIP_TruncatesLongAddresses()
	{
		var longValue = new string('a', 80);

		Assert.Equal(50, Infrastructure.Security.AuthenticationService
			.GetAuditIP(userIP: longValue).Length);

		Assert.Equal("10.0.0.1", Infrastructure.Security.AuthenticationService
			.GetAuditIP(userIP: "10.0.0.1"));
	}

	[Fact]
	public void GetLockoutKey_IsCaseInsensitive()
	{
		Assert.Equal("admin", Infrastructure.Security.AuthenticationService
			.GetLockoutKey(username: "  Admin "));
	}
}
