using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Infrastructure.DatabaseSetup;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

/// <summary>
/// The whole first-run flow over HTTP against a REAL SQL Server. Runs only when
/// <c>FARANOU_TEST_SQLSERVER</c> is set (same variable and skipping rule as
/// <see cref="DatabaseSetupSqlServerTests" />); otherwise each test is reported as skipped. The
/// database name is unique (<c>FaranoTests_</c> + guid) and only that database is dropped afterwards.
/// </summary>
public sealed class DatabaseSetupPageSqlServerTests
{
	private static bool SqlServerAvailable()
	{
		if (Integration.IntegrationDatabase.UsesSqlServer)
		{
			return true;
		}

		if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
		{
			throw new InvalidOperationException("FARANOU_REQUIRE_SQLSERVER=1 but FARANOU_TEST_SQLSERVER is not set.");
		}

		return false;
	}

	private static SqlConnectionStringBuilder Admin => new(Integration.IntegrationDatabase.SqlServerConnectionString);

	private static async Task DropOwnDatabaseAsync(string name)
	{
		Assert.StartsWith("FaranoTests_", name, StringComparison.Ordinal);
		var builder = Admin;
		builder.InitialCatalog = name;
		await using var context = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
			.UseSqlServer(builder.ConnectionString)
			.Options);
		await context.Database.EnsureDeletedAsync();
	}

	[Fact]
	public async Task FullSuccessfulPost_CreatesTheDatabase_SavesProtectedSettings_AndOffersTheMasterStep()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		var name = "FaranoTests_" + Guid.NewGuid().ToString("N");
		var admin = Admin;
		using var page = SetupPage.Start();

		try
		{
			var form = SetupPage.Form(
				page.ReadSetupToken(),
				server: admin.DataSource,
				database: name,
				mode: "Sql",
				userName: admin.UserID,
				password: admin.Password,
				trustServerCertificate: true);

			var first = await page.PostAsync(form);

			Assert.Equal(HttpStatusCode.OK, first.Status);
			Assert.DoesNotContain("اطلاعات فرم را بررسی کنید", first.Text);
			Assert.Contains("دیتابیس با Migrationهای پروژه آماده شد", first.Text);
			Assert.Contains("class=\"alert success\"", first.Html);
			Assert.Contains("اجرای Migrationها: انجام شد", first.Text);
			Assert.Contains("ذخیره تنظیمات: انجام شد", first.Text);
			Assert.DoesNotContain("data-state=\"failed\"", first.Html);

			// The next step (Master bootstrap) is offered, and the connection form is gone.
			Assert.Contains("تنظیم مدیر زیرساختی (Master)", first.Text);
			Assert.NotNull(first.Tag("MasterInput_Username"));
			Assert.Null(first.Tag("Input_Server"));

			// Nothing secret in the response, in the settings file or in any log.
			Assert.DoesNotContain(admin.Password, first.Html);
			Assert.DoesNotContain(admin.Password, first.Text);
			Assert.DoesNotContain(admin.Password, page.Factory.Logs.All);

			Assert.True(File.Exists(page.SettingsFile));
			var saved = await File.ReadAllTextAsync(page.SettingsFile, TestContext.Current.CancellationToken);
			Assert.DoesNotContain(admin.Password, saved);
			Assert.DoesNotContain(admin.DataSource, saved);
			// (The user name is not asserted: "sa" is two letters and occurs by chance in any ciphertext.)
			Assert.DoesNotContain("Password=", saved, StringComparison.OrdinalIgnoreCase);
			Assert.DoesNotContain("Data Source", saved, StringComparison.OrdinalIgnoreCase);
			Assert.DoesNotContain(name, saved);
			Assert.False(File.Exists(page.TokenFile), "The one-time token must be consumed.");

			// The saved settings really open the database that was created and migrated.
			var loaded = new DatabaseConnectionSettingsStore(page.SettingsDirectory).Load();
			Assert.Equal(DatabaseSettingsStatus.Loaded, loaded.Status);
			Assert.Equal(name, new SqlConnectionStringBuilder(loaded.ConnectionString).InitialCatalog);
			await using (var context = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
				.UseSqlServer(loaded.ConnectionString, sql => sql.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName))
				.Options))
			{
				Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
				Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));
			}

			// A GET after success shows the saved state, not the form.
			var again = await page.GetAsync();
			Assert.Equal(HttpStatusCode.OK, again.Status);
			Assert.Contains("تنظیمات ذخیره شده است", again.Text);
			Assert.Null(again.Tag("Input_Server"));

			// A second POST (token from the Master form) must not overwrite anything.
			var bytesBefore = await File.ReadAllBytesAsync(page.SettingsFile, TestContext.Current.CancellationToken);
			var snapshotBefore = LegacyFixture.Snapshot(page.SettingsDirectory);
			var second = await page.PostAsync(
				SetupPage.Form(
					"any-token",
					server: "127.0.0.1,1",
					database: "FaranoTests_" + Guid.NewGuid().ToString("N"),
					mode: "Windows"),
				first.AntiforgeryToken);

			Assert.Equal(HttpStatusCode.OK, second.Status);
			Assert.Contains("تنظیمات قبلاً ذخیره شده است", second.Text);
			Assert.DoesNotContain("<ol class=\"steps\"", second.Html);
			Assert.Equal(bytesBefore, await File.ReadAllBytesAsync(page.SettingsFile, TestContext.Current.CancellationToken));
			Assert.Equal(snapshotBefore, LegacyFixture.Snapshot(page.SettingsDirectory));
			Assert.DoesNotContain(admin.Password, page.Factory.Logs.All);
		}
		finally
		{
			await DropOwnDatabaseAsync(name);
		}
	}

	[Fact]
	public async Task WrongSqlPassword_OverHttp_ShowsLoginFailed_WithoutLeakingThePassword_AndWritesNothing()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		const string wrong = "Wr0ng-Page-Pass!word-91";
		var admin = Admin;
		using var page = SetupPage.Start();

		var response = await page.PostAsync(SetupPage.Form(
			page.ReadSetupToken(),
			server: admin.DataSource,
			database: "FaranoTests_" + Guid.NewGuid().ToString("N"),
			mode: "Sql",
			userName: admin.UserID,
			password: wrong,
			trustServerCertificate: true));

		Assert.Equal(HttpStatusCode.OK, response.Status);
		Assert.Contains(DatabaseSetupErrorCatalog.Describe(DatabaseSetupErrorCode.LoginFailed).Message, response.Text);
		Assert.Matches("شناسه پیگیری: [0-9a-f]{12}", response.Text);
		Assert.DoesNotContain(wrong, response.Html);
		Assert.DoesNotContain(wrong, response.Text);
		Assert.DoesNotContain(wrong, page.Factory.Logs.All);
		Assert.DoesNotContain(admin.Password, page.Factory.Logs.All);
		Assert.False(File.Exists(page.SettingsFile));
		Assert.True(File.Exists(page.TokenFile));
	}
}
