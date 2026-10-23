using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.DatabaseSetup;
using Infrastructure.Security;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Pages;
using Xunit;

namespace Template.Tests;

public sealed class DatabaseSetupServiceTests
{
	public static TheoryData<DatabaseSetupStage, int?, DatabaseSetupErrorCode> ClassifyRows => new()
	{
		{ DatabaseSetupStage.TestingConnection, 18456, DatabaseSetupErrorCode.LoginFailed },
		{ DatabaseSetupStage.TestingConnection, 18452, DatabaseSetupErrorCode.WindowsAuthNotTrusted },
		{ DatabaseSetupStage.TestingConnection, 18470, DatabaseSetupErrorCode.LoginDisabledOrExpired },
		{ DatabaseSetupStage.TestingConnection, 18487, DatabaseSetupErrorCode.LoginDisabledOrExpired },
		{ DatabaseSetupStage.TestingConnection, 18488, DatabaseSetupErrorCode.LoginDisabledOrExpired },
		{ DatabaseSetupStage.TestingConnection, 53, DatabaseSetupErrorCode.ServerUnreachable },
		{ DatabaseSetupStage.TestingConnection, 2, DatabaseSetupErrorCode.ServerUnreachable },
		{ DatabaseSetupStage.TestingConnection, 10060, DatabaseSetupErrorCode.ServerUnreachable },
		{ DatabaseSetupStage.TestingConnection, 11001, DatabaseSetupErrorCode.ServerUnreachable },
		{ DatabaseSetupStage.TestingConnection, -1, DatabaseSetupErrorCode.ServerUnreachable },
		{ DatabaseSetupStage.TestingConnection, 26, DatabaseSetupErrorCode.InstanceNotFound },
		{ DatabaseSetupStage.TestingConnection, 40, DatabaseSetupErrorCode.InstanceNotFound },
		{ DatabaseSetupStage.TestingConnection, -2, DatabaseSetupErrorCode.Timeout },
		{ DatabaseSetupStage.RunningMigrations, 258, DatabaseSetupErrorCode.Timeout },
		{ DatabaseSetupStage.TestingConnection, 233, DatabaseSetupErrorCode.ConnectionClosed },
		{ DatabaseSetupStage.TestingConnection, 10054, DatabaseSetupErrorCode.ConnectionClosed },
		{ DatabaseSetupStage.Connected, 4060, DatabaseSetupErrorCode.DatabaseNoAccess },
		{ DatabaseSetupStage.Connected, 916, DatabaseSetupErrorCode.DatabaseNoAccess },
		{ DatabaseSetupStage.CreatingDatabase, 262, DatabaseSetupErrorCode.PermissionDenied },
		{ DatabaseSetupStage.CreatingDatabase, 229, DatabaseSetupErrorCode.PermissionDenied },
		{ DatabaseSetupStage.RunningMigrations, 15247, DatabaseSetupErrorCode.PermissionDenied },
		{ DatabaseSetupStage.CreatingDatabase, 5170, DatabaseSetupErrorCode.DatabaseFileError },
		{ DatabaseSetupStage.CreatingDatabase, 5133, DatabaseSetupErrorCode.DatabaseFileError },
		{ DatabaseSetupStage.CreatingDatabase, 5120, DatabaseSetupErrorCode.DatabaseFileError },
		{ DatabaseSetupStage.CreatingDatabase, 1802, DatabaseSetupErrorCode.DatabaseFileError },
		{ DatabaseSetupStage.RunningMigrations, 1105, DatabaseSetupErrorCode.DiskFull },
		{ DatabaseSetupStage.RunningMigrations, 9002, DatabaseSetupErrorCode.DiskFull },
		{ DatabaseSetupStage.RunningMigrations, 547, DatabaseSetupErrorCode.MigrationFailed },
		{ DatabaseSetupStage.RunningMigrations, null, DatabaseSetupErrorCode.MigrationFailed },
		{ DatabaseSetupStage.TestingConnection, 999999, DatabaseSetupErrorCode.Unknown },
		{ DatabaseSetupStage.TestingConnection, null, DatabaseSetupErrorCode.Unknown },
		{ DatabaseSetupStage.CreatingDatabase, 1801, DatabaseSetupErrorCode.Unknown },
	};

	[Theory]
	[MemberData(nameof(ClassifyRows))]
	public void Classify_MapsSqlNumbersAndStages(DatabaseSetupStage stage, int? number, DatabaseSetupErrorCode expected)
	{
		Assert.Equal(expected, DatabaseSetupErrorCatalog.Classify(stage, number, null));
	}

	[Fact]
	public void Classify_CertificateAndAuthenticationExceptionChains()
	{
		var chain = new InvalidOperationException("outer", new AuthenticationException("The remote certificate is invalid"));

		Assert.Equal(DatabaseSetupErrorCode.CertificateNotTrusted, DatabaseSetupErrorCatalog.Classify(DatabaseSetupStage.TestingConnection, -1, chain));
		Assert.Equal(DatabaseSetupErrorCode.CertificateNotTrusted, DatabaseSetupErrorCatalog.Classify(DatabaseSetupStage.TestingConnection, null, new AuthenticationException()));
	}

	[Fact]
	public void Classify_SettingsErrorsInSavingStage()
	{
		Assert.Equal(DatabaseSetupErrorCode.SettingsAccessDenied, DatabaseSetupErrorCatalog.Classify(DatabaseSetupStage.SavingSettings, null, new UnauthorizedAccessException()));
		Assert.Equal(DatabaseSetupErrorCode.SettingsIoError, DatabaseSetupErrorCatalog.Classify(DatabaseSetupStage.SavingSettings, null, new IOException()));
		Assert.Equal(DatabaseSetupErrorCode.SettingsProtectionFailed, DatabaseSetupErrorCatalog.Classify(DatabaseSetupStage.SavingSettings, null, new System.Security.Cryptography.CryptographicException()));
		Assert.Equal(DatabaseSetupErrorCode.SettingsIoError, DatabaseSetupErrorCatalog.Classify(
			DatabaseSetupStage.SavingSettings, null, new DatabaseSettingsStoreException(DatabaseSetupErrorCode.SettingsIoError, "x")));
	}

	[Fact]
	public void EveryErrorCode_HasAMessageAndRemedy_DistinctFromTheGenericOne()
	{
		var (generic, genericRemedy) = DatabaseSetupErrorCatalog.Describe(DatabaseSetupErrorCode.Unknown);
		var seen = new System.Collections.Generic.HashSet<string>();

		foreach (var code in Enum.GetValues<DatabaseSetupErrorCode>().Where(c => c != DatabaseSetupErrorCode.None))
		{
			var (message, remedy) = DatabaseSetupErrorCatalog.Describe(code);
			Assert.False(string.IsNullOrWhiteSpace(message), code.ToString());
			Assert.False(string.IsNullOrWhiteSpace(remedy), code.ToString());
			if (code != DatabaseSetupErrorCode.Unknown)
			{
				Assert.NotEqual(generic, message);
				Assert.NotEqual(genericRemedy, remedy);
				Assert.True(seen.Add(message), "Duplicate message for " + code);
			}
		}
	}

	[Fact]
	public void ClassifyDatabaseState_Rules()
	{
		string[] known = ["m1", "m2"];

		Assert.Equal(DatabaseSetupDatabaseState.Empty, DatabaseConnectionSetupService.ClassifyDatabaseState(false, 0, [], known));
		Assert.Equal(DatabaseSetupDatabaseState.ForeignOrLegacy, DatabaseConnectionSetupService.ClassifyDatabaseState(false, 3, [], known));
		Assert.Equal(DatabaseSetupDatabaseState.ForeignOrLegacy, DatabaseConnectionSetupService.ClassifyDatabaseState(true, 3, [], known));
		Assert.Equal(DatabaseSetupDatabaseState.UpToDate, DatabaseConnectionSetupService.ClassifyDatabaseState(true, 9, ["m1", "m2"], known));
		Assert.Equal(DatabaseSetupDatabaseState.PartiallyMigrated, DatabaseConnectionSetupService.ClassifyDatabaseState(true, 9, ["m1"], known));
		Assert.Equal(DatabaseSetupDatabaseState.NewerThanApplication, DatabaseConnectionSetupService.ClassifyDatabaseState(true, 9, ["m1", "other"], known));
		Assert.Equal(DatabaseSetupDatabaseState.ForeignOrLegacy, DatabaseConnectionSetupService.ClassifyDatabaseState(true, 9, ["x", "y"], known));
	}

	[Fact]
	public void InputModel_UserNameAndPassword_AreNotImplicitlyRequired()
	{
		var provider = new ServiceCollection().AddLogging().AddMvcCore().AddDataAnnotations().Services
			.BuildServiceProvider().GetRequiredService<IModelMetadataProvider>();

		foreach (var property in new[] { "UserName", "Password" })
		{
			Assert.False(provider.GetMetadataForProperty(typeof(DatabaseSetupModel.InputModel), property).IsRequired, property);
		}

		Assert.True(provider.GetMetadataForProperty(typeof(DatabaseSetupModel.InputModel), "SetupToken").IsRequired);
	}

	private static async Task<(DatabaseSetupResult Result, CapturingLogger<DatabaseConnectionSetupService> Log)> RunAgainstRefusedPortAsync(
		TempFolder folder, string password)
	{
		var port = FreeLoopbackPort();
		var log = new CapturingLogger<DatabaseConnectionSetupService>();
		var armedStore = new DatabaseConnectionSettingsStore(folder.Combine("s"));
		armedStore.EnsureSetupToken();
		var service = new DatabaseConnectionSetupService(armedStore, log);

		var result = await service.ConfigureAsync(
			new DatabaseSetupRequest
			{
				Server = "127.0.0.1," + port,
				Database = "Farano",
				AuthenticationMode = "Sql",
				UserName = "sa",
				Password = password,
			},
			TestContext.Current.CancellationToken);

		return (result, log);
	}

	private static int FreeLoopbackPort()
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		listener.Stop();
		return port;
	}

	[Fact]
	public async Task WrongHost_IsClassified_AndNothingIsSaved_AndSecretsAreNotLogged()
	{
		using var folder = new TempFolder();
		const string password = "Zq9!Sup3r-Secret";

		var (result, log) = await RunAgainstRefusedPortAsync(folder, password);

		Assert.False(result.Succeeded);
		var expectedCodes = new[]
		{
			DatabaseSetupErrorCode.ServerUnreachable, DatabaseSetupErrorCode.Timeout, DatabaseSetupErrorCode.InstanceNotFound,
			DatabaseSetupErrorCode.ConnectionClosed,
		};
		Assert.True(expectedCodes.Contains(result.ErrorCode), log.All);
		Assert.Equal(DatabaseSetupStage.TestingConnection, result.FailedStage);
		Assert.Equal(12, result.TrackingId!.Length);
		Assert.Matches("^[0-9a-f]{12}$", result.TrackingId);
		Assert.False(string.IsNullOrWhiteSpace(result.Remedy));
		Assert.Contains(result.TrackingId, result.ErrorMessage);
		Assert.Contains(result.Stages, s => s.Stage == DatabaseSetupStage.TestingConnection && s.Status == DatabaseSetupStageStatus.Failed);
		Assert.False(File.Exists(Path.Combine(folder.Combine("s"), "database-connection.json")));

		var everything = log.All + result.ErrorMessage + result.Remedy + string.Join("|", result.Stages.Select(s => s.Detail));
		Assert.DoesNotContain(password, everything);
		Assert.DoesNotContain("Password", everything, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("Server=127.0.0.1", everything);
		Assert.Contains(result.TrackingId, log.All);
	}

	[Fact]
	public async Task OnlyOneSetupRunsAtATime()
	{
		using var folder = new TempFolder();
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start(); // accepts the TCP connection but never answers the SQL handshake
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		var armedStore = new DatabaseConnectionSettingsStore(folder.Combine("s"));
		armedStore.EnsureSetupToken();
		var service = new DatabaseConnectionSetupService(armedStore, NullLogger<DatabaseConnectionSetupService>.Instance);
		var request = new DatabaseSetupRequest { Server = "127.0.0.1," + port, Database = "Farano", AuthenticationMode = "Windows" };
		using var cancel = new CancellationTokenSource();

		var first = service.ConfigureAsync(request, cancel.Token);
		var second = await service.ConfigureAsync(request, TestContext.Current.CancellationToken);

		Assert.False(second.Succeeded);
		Assert.Equal(DatabaseSetupErrorCode.SetupInProgress, second.ErrorCode);

		await cancel.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

		// The gate is released afterwards.
		var third = await service.ConfigureAsync(new DatabaseSetupRequest { Server = string.Empty }, TestContext.Current.CancellationToken);
		Assert.Equal(DatabaseSetupErrorCode.ValidationFailed, third.ErrorCode);
	}

	// ------------------------------------------------------------ page model (token gate, all errors at once)

	private static DatabaseSetupModel CreatePage(DatabaseConnectionSettingsStore store, DatabaseConnectionSetupService service) =>
		new(
			store,
			service,
			new InfrastructureMasterBootstrap(new PasswordHashService()),
			new ConfigurationBuilder().Build(),
			NullLogger<DatabaseSetupModel>.Instance);

	[Fact]
	public async Task Page_WrongToken_SurfacesTokenAndAllFieldErrorsAtOnce_AndDoesNotCallTheService()
	{
		using var folder = new TempFolder();
		var store = new DatabaseConnectionSettingsStore(folder.Combine("s"));
		store.Load();
		store.EnsureSetupToken();
		var page = CreatePage(store, new DatabaseConnectionSetupService(store, NullLogger<DatabaseConnectionSetupService>.Instance));
		page.Input = new DatabaseSetupModel.InputModel
		{
			SetupToken = "wrong",
			Server = string.Empty,
			Database = "master",
			AuthenticationMode = "Sql",
			UserName = null,
			Password = null,
		};

		await page.OnPostAsync(TestContext.Current.CancellationToken);

		var keys = page.ModelState.Where(e => e.Value!.Errors.Count > 0).Select(e => e.Key).Order().ToArray();
		Assert.Equal(
			["Input.Database", "Input.Password", "Input.Server", "Input.SetupToken", "Input.UserName"],
			keys);
		Assert.False(File.Exists(store.SettingsFilePath));
	}

	[Fact]
	public async Task Page_WindowsAuthentication_WithoutCredentials_NoLongerFailsValidation()
	{
		using var folder = new TempFolder();
		var store = new DatabaseConnectionSettingsStore(folder.Combine("s"));
		store.Load();
		store.EnsureSetupToken();
		var page = CreatePage(store, new DatabaseConnectionSetupService(store, NullLogger<DatabaseConnectionSetupService>.Instance));
		page.Input = new DatabaseSetupModel.InputModel
		{
			SetupToken = store.SetupAccessToken!,
			Server = "127.0.0.1," + FreeLoopbackPort(),
			Database = "Farano",
			AuthenticationMode = "Windows",
		};

		await page.OnPostAsync(TestContext.Current.CancellationToken);

		Assert.Empty(page.ModelState.Where(e => e.Value!.Errors.Count > 0));
		Assert.NotNull(page.TrackingId); // the service ran and failed at the connection, not at validation
		Assert.NotEqual(DatabaseSetupErrorCode.ValidationFailed, page.FailureCode);
		Assert.Equal(DatabaseSetupStage.TestingConnection, page.FailedStage);
	}

	[Fact]
	public async Task Page_UnreadableExistingFile_NeedsConfirmReplace_AndBlockedStateOffersNoForm()
	{
		using var folder = new TempFolder();
		var root = folder.Combine("s");
		Directory.CreateDirectory(root);
		File.WriteAllText(Path.Combine(root, "database-connection.json"), "corrupt");
		var store = new DatabaseConnectionSettingsStore(root);
		Assert.Equal(DatabaseSettingsStatus.Corrupt, store.Load().Status);
		store.EnsureSetupToken();
		var page = CreatePage(store, new DatabaseConnectionSetupService(store, NullLogger<DatabaseConnectionSetupService>.Instance));
		page.Input = new DatabaseSetupModel.InputModel
		{
			SetupToken = store.SetupAccessToken!,
			Server = "127.0.0.1," + FreeLoopbackPort(),
			ConfirmReplace = false,
		};

		await page.OnPostAsync(TestContext.Current.CancellationToken);

		Assert.True(page.RequiresReplaceConfirmation);
		Assert.True(page.ModelState["Input.ConfirmReplace"]!.Errors.Count > 0);
		Assert.Equal("corrupt", File.ReadAllText(Path.Combine(root, "database-connection.json")));

		using var blockedFolder = new TempFolder();
		var blockedRoot = blockedFolder.Combine("s");
		Directory.CreateDirectory(blockedRoot);
		File.WriteAllText(Path.Combine(blockedRoot, "database-connection.json"), "{\"version\":9,\"protectedConnectionString\":\"x\"}");
		var blockedStore = new DatabaseConnectionSettingsStore(blockedRoot);
		blockedStore.Load();
		var blocked = CreatePage(blockedStore, new DatabaseConnectionSetupService(blockedStore, NullLogger<DatabaseConnectionSetupService>.Instance));
		blocked.OnGet();

		Assert.True(blocked.IsBlocked);
		Assert.Equal(DatabaseSettingsStatus.UnsupportedVersion, blocked.SettingsStatus);
		Assert.False(string.IsNullOrWhiteSpace(blocked.StatusTitle));
		Assert.False(string.IsNullOrWhiteSpace(blocked.StatusRemedy));
		Assert.Equal(blockedRoot, blocked.SettingsDirectory);
	}

	// ------------------------------------------------------------ operator command

	[Fact]
	public void SettingsCommand_Status_IsReadOnlyAndPrintsNoSecrets_AndMigrateIsIdempotent()
	{
		using var folder = new TempFolder();
		var legacy = folder.Combine("old");
		LegacyFixture.Create(legacy, "Server=h;Database=Farano;User ID=u;Password=VerySecret-9;Encrypt=True");
		var root = folder.Combine("new");
		var store = new DatabaseConnectionSettingsStore(root, legacy);
		var location = new DatabaseSettingsLocation(root, DatabaseSettingsLocation.DefaultSource, legacy);
		var legacyBefore = LegacyFixture.Snapshot(legacy);

		var statusOut = new StringWriter();
		var statusCode = SettingsCommand.Run(["--settings", "status"], store, location, statusOut, new StringWriter());

		Assert.Equal(0, statusCode);
		Assert.Contains("LoadedFromLegacy", statusOut.ToString());
		Assert.DoesNotContain("VerySecret", statusOut.ToString());
		Assert.False(Directory.Exists(root));

		for (var run = 0; run < 2; run++)
		{
			var migrateOut = new StringWriter();
			Assert.Equal(0, SettingsCommand.Run(["--settings", "migrate"], store, location, migrateOut, new StringWriter()));
			Assert.DoesNotContain("VerySecret", migrateOut.ToString());
		}

		Assert.Equal(legacyBefore, LegacyFixture.Snapshot(legacy));
		Assert.Equal(2, SettingsCommand.Run(["--settings", "bogus"], store, location, new StringWriter(), new StringWriter()));
		Assert.Equal(2, SettingsCommand.Run(["--settings", "migrate", "--keep-current", "--replace-current"], store, location, new StringWriter(), new StringWriter()));
	}

	[Fact]
	public void SettingsCommand_ConflictNeedsDecision_ExitCode2_ThenResolves()
	{
		using var folder = new TempFolder();
		var legacy = folder.Combine("old");
		LegacyFixture.Create(legacy, "Server=old;Database=Farano;Encrypt=True");
		var root = folder.Combine("new");
		new DatabaseConnectionSettingsStore(root, null).Save("Server=new;Database=Farano;Encrypt=True");
		var store = new DatabaseConnectionSettingsStore(root, legacy);
		var location = new DatabaseSettingsLocation(root, DatabaseSettingsLocation.DefaultSource, legacy);

		Assert.Equal(2, SettingsCommand.Run(["--settings", "migrate"], store, location, new StringWriter(), new StringWriter()));
		Assert.Equal(0, SettingsCommand.Run(["--settings", "migrate", "--keep-current"], store, location, new StringWriter(), new StringWriter()));
		Assert.Equal(0, SettingsCommand.Run(["--settings", "status"], store, location, new StringWriter(), new StringWriter()));
	}

	[Fact]
	public void SettingsCommand_CorruptSettings_ExitCode1()
	{
		using var folder = new TempFolder();
		var root = folder.Combine("new");
		Directory.CreateDirectory(root);
		File.WriteAllText(Path.Combine(root, "database-connection.json"), "{");
		var store = new DatabaseConnectionSettingsStore(root, null);

		Assert.Equal(1, SettingsCommand.Run(["--settings", "status"], store,
			new DatabaseSettingsLocation(root, DatabaseSettingsLocation.DefaultSource, null), new StringWriter(), new StringWriter()));
	}

	// ------------------------------------------------------------ single-use gate, secrets, M4

	[Fact]
	public async Task Gate_WithoutAnOutstandingToken_IsAlreadyConfigured_AndNothingHappens()
	{
		using var folder = new TempFolder();
		var store = new DatabaseConnectionSettingsStore(folder.Combine("s"));
		var service = new DatabaseConnectionSetupService(store, NullLogger<DatabaseConnectionSetupService>.Instance);

		var result = await service.ConfigureAsync(
			new DatabaseSetupRequest { Server = "127.0.0.1," + FreeLoopbackPort(), Database = "Farano" },
			TestContext.Current.CancellationToken);

		Assert.Equal(DatabaseSetupErrorCode.AlreadyConfigured, result.ErrorCode);
		Assert.DoesNotContain(result.Stages, outcome => outcome.Stage == DatabaseSetupStage.TestingConnection);
	}

	[Fact]
	public async Task Gate_ANewlyAppearedValidFile_IsNeverOverwritten()
	{
		using var folder = new TempFolder();
		var root = folder.Combine("s");
		var store = new DatabaseConnectionSettingsStore(root);
		Assert.Equal(DatabaseSettingsStatus.NotConfigured, store.Load().Status);
		store.EnsureSetupToken();
		// Another process (or a second operator) saves valid settings after start-up.
		new DatabaseConnectionSettingsStore(root).Save("Server=other;Database=Farano;Encrypt=True");
		var other = File.ReadAllBytes(store.SettingsFilePath);
		store.EnsureSetupToken();
		var service = new DatabaseConnectionSetupService(store, NullLogger<DatabaseConnectionSetupService>.Instance);

		var result = await service.ConfigureAsync(
			new DatabaseSetupRequest { Server = "127.0.0.1," + FreeLoopbackPort(), Database = "Farano" },
			TestContext.Current.CancellationToken);

		Assert.Equal(DatabaseSetupErrorCode.AlreadyConfigured, result.ErrorCode);
		Assert.Equal(other, File.ReadAllBytes(store.SettingsFilePath));
	}

	[Fact]
	public void Save_UpdatesLastLoad_SoTheReplaceConfirmationIsNotStale()
	{
		using var folder = new TempFolder();
		var root = folder.Combine("s");
		Directory.CreateDirectory(root);
		File.WriteAllText(Path.Combine(root, "database-connection.json"), "corrupt");
		var store = new DatabaseConnectionSettingsStore(root);
		Assert.True(store.Load().RequiresReplaceConfirmation);

		store.Save("Server=h;Database=Farano;Encrypt=True");

		Assert.Equal(DatabaseSettingsStatus.Loaded, store.LastLoad!.Status);
		Assert.False(store.LastLoad.RequiresReplaceConfirmation);
		Assert.False(store.LastLoad.IsBlocked);
	}

	[Fact]
	public async Task Page_AfterAFailedPost_DoesNotEchoThePasswordOrTheToken()
	{
		using var folder = new TempFolder();
		var store = new DatabaseConnectionSettingsStore(folder.Combine("s"));
		store.Load();
		store.EnsureSetupToken();
		var page = CreatePage(store, new DatabaseConnectionSetupService(store, NullLogger<DatabaseConnectionSetupService>.Instance));
		page.Input = new DatabaseSetupModel.InputModel
		{
			SetupToken = "typed-token",
			Server = string.Empty,
			AuthenticationMode = "Sql",
			UserName = "sa",
			Password = "Typed-Secret-1",
		};
		page.ModelState.SetModelValue("Input.Password", "Typed-Secret-1", "Typed-Secret-1");
		page.ModelState.SetModelValue("Input.SetupToken", "typed-token", "typed-token");

		await page.OnPostAsync(TestContext.Current.CancellationToken);

		Assert.Null(page.Input.Password);
		Assert.Equal(string.Empty, page.Input.SetupToken);
		Assert.Null(page.ModelState["Input.Password"]!.AttemptedValue);
		Assert.Null(page.ModelState["Input.SetupToken"]!.AttemptedValue);
		Assert.Null(page.ModelState["Input.Password"]!.RawValue);
		Assert.Null(page.ModelState["Input.SetupToken"]!.RawValue);
	}
}
