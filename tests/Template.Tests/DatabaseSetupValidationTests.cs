using System.Linq;
using System.Threading.Tasks;
using Infrastructure.DatabaseSetup;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Template.Tests;

public sealed class DatabaseSetupValidationTests
{
	private static DatabaseConnectionSetupService CreateService(TempFolder folder) =>
		new(
			new DatabaseConnectionSettingsStore(folder.Combine("settings")),
			NullLogger<DatabaseConnectionSetupService>.Instance);

	private static DatabaseSetupRequest Valid(string mode = "Windows") => new()
	{
		Server = "localhost",
		Database = "Farano",
		AuthenticationMode = mode,
		UserName = mode == "Sql" ? "sa" : null,
		Password = mode == "Sql" ? "x" : null,
	};

	[Fact]
	public void DefaultsAreFaranoDatabaseAndNoServer()
	{
		var request = new DatabaseSetupRequest();

		Assert.Equal("Farano", request.Database);
		Assert.True(string.IsNullOrEmpty(request.Server));
		Assert.Contains("Server", DatabaseConnectionSetupService.Validate(request).Keys);
	}

	[Fact]
	public async Task ConfigureAsync_WhenServerIsMissing_ReturnsFieldError()
	{
		using var folder = new TempFolder();

		var result = await CreateService(folder).ConfigureAsync(
			new DatabaseSetupRequest { Server = string.Empty, Database = "Farano_Test" },
			TestContext.Current.CancellationToken);

		Assert.False(result.Succeeded);
		Assert.Equal(DatabaseSetupErrorCode.ValidationFailed, result.ErrorCode);
		Assert.Equal(DatabaseSetupStage.Validating, result.FailedStage);
		Assert.Contains("نام یا نشانی سرور الزامی است", result.ValidationErrors["Server"].Single());
		Assert.Equal(12, result.TrackingId!.Length);
	}

	[Fact]
	public async Task ConfigureAsync_WhenAuthenticationModeIsInvalid_ReturnsFieldError()
	{
		using var folder = new TempFolder();

		var result = await CreateService(folder).ConfigureAsync(
			new DatabaseSetupRequest { Server = "localhost", Database = "Farano_Test", AuthenticationMode = "Unknown" },
			TestContext.Current.CancellationToken);

		Assert.False(result.Succeeded);
		Assert.Contains("Windows", result.ValidationErrors["AuthenticationMode"].Single());
	}

	[Fact]
	public async Task ConfigureAsync_WhenSqlAuthenticationHasNoPassword_ReturnsFieldError()
	{
		using var folder = new TempFolder();

		var result = await CreateService(folder).ConfigureAsync(
			new DatabaseSetupRequest
			{
				Server = "localhost",
				Database = "Farano_Test",
				AuthenticationMode = "Sql",
				UserName = "test-user",
				Password = string.Empty,
			},
			TestContext.Current.CancellationToken);

		Assert.False(result.Succeeded);
		Assert.Contains("رمز عبور لازم است", result.ValidationErrors["Password"].Single());
		Assert.False(result.ValidationErrors.ContainsKey("UserName"));
	}

	[Fact]
	public async Task ConfigureAsync_WhenSystemDatabaseIsSelected_ReturnsFieldError()
	{
		using var folder = new TempFolder();

		var result = await CreateService(folder).ConfigureAsync(
			new DatabaseSetupRequest { Server = "localhost", Database = "master", AuthenticationMode = "Windows" },
			TestContext.Current.CancellationToken);

		Assert.False(result.Succeeded);
		Assert.Contains("دیتابیس‌های سیستمی", result.ValidationErrors["Database"].Single());
	}

	[Fact]
	public void Validate_WindowsAuthenticationWithoutUserOrPassword_IsValid()
	{
		Assert.Empty(DatabaseConnectionSetupService.Validate(Valid("Windows")));
		Assert.Empty(DatabaseConnectionSetupService.Validate(Valid("windows")));
	}

	[Fact]
	public void Validate_ReportsEveryIndependentErrorAtOnce()
	{
		var errors = DatabaseConnectionSetupService.Validate(new DatabaseSetupRequest
		{
			Server = " ",
			Database = "tempdb",
			AuthenticationMode = "Sql",
			UserName = null,
			Password = null,
		});

		Assert.Equal(["Database", "Password", "Server", "UserName"], errors.Keys.Order().ToArray());
		Assert.All(errors.Values.SelectMany(v => v), message => Assert.False(string.IsNullOrWhiteSpace(message)));

		var allWrong = DatabaseConnectionSetupService.Validate(new DatabaseSetupRequest
		{
			Server = new string('s', 257),
			Database = new string('d', 129),
			AuthenticationMode = "Kerberos",
		});

		Assert.Equal(["AuthenticationMode", "Database", "Server"], allWrong.Keys.Order().ToArray());
	}

	[Theory]
	[InlineData("master")]
	[InlineData("MODEL")]
	[InlineData("MsDb")]
	[InlineData(" tempdb ")]
	public void Validate_RejectsSystemDatabasesCaseInsensitively(string name)
	{
		var request = new DatabaseSetupRequest { Server = "s", Database = name };

		Assert.Contains("Database", DatabaseConnectionSetupService.Validate(request).Keys);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("a\tb")]
	[InlineData("a\nb")]
	public void Validate_RejectsEmptyWhitespaceOnlyAndControlCharactersInDatabase(string name)
	{
		var request = new DatabaseSetupRequest { Server = "s", Database = name };

		Assert.Contains("Database", DatabaseConnectionSetupService.Validate(request).Keys);
	}

	[Theory]
	[InlineData("Farano")]
	[InlineData("Farano_2026")]
	[InlineData("farano-db")]
	[InlineData("حسابداری فرانو")]
	[InlineData("my db")]
	[InlineData("123")]
	public void Validate_AcceptsValidSqlIdentifiers(string name)
	{
		var request = new DatabaseSetupRequest { Server = "s", Database = name };

		Assert.Empty(DatabaseConnectionSetupService.Validate(request));
	}

	[Fact]
	public void Validate_LengthLimits()
	{
		Assert.Empty(DatabaseConnectionSetupService.Validate(new DatabaseSetupRequest { Server = new string('s', 256), Database = new string('d', 123) }));
		Assert.Contains("Server", DatabaseConnectionSetupService.Validate(new DatabaseSetupRequest { Server = new string('s', 257), Database = "d" }).Keys);
		Assert.Contains("Database", DatabaseConnectionSetupService.Validate(new DatabaseSetupRequest { Server = "s", Database = new string('d', 124) }).Keys);
		Assert.Contains("Database", DatabaseConnectionSetupService.Validate(new DatabaseSetupRequest { Server = "s", Database = new string('d', 129) }).Keys);
		var tooLong = DatabaseConnectionSetupService.Validate(new DatabaseSetupRequest { Server = "s", Database = new string('d', 124) })["Database"].Single();
		Assert.Contains("۱۲۳", tooLong);
		Assert.Contains("Server", DatabaseConnectionSetupService.Validate(new DatabaseSetupRequest { Server = "bad\u0007server", Database = "d" }).Keys);
	}

	[Fact]
	public void RequestToString_DoesNotLeakThePassword()
	{
		var request = new DatabaseSetupRequest { Server = "s", UserName = "sa", Password = "Sup3rSecret!" };

		Assert.DoesNotContain("Sup3rSecret!", request.ToString());
	}

	[Theory]
	[InlineData("\uFF4D\uFF41\uFF53\uFF54\uFF45\uFF52")] // fullwidth "master"
	[InlineData("\uFF2D\uFF2F\uFF24\uFF25\uFF2C")] // fullwidth "MODEL"
	[InlineData("\uFEFFmaster")] // byte order mark
	[InlineData("ma\u200Bster")] // zero width space
	[InlineData("m\u200Cs\u200Ddb")] // ZWNJ / ZWJ
	[InlineData("temp\u2060db")] // word joiner
	[InlineData("a\u00A0b")] // no-break space
	[InlineData("a\u202Eb")] // right-to-left override
	[InlineData("tempdb\u2028")] // line separator
	public void Validate_RejectsUnicodeLookAlikesOfSystemDatabases(string name)
	{
		var errors = DatabaseConnectionSetupService.Validate(new DatabaseSetupRequest { Server = "s", Database = name });

		Assert.Contains("Database", errors.Keys);
	}

	[Fact]
	public void Validate_FullwidthSystemName_IsCaughtByTheNormalizedBlocklist_NotOnlyByTheCharacterRule()
	{
		// Fullwidth letters are ordinary letters; only NFKC normalization reveals "master".
		var errors = DatabaseConnectionSetupService.Validate(
			new DatabaseSetupRequest { Server = "s", Database = "\uFF4D\uFF41\uFF53\uFF54\uFF45\uFF52" });

		Assert.Contains("سیستمی", errors["Database"].Single());
	}

	[Theory]
	[InlineData("فرانو")]
	[InlineData("حسابداری ۱۴۰۵")]
	[InlineData("Farano-2026_db.v2")]
	[InlineData("café")]
	[InlineData("数据库")]
	public void Validate_StillAcceptsLegitimateUnicodeNames(string name)
	{
		Assert.Empty(DatabaseConnectionSetupService.Validate(new DatabaseSetupRequest { Server = "s", Database = name }));
	}

	[Theory]
	[InlineData("a]b")]
	[InlineData("a[b")]
	[InlineData("a\\b")]
	[InlineData("a/b")]
	[InlineData("a:b")]
	[InlineData("a*b")]
	[InlineData("a?b")]
	[InlineData("a\"b")]
	[InlineData("a<b")]
	[InlineData("a>b")]
	[InlineData("a|b")]
	[InlineData("a;b")]
	[InlineData("a'b")]
	[InlineData("..\\evil")]
	[InlineData("a..b")]
	[InlineData(".hidden")]
	[InlineData("trailing.")]
	[InlineData("name\u0000")]
	public void Validate_RejectsPathAndQuoteCharacters_WithAClearFieldError(string name)
	{
		var errors = DatabaseConnectionSetupService.Validate(new DatabaseSetupRequest { Server = "s", Database = name });

		var message = Assert.Single(errors["Database"]);
		Assert.Contains("حروف", message);
		Assert.Contains("مجاز نیست", message);
	}

	[Fact]
	public void Validate_LoneSurrogate_GivesTheInvalidCharactersMessage_NotTheSystemDatabaseOne()
	{
		var errors = DatabaseConnectionSetupService.Validate(new DatabaseSetupRequest { Server = "s", Database = "ab\uD800cd" });

		var message = Assert.Single(errors["Database"]);
		Assert.DoesNotContain("سیستمی", message);
		Assert.Contains("حروف", message);
	}

	[Fact]
	public void Validate_NameFitsWithItsLogFileSuffix()
	{
		Assert.True(DatabaseConnectionSetupService.MaximumDatabaseNameLength + "_log".Length <= 128);
	}
}
