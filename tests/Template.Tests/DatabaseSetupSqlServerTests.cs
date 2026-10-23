using System;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.DatabaseSetup;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Template.Tests;

/// <summary>
/// The database setup against a real SQL Server. Runs only when <c>FARANOU_TEST_SQLSERVER</c> is set
/// (same variable and skipping rule as the other SQL Server tests). Every test creates and drops its
/// own database named <c>FaranouTests_Setup_*</c> and uses a temp settings folder.
/// </summary>
public sealed class DatabaseSetupSqlServerTests
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

	private static DatabaseSetupRequest Request(string database, bool continuePartial = false, string? password = null)
	{
		var admin = Admin;
		return new DatabaseSetupRequest
		{
			Server = admin.DataSource,
			Database = database,
			AuthenticationMode = "Sql",
			UserName = admin.UserID,
			Password = password ?? admin.Password,
			TrustServerCertificate = true,
			ContinuePartialMigrations = continuePartial,
		};
	}

	private static string ConnectionFor(string database)
	{
		var builder = Admin;
		builder.InitialCatalog = database;
		return builder.ConnectionString;
	}

	private static Data.DatabaseContext Context(string database) =>
		new(new DbContextOptionsBuilder<Data.DatabaseContext>()
			.UseSqlServer(ConnectionFor(database), sql => sql.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName))
			.Options);

	private static async Task WithDatabaseAsync(Func<string, TempFolder, DatabaseConnectionSetupService, DatabaseConnectionSettingsStore, Task> test)
	{
		var name = "FaranoTests_Setup_" + Guid.NewGuid().ToString("N")[..12];
		using var folder = new TempFolder();
		var store = new DatabaseConnectionSettingsStore(folder.Combine("settings"));
		var service = new DatabaseConnectionSetupService(store, NullLogger<DatabaseConnectionSetupService>.Instance);
		try
		{
			await test(name, folder, service, store);
		}
		finally
		{
			await using var cleanup = Context(name);
			await cleanup.Database.EnsureDeletedAsync();
		}
	}

	/// <summary>The setup is single-use: the one-time token must be outstanding for every run.</summary>
	private static Task<DatabaseSetupResult> Configure(
		DatabaseConnectionSettingsStore store,
		DatabaseConnectionSetupService service,
		DatabaseSetupRequest request,
		System.Threading.CancellationToken cancellationToken,
		IProgress<DatabaseSetupStageOutcome>? progress = null)
	{
		store.EnsureSetupToken();
		return service.ConfigureAsync(request, cancellationToken, progress);
	}

	private sealed class SyncProgress(Action<DatabaseSetupStageOutcome> handler) : IProgress<DatabaseSetupStageOutcome>
	{
		public void Report(DatabaseSetupStageOutcome value) => handler(value);
	}

	private static async Task ExecuteAsync(string database, string sql)
	{
		await using var connection = new SqlConnection(ConnectionFor(database));
		await connection.OpenAsync();
		await using var command = new SqlCommand(sql, connection);
		await command.ExecuteNonQueryAsync();
	}

	[Fact]
	public async Task CreatesTheDatabase_RunsMigrations_AndSavesProtectedSettings()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (name, folder, service, store) =>
		{
			store.EnsureSetupToken();

			var result = await Configure(store, service, Request(name), TestContext.Current.CancellationToken);

			Assert.True(result.Succeeded, result.ErrorMessage);
			Assert.True(result.DatabaseCreated);
			Assert.True(result.MigrationsApplied);
			Assert.Equal(DatabaseSetupStage.Completed, result.Stages.Last().Stage);
			Assert.Null(store.SetupAccessToken);

			var loaded = new DatabaseConnectionSettingsStore(folder.Combine("settings")).Load();
			Assert.Equal(DatabaseSettingsStatus.Loaded, loaded.Status);
			var saved = new SqlConnectionStringBuilder(loaded.ConnectionString);
			Assert.Equal(name, saved.InitialCatalog);
			Assert.Equal("Farano", saved.ApplicationName);
			Assert.Equal(SqlConnectionEncryptOption.Mandatory, saved.Encrypt);
			Assert.Equal(15, saved.ConnectTimeout);

			await using var context = Context(name);
			Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
		});
	}

	[Fact]
	public async Task ExistingDatabaseWithMatchingSchema_IsSavedWithoutChange()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (name, folder, service, store) =>
		{
			Assert.True((await Configure(store, service, Request(name), TestContext.Current.CancellationToken)).Succeeded);

			var again = await Configure(store, service, Request(name), TestContext.Current.CancellationToken);

			Assert.True(again.Succeeded, again.ErrorMessage);
			Assert.False(again.DatabaseCreated);
			Assert.False(again.MigrationsApplied);
			Assert.Equal(DatabaseSetupDatabaseState.UpToDate, again.DatabaseState);
			Assert.Single(System.IO.Directory.EnumerateFiles(folder.Combine("settings", "backups")));
		});
	}

	[Fact]
	public async Task MissingDatabase_WithoutCreateOption_FailsAndCreatesNothing()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (name, folder, service, store) =>
		{
			var request = new DatabaseSetupRequest
			{
				Server = Admin.DataSource, Database = name, AuthenticationMode = "Sql", UserName = Admin.UserID,
				Password = Admin.Password, TrustServerCertificate = true, CreateDatabaseIfMissing = false,
			};

			var result = await Configure(store, service, request, TestContext.Current.CancellationToken);

			Assert.Equal(DatabaseSetupErrorCode.DatabaseMissing, result.ErrorCode);
			await using var master = new SqlConnection(ConnectionFor("master"));
			await master.OpenAsync(TestContext.Current.CancellationToken);
			await using var check = new SqlCommand("SELECT DB_ID(@n)", master);
			check.Parameters.AddWithValue("@n", name);
			Assert.True(await check.ExecuteScalarAsync(TestContext.Current.CancellationToken) is null or DBNull);
		});
	}

	[Fact]
	public async Task ForeignDatabase_IsRejected_AndNothingIsTouched()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (name, folder, service, store) =>
		{
			await ExecuteAsync("master", $"CREATE DATABASE [{name}]");
			await ExecuteAsync(name, "CREATE TABLE dbo.SomebodyElsesData (Id int PRIMARY KEY); INSERT dbo.SomebodyElsesData VALUES (1);");

			var result = await Configure(store, service, Request(name, continuePartial: true), TestContext.Current.CancellationToken);

			Assert.False(result.Succeeded);
			Assert.Equal(DatabaseSetupErrorCode.NotFaranoDatabase, result.ErrorCode);
			Assert.Equal(DatabaseSetupDatabaseState.ForeignOrLegacy, result.DatabaseState);
			Assert.False(System.IO.File.Exists(store.SettingsFilePath));
			await using var connection = new SqlConnection(ConnectionFor(name));
			await connection.OpenAsync(TestContext.Current.CancellationToken);
			await using var command = new SqlCommand(
				"SELECT (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0), (SELECT COUNT(*) FROM dbo.SomebodyElsesData), OBJECT_ID(N'dbo.__EFMigrationsHistory')",
				connection);
			await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
			Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal(1, reader.GetInt32(0));
			Assert.Equal(1, reader.GetInt32(1));
			Assert.True(reader.IsDBNull(2));
		});
	}

	[Fact]
	public async Task UnknownMigrationInHistory_MeansANewerDatabase_NotAForeignOne()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (name, folder, service, store) =>
		{
			Assert.True((await Configure(store, service, Request(name), TestContext.Current.CancellationToken)).Succeeded);
			await ExecuteAsync(name, "INSERT dbo.__EFMigrationsHistory (MigrationId, ProductVersion) VALUES (N'29990101000000_FromTheFuture', N'99.0.0')");

			var result = await Configure(store, service, Request(name), TestContext.Current.CancellationToken);

			Assert.Equal(DatabaseSetupErrorCode.DatabaseNewerThanApplication, result.ErrorCode);
			Assert.Equal(DatabaseSetupDatabaseState.NewerThanApplication, result.DatabaseState);
			Assert.Contains("نسخهٔ جدیدتر فرانو", result.ErrorMessage);
		});
	}

	[Fact]
	public async Task HistoryWithOnlyUnknownMigrations_IsForeign()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (name, folder, service, store) =>
		{
			await ExecuteAsync("master", $"CREATE DATABASE [{name}]");
			await ExecuteAsync(name, "CREATE TABLE dbo.__EFMigrationsHistory (MigrationId nvarchar(150) PRIMARY KEY, ProductVersion nvarchar(32)); INSERT dbo.__EFMigrationsHistory VALUES (N'20200101000000_OtherApp', N'8.0.0');");

			var result = await Configure(store, service, Request(name), TestContext.Current.CancellationToken);

			Assert.Equal(DatabaseSetupErrorCode.NotFaranoDatabase, result.ErrorCode);
		});
	}

	[Fact]
	public async Task PartialMigration_NeedsTheCheckbox_ThenRetryIsSafe()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (name, folder, service, store) =>
		{
			await using (var context = Context(name))
			{
				var first = context.Database.GetMigrations().First();
				await context.GetService<IMigrator>().MigrateAsync(first, TestContext.Current.CancellationToken);
			}

			var refused = await Configure(store, service, Request(name), TestContext.Current.CancellationToken);
			Assert.Equal(DatabaseSetupErrorCode.PendingMigrations, refused.ErrorCode);
			Assert.Equal(DatabaseSetupDatabaseState.PartiallyMigrated, refused.DatabaseState);
			Assert.False(System.IO.File.Exists(store.SettingsFilePath));

			var done = await Configure(store, service, Request(name, continuePartial: true), TestContext.Current.CancellationToken);
			Assert.True(done.Succeeded, done.ErrorMessage);
			Assert.True(done.MigrationsApplied);

			var retry = await Configure(store, service, Request(name), TestContext.Current.CancellationToken);
			Assert.True(retry.Succeeded);
			Assert.False(retry.MigrationsApplied);
		});
	}

	[Fact]
	public async Task WrongPassword_IsLoginFailed_WithoutLeakingThePassword()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		var log = new CapturingLogger<DatabaseConnectionSetupService>();
		using var folder = new TempFolder();
		var armedStore = new DatabaseConnectionSettingsStore(folder.Combine("s"));
		armedStore.EnsureSetupToken();
		var service = new DatabaseConnectionSetupService(armedStore, log);
		const string wrong = "Wr0ng-Pass!word";

		var result = await Configure(armedStore, service, Request("FaranoTests_Setup_x", password: wrong), TestContext.Current.CancellationToken);

		Assert.Equal(DatabaseSetupErrorCode.LoginFailed, result.ErrorCode);
		Assert.Equal(DatabaseSetupStage.TestingConnection, result.FailedStage);
		Assert.DoesNotContain(wrong, log.All + result.ErrorMessage + result.Remedy);
		Assert.DoesNotContain(Admin.Password, log.All + result.ErrorMessage + result.Remedy);
	}

	[Fact]
	public async Task WrongHost_IsServerUnreachableOrTimeout()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		using var folder = new TempFolder();
		var armedStore = new DatabaseConnectionSettingsStore(folder.Combine("s"));
		armedStore.EnsureSetupToken();
		var service = new DatabaseConnectionSetupService(armedStore, NullLogger<DatabaseConnectionSetupService>.Instance);
		var request = new DatabaseSetupRequest
		{
			Server = "127.0.0.1,1", Database = "Farano", AuthenticationMode = "Sql", UserName = "sa", Password = "irrelevant", TrustServerCertificate = true,
		};

		var result = await Configure(armedStore, service, request, TestContext.Current.CancellationToken);

		Assert.Contains(result.ErrorCode, new[] { DatabaseSetupErrorCode.ServerUnreachable, DatabaseSetupErrorCode.Timeout, DatabaseSetupErrorCode.InstanceNotFound });
	}

	[Fact]
	public async Task FullwidthMasterIsRefused_AndMasterIsUntouched()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (name, folder, service, store) =>
		{
			foreach (var lookAlike in new[] { "\uFF4D\uFF41\uFF53\uFF54\uFF45\uFF52", "\uFEFFmaster", "ma\u200Bster", "m\u200Dodel" })
			{
				var result = await Configure(store, service, Request(lookAlike), TestContext.Current.CancellationToken);

				Assert.False(result.Succeeded);
				Assert.Equal(DatabaseSetupErrorCode.ValidationFailed, result.ErrorCode);
			}

			await using var master = new SqlConnection(ConnectionFor("master"));
			await master.OpenAsync(TestContext.Current.CancellationToken);
			await using var command = new SqlCommand("SELECT OBJECT_ID(N'master.dbo.__EFMigrationsHistory'), (SELECT COUNT(*) FROM master.sys.tables WHERE name = N'Users')", master);
			await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
			Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.True(reader.IsDBNull(0));
			Assert.Equal(0, reader.GetInt32(1));
			Assert.False(System.IO.File.Exists(store.SettingsFilePath));
		});
	}

	[Fact]
	public async Task ClosingTheBrowserAfterTheConnectionTest_DoesNotInterruptCreateMigrateAndSave()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (name, folder, service, store) =>
		{
			using var cancel = new System.Threading.CancellationTokenSource();
			var progress = new SyncProgress(outcome =>
			{
				if (outcome.Stage == DatabaseSetupStage.Connected)
				{
					cancel.Cancel(); // the request is gone from here on
				}
			});

			var result = await Configure(store, service, Request(name), cancel.Token, progress);

			Assert.True(cancel.IsCancellationRequested);
			Assert.True(result.Succeeded, result.ErrorMessage);
			Assert.True(result.DatabaseCreated);
			Assert.True(result.MigrationsApplied);
			Assert.Equal(DatabaseSettingsStatus.Loaded, new DatabaseConnectionSettingsStore(folder.Combine("settings")).Load().Status);
		});
	}

	[Fact]
	public async Task FailedInspection_ReplacesTheConnectedStage_InsteadOfDuplicatingIt()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

		await WithDatabaseAsync(async (name, folder, service, store) =>
		{
			await ExecuteAsync("master", $"CREATE DATABASE [{name}]");
			var login = "fl_" + Guid.NewGuid().ToString("N")[..10];
			const string loginPassword = "Fx9!aB-7qLmN2pZ";
			await ExecuteAsync("master", $"CREATE LOGIN [{login}] WITH PASSWORD = N'{loginPassword}', CHECK_POLICY = OFF");
			try
			{
				// The login may connect to master but has no user in the target database: 4060 on inspection.
				var request = new DatabaseSetupRequest
				{
					Server = Admin.DataSource, Database = name, AuthenticationMode = "Sql", UserName = login,
					Password = loginPassword, TrustServerCertificate = true,
				};

				var result = await Configure(store, service, request, TestContext.Current.CancellationToken);

				Assert.Equal(DatabaseSetupErrorCode.DatabaseNoAccess, result.ErrorCode);
				var connected = Assert.Single(result.Stages, outcome => outcome.Stage == DatabaseSetupStage.Connected);
				Assert.Equal(DatabaseSetupStageStatus.Failed, connected.Status);
			}
			finally
			{
				SqlConnection.ClearAllPools(); // pooled sessions of the login would block DROP LOGIN
				await ExecuteAsync("master", $"DROP LOGIN [{login}]");
			}
		});
	}
}
