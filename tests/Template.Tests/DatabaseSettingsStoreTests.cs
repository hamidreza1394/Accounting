using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Infrastructure.DatabaseSetup;
using Xunit;

namespace Template.Tests;

public sealed class DatabaseSettingsStoreTests
{
	private const string ConnectionA = "Server=host-a;Database=Farano;User ID=u;Password=PassWordA-1!;Encrypt=True";
	private const string ConnectionB = "Server=host-b;Database=Farano;User ID=u;Password=PassWordB-2!;Encrypt=True";

	private sealed class Fixture : IDisposable
	{
		private readonly TempFolder _folder = new();

		public string NewRoot => _folder.Combine("new", "Settings");
		public string LegacyRoot => _folder.Combine("old", "TarazAva", "Database");

		public DatabaseConnectionSettingsStore CreateStore() => new(NewRoot, LegacyRoot);

		public string LegacyFile => Path.Combine(LegacyRoot, "database-settings.json");

		public void Dispose() => _folder.Dispose();
	}

	[Fact]
	public void Neither_IsNotConfigured_AndNothingIsCreated()
	{
		using var fixture = new Fixture();
		var store = fixture.CreateStore();

		var result = store.Load();

		Assert.Equal(DatabaseSettingsStatus.NotConfigured, result.Status);
		Assert.Null(result.ConnectionString);
		Assert.False(Directory.Exists(fixture.NewRoot));
		Assert.False(Directory.Exists(fixture.LegacyRoot));
	}

	[Fact]
	public void OnlyNewValid_IsLoaded_AndRoundTripsWithFileLayout()
	{
		using var fixture = new Fixture();
		fixture.CreateStore().Save(ConnectionA);

		var result = fixture.CreateStore().Load();

		Assert.Equal(DatabaseSettingsStatus.Loaded, result.Status);
		Assert.Equal(ConnectionA, result.ConnectionString);
		using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.NewRoot, "database-connection.json")));
		Assert.Equal(2, document.RootElement.GetProperty("version").GetInt32());
		Assert.Equal("Setup", document.RootElement.GetProperty("origin").GetString());
		Assert.True(document.RootElement.TryGetProperty("savedAtUtc", out _));
		Assert.DoesNotContain("PassWordA", File.ReadAllText(Path.Combine(fixture.NewRoot, "database-connection.json")));
		Assert.True(Directory.Exists(Path.Combine(fixture.NewRoot, "keys")));
	}

	[Fact]
	public void OnlyOldValid_IsMigrated_OldFileIsUnchangedByteForByte()
	{
		using var fixture = new Fixture();
		LegacyFixture.Create(fixture.LegacyRoot, ConnectionA);
		var before = LegacyFixture.Snapshot(fixture.LegacyRoot);

		var first = fixture.CreateStore().Load();
		var second = fixture.CreateStore().Load();

		Assert.Equal(DatabaseSettingsStatus.MigratedFromLegacy, first.Status);
		Assert.Equal(ConnectionA, first.ConnectionString);
		Assert.Equal(DatabaseSettingsStatus.Loaded, second.Status);
		Assert.Equal(ConnectionA, second.ConnectionString);
		Assert.Equal(before, LegacyFixture.Snapshot(fixture.LegacyRoot));

		using var current = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.NewRoot, "database-connection.json")));
		Assert.Equal("MigratedFromTarazAva", current.RootElement.GetProperty("origin").GetString());
		using var marker = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.NewRoot, "migration.json")));
		Assert.Equal(fixture.LegacyFile, marker.RootElement.GetProperty("legacyPath").GetString());
		Assert.Equal(
			Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixture.LegacyFile))).ToLowerInvariant(),
			marker.RootElement.GetProperty("legacySha256").GetString());
		Assert.True(marker.RootElement.TryGetProperty("migratedAtUtc", out _));
	}

	[Fact]
	public void LegacyKeyRing_IsNeverWrittenOrCreated_EvenWhenOnlyReadOnly()
	{
		using var fixture = new Fixture();
		LegacyFixture.Create(fixture.LegacyRoot, ConnectionA);
		var before = LegacyFixture.Snapshot(fixture.LegacyRoot);

		var result = fixture.CreateStore().Load(readOnly: true);

		Assert.Equal(DatabaseSettingsStatus.LoadedFromLegacy, result.Status);
		Assert.Contains(DatabaseSettingsWarnings.LegacyMigrationPending, result.WarningCodes);
		Assert.Equal(before, LegacyFixture.Snapshot(fixture.LegacyRoot));
		Assert.False(Directory.Exists(fixture.NewRoot), "A read-only load must not create the new folder.");
	}

	[Fact]
	public void OnlyOldValid_ButNewFolderNotWritable_IsLoadedFromLegacyWithWarning()
	{
		using var fixture = new Fixture();
		LegacyFixture.Create(fixture.LegacyRoot, ConnectionA);
		// A file where the settings folder should be makes every write impossible.
		Directory.CreateDirectory(Path.GetDirectoryName(fixture.NewRoot)!);
		File.WriteAllText(fixture.NewRoot, "not a folder");

		var result = fixture.CreateStore().Load();

		Assert.Equal(DatabaseSettingsStatus.LoadedFromLegacy, result.Status);
		Assert.Equal(ConnectionA, result.ConnectionString);
		Assert.Contains(DatabaseSettingsWarnings.NewFolderNotWritable, result.WarningCodes);
	}

	[Fact]
	public void BothValidSameValue_IsLoaded_AndMarkerIsAddedWhenMissing()
	{
		using var fixture = new Fixture();
		fixture.CreateStore().Save(ConnectionA);
		LegacyFixture.Create(fixture.LegacyRoot, ConnectionA);

		var result = fixture.CreateStore().Load();

		Assert.Equal(DatabaseSettingsStatus.Loaded, result.Status);
		Assert.Empty(result.WarningCodes);
		Assert.True(File.Exists(Path.Combine(fixture.NewRoot, "migration.json")));
	}

	[Fact]
	public void BothDiffer_WithoutMarker_IsConflict_RunsOnNew_AndNeverOverwrites()
	{
		using var fixture = new Fixture();
		fixture.CreateStore().Save(ConnectionB);
		LegacyFixture.Create(fixture.LegacyRoot, ConnectionA);
		var newBefore = File.ReadAllBytes(Path.Combine(fixture.NewRoot, "database-connection.json"));

		var result = fixture.CreateStore().Load();

		Assert.Equal(DatabaseSettingsStatus.Conflict, result.Status);
		Assert.Equal(ConnectionB, result.ConnectionString);
		Assert.Contains(DatabaseSettingsWarnings.LegacyConflict, result.WarningCodes);
		Assert.Equal(newBefore, File.ReadAllBytes(Path.Combine(fixture.NewRoot, "database-connection.json")));
		Assert.False(File.Exists(Path.Combine(fixture.NewRoot, "migration.json")));
	}

	[Fact]
	public void BothDiffer_MarkerMatchesCurrentOldFile_IsLoadedNew()
	{
		using var fixture = new Fixture();
		LegacyFixture.Create(fixture.LegacyRoot, ConnectionA);
		fixture.CreateStore().Load(); // migrates and writes the marker
		fixture.CreateStore().Save(ConnectionB); // operator later changed the connection in the new folder

		var result = fixture.CreateStore().Load();

		Assert.Equal(DatabaseSettingsStatus.Loaded, result.Status);
		Assert.Equal(ConnectionB, result.ConnectionString);
	}

	[Fact]
	public void BothDiffer_OldFileChangedAfterMarker_IsConflict()
	{
		using var fixture = new Fixture();
		LegacyFixture.Create(fixture.LegacyRoot, ConnectionA);
		fixture.CreateStore().Load();
		fixture.CreateStore().Save(ConnectionB);
		LegacyFixture.Create(fixture.LegacyRoot, "Server=host-c;Database=Farano;Encrypt=True"); // new content, new hash

		Assert.Equal(DatabaseSettingsStatus.Conflict, fixture.CreateStore().Load().Status);
	}

	[Fact]
	public void MigrateCommandChoices_ResolveConflict_AndAreIdempotent()
	{
		using var fixture = new Fixture();
		fixture.CreateStore().Save(ConnectionB);
		LegacyFixture.Create(fixture.LegacyRoot, ConnectionA);
		var legacyBefore = LegacyFixture.Snapshot(fixture.LegacyRoot);

		Assert.Equal(DatabaseSettingsStatus.Conflict, fixture.CreateStore().MigrateFromLegacy(false, false).Status);

		var keep = fixture.CreateStore().MigrateFromLegacy(replaceCurrent: false, keepCurrent: true);
		Assert.Equal(DatabaseSettingsStatus.Loaded, keep.Status);
		Assert.Equal(ConnectionB, keep.ConnectionString);
		Assert.Equal(DatabaseSettingsStatus.Loaded, fixture.CreateStore().MigrateFromLegacy(false, true).Status);

		// Replace from a fresh conflict.
		using var other = new Fixture();
		other.CreateStore().Save(ConnectionB);
		LegacyFixture.Create(other.LegacyRoot, ConnectionA);
		var replace = other.CreateStore().MigrateFromLegacy(replaceCurrent: true, keepCurrent: false);
		Assert.Equal(DatabaseSettingsStatus.Loaded, replace.Status);
		Assert.Equal(ConnectionA, replace.ConnectionString);
		Assert.Equal(DatabaseSettingsStatus.Loaded, other.CreateStore().MigrateFromLegacy(true, false).Status);
		Assert.Single(Directory.EnumerateFiles(Path.Combine(other.NewRoot, "backups")));

		Assert.Equal(legacyBefore, LegacyFixture.Snapshot(fixture.LegacyRoot));
	}

	[Theory]
	[InlineData("not json at all")]
	[InlineData("{\"version\":2}")]
	[InlineData("[1,2]")]
	[InlineData("{\"version\":1,\"protectedConnectionString\":\"x\"}")]
	[InlineData("{\"version\":2,\"protectedConnectionString\":\"\"}")]
	public void NewCorrupt_IsCorrupt_AndOldFileIsNotUsedAsFallback(string content)
	{
		using var fixture = new Fixture();
		Directory.CreateDirectory(fixture.NewRoot);
		File.WriteAllText(Path.Combine(fixture.NewRoot, "database-connection.json"), content);
		LegacyFixture.Create(fixture.LegacyRoot, ConnectionA);

		var result = fixture.CreateStore().Load();

		Assert.Equal(DatabaseSettingsStatus.Corrupt, result.Status);
		Assert.Null(result.ConnectionString);
		Assert.True(result.RequiresReplaceConfirmation);
	}

	[Fact]
	public void NewVersionGreaterThanTwo_IsUnsupported_AndBlocked()
	{
		using var fixture = new Fixture();
		Directory.CreateDirectory(fixture.NewRoot);
		File.WriteAllText(Path.Combine(fixture.NewRoot, "database-connection.json"), "{\"version\":3,\"protectedConnectionString\":\"x\"}");

		var result = fixture.CreateStore().Load();

		Assert.Equal(DatabaseSettingsStatus.UnsupportedVersion, result.Status);
		Assert.True(result.IsBlocked);
	}

	[Fact]
	public void NewUndecryptable_WhenKeysAreLostOrWrong()
	{
		using var fixture = new Fixture();
		fixture.CreateStore().Save(ConnectionA);

		// Lost key ring.
		Directory.Delete(Path.Combine(fixture.NewRoot, "keys"), recursive: true);
		var lost = fixture.CreateStore().Load();
		Assert.Equal(DatabaseSettingsStatus.Undecryptable, lost.Status);
		Assert.False(Directory.Exists(Path.Combine(fixture.NewRoot, "keys")), "Loading must not create a fresh key.");

		// Wrong payload with a valid key ring.
		using var second = new Fixture();
		second.CreateStore().Save(ConnectionA);
		var path = Path.Combine(second.NewRoot, "database-connection.json");
		File.WriteAllText(path, "{\"version\":2,\"protectedConnectionString\":\"AAAA-not-a-payload\"}");
		Assert.Equal(DatabaseSettingsStatus.Undecryptable, second.CreateStore().Load().Status);
	}

	[Fact]
	public void NewFileWithoutReadPermission_IsAccessDenied_NotTheFirstRunScreen()
	{
		Assert.SkipWhen(OperatingSystem.IsWindows() || IsRoot(), "chmod does not restrict root or Windows.");
		using var fixture = new Fixture();
		fixture.CreateStore().Save(ConnectionA);
		var path = Path.Combine(fixture.NewRoot, "database-connection.json");
		File.SetUnixFileMode(path, UnixFileMode.None);
		try
		{
			var result = fixture.CreateStore().Load();

			Assert.Equal(DatabaseSettingsStatus.AccessDenied, result.Status);
			Assert.True(result.IsBlocked);
		}
		finally
		{
			File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
		}
	}

	[Fact]
	public void OnlyOldButCorruptOrUndecryptable_IsLegacyUnreadable()
	{
		using var corrupt = new Fixture();
		Directory.CreateDirectory(corrupt.LegacyRoot);
		File.WriteAllText(corrupt.LegacyFile, "{ not json");
		Assert.Equal(DatabaseSettingsStatus.LegacyUnreadable, corrupt.CreateStore().Load().Status);

		using var noKeys = new Fixture();
		LegacyFixture.Create(noKeys.LegacyRoot, ConnectionA);
		Directory.Delete(Path.Combine(noKeys.LegacyRoot, "keys"), recursive: true);
		var result = noKeys.CreateStore().Load();
		Assert.Equal(DatabaseSettingsStatus.LegacyUnreadable, result.Status);
		Assert.True(result.RequiresReplaceConfirmation);
		Assert.False(Directory.Exists(Path.Combine(noKeys.LegacyRoot, "keys")), "The legacy folder must never be recreated.");
	}

	[Fact]
	public void NewValid_WithOldCorrupt_IsLoadedWithWarning()
	{
		using var fixture = new Fixture();
		fixture.CreateStore().Save(ConnectionA);
		Directory.CreateDirectory(fixture.LegacyRoot);
		File.WriteAllText(fixture.LegacyFile, "garbage");

		var result = fixture.CreateStore().Load();

		Assert.Equal(DatabaseSettingsStatus.Loaded, result.Status);
		Assert.Contains(DatabaseSettingsWarnings.LegacyUnreadable, result.WarningCodes);
	}

	[Fact]
	public void LeftoverTempFile_IsIgnoredOnLoad_AndCleanedOnSave()
	{
		using var fixture = new Fixture();
		fixture.CreateStore().Save(ConnectionA);
		var leftover = Path.Combine(fixture.NewRoot, "database-connection.json.tmp");
		File.WriteAllText(leftover, "half written");

		Assert.Equal(DatabaseSettingsStatus.Loaded, fixture.CreateStore().Load().Status);
		Assert.True(File.Exists(leftover));

		fixture.CreateStore().Save(ConnectionB);

		Assert.False(File.Exists(leftover));
		Assert.Equal(ConnectionB, fixture.CreateStore().Load().ConnectionString);
	}

	[Fact]
	public void Save_OverUnreadableFile_BacksItUpFirst()
	{
		using var fixture = new Fixture();
		Directory.CreateDirectory(fixture.NewRoot);
		var path = Path.Combine(fixture.NewRoot, "database-connection.json");
		File.WriteAllText(path, "corrupt content");

		fixture.CreateStore().Save(ConnectionA);

		var backup = Assert.Single(Directory.EnumerateFiles(Path.Combine(fixture.NewRoot, "backups")));
		Assert.Equal("corrupt content", File.ReadAllText(backup));
		Assert.Matches(@"^database-connection\.\d{8}T\d{9}Z\.json$", Path.GetFileName(backup));
		Assert.Equal(ConnectionA, fixture.CreateStore().Load().ConnectionString);
	}

	[Fact]
	public void Backups_KeepOnlyTheLastTen()
	{
		using var fixture = new Fixture();
		var store = fixture.CreateStore();
		for (var i = 0; i < 14; i++)
		{
			store.Save(ConnectionA + ";Application Name=" + i);
		}

		var backups = Directory.EnumerateFiles(Path.Combine(fixture.NewRoot, "backups")).Select(Path.GetFileName).Order().ToArray();
		Assert.Equal(10, backups.Length);
		Assert.Equal(ConnectionA + ";Application Name=13", fixture.CreateStore().Load().ConnectionString);
	}

	[Fact]
	public void AtomicWriteFailure_LeavesTheOldSettingsIntact()
	{
		using var fixture = new Fixture();
		var store = fixture.CreateStore();
		store.Save(ConnectionA);
		var before = File.ReadAllBytes(store.SettingsFilePath);
		// A directory at the temporary path makes writing the temp file fail before the move.
		Directory.CreateDirectory(store.SettingsFilePath + ".tmp");

		var exception = Assert.Throws<DatabaseSettingsStoreException>(() => store.Save(ConnectionB));

		Assert.Contains(exception.Code, new[] { DatabaseSetupErrorCode.SettingsAccessDenied, DatabaseSetupErrorCode.SettingsIoError });
		Assert.Equal(before, File.ReadAllBytes(store.SettingsFilePath));
		Assert.Equal(ConnectionA, fixture.CreateStore().Load().ConnectionString);
	}

	[Fact]
	public void Save_FailsWithSettingsError_WhenFolderIsNotWritable_WithoutSecretsInMessage()
	{
		using var fixture = new Fixture();
		Directory.CreateDirectory(Path.GetDirectoryName(fixture.NewRoot)!);
		File.WriteAllText(fixture.NewRoot, "file in the way");

		var exception = Assert.ThrowsAny<DatabaseSettingsStoreException>(() => fixture.CreateStore().Save(ConnectionA));

		Assert.DoesNotContain("PassWordA", exception.ToString());
	}

	[Fact]
	public void ExclusiveLock_BlocksASecondWriterAndTimesOut()
	{
		using var fixture = new Fixture();
		var store = fixture.CreateStore();
		store.Save(ConnectionA);
		using var held = new FileStream(store.LockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
		var started = DateTime.UtcNow;

		var exception = Assert.Throws<DatabaseSettingsStoreException>(() => store.Save(ConnectionB));

		Assert.Equal(DatabaseSetupErrorCode.SettingsIoError, exception.Code);
		Assert.True(DateTime.UtcNow - started >= TimeSpan.FromSeconds(4), "The writer must wait for the lock.");
	}

	[Fact]
	public void LoadResultToString_DoesNotLeakTheConnectionString()
	{
		using var fixture = new Fixture();
		fixture.CreateStore().Save(ConnectionA);

		var result = fixture.CreateStore().Load();

		Assert.DoesNotContain("PassWordA", result.ToString());
		Assert.DoesNotContain("host-a", result.ToString());
	}

	[Fact]
	public void SetupToken_IsCreatedInTheSettingsFolder_AndRemovedAfterSave()
	{
		using var fixture = new Fixture();
		var store = fixture.CreateStore();

		store.EnsureSetupToken();

		Assert.False(string.IsNullOrWhiteSpace(store.SetupAccessToken));
		Assert.Equal(store.SetupAccessToken, File.ReadAllText(Path.Combine(fixture.NewRoot, "setup-token.txt")));

		store.Save(ConnectionA);

		Assert.Null(store.SetupAccessToken);
		Assert.False(File.Exists(store.SetupTokenFilePath));
	}

	[Fact]
	public void UnixPermissions_AreRestricted()
	{
		Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix permissions only.");
		using var fixture = new Fixture();
		fixture.CreateStore().Save(ConnectionA);

		Assert.Equal(
			UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
			File.GetUnixFileMode(fixture.NewRoot));
		Assert.Equal(
			UnixFileMode.UserRead | UnixFileMode.UserWrite,
			File.GetUnixFileMode(Path.Combine(fixture.NewRoot, "database-connection.json")));
	}

	private static bool IsRoot() => !OperatingSystem.IsWindows() && Environment.UserName == "root";

	[Fact]
	public void CleanUp_DeletesOnlyFaranosOwnTemporaryFiles()
	{
		using var fixture = new Fixture();
		var store = fixture.CreateStore();
		store.Save(ConnectionA);
		var foreign = Path.Combine(fixture.NewRoot, "someone-elses.tmp");
		File.WriteAllText(foreign, "not ours");
		var own = store.SettingsFilePath + ".tmp";
		File.WriteAllText(own, "leftover");

		store.Save(ConnectionB);

		Assert.True(File.Exists(foreign));
		Assert.False(File.Exists(own));
	}

	[Fact]
	public void PreExistingFolder_KeepsItsPermissions()
	{
		Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix permissions only.");
		using var fixture = new Fixture();
		Directory.CreateDirectory(fixture.NewRoot);
		var shared = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute;
		File.SetUnixFileMode(fixture.NewRoot, shared);

		fixture.CreateStore().Save(ConnectionA);

		Assert.Equal(shared, File.GetUnixFileMode(fixture.NewRoot));
	}

	[Fact]
	public void BackupRetention_OrdersSameMillisecondBackupsByCounter_NotByName()
	{
		using var fixture = new Fixture();
		var store = fixture.CreateStore();
		store.Save(ConnectionA);
		var backups = Path.Combine(fixture.NewRoot, "backups");
		Directory.CreateDirectory(backups);
		// 12 backups in the same millisecond: "...Z.json" is the oldest, then "-1" ... "-11".
		File.WriteAllText(Path.Combine(backups, "database-connection.20200101T000000000Z.json"), "0");
		for (var counter = 1; counter <= 11; counter++)
		{
			File.WriteAllText(Path.Combine(backups, $"database-connection.20200101T000000000Z-{counter}.json"), counter.ToString());
		}

		store.Save(ConnectionB); // adds a much newer backup and prunes to ten

		var names = Directory.EnumerateFiles(backups).Select(Path.GetFileName).ToArray();
		Assert.Equal(10, names.Length);
		Assert.DoesNotContain("database-connection.20200101T000000000Z.json", names);
		Assert.DoesNotContain("database-connection.20200101T000000000Z-1.json", names);
		Assert.DoesNotContain("database-connection.20200101T000000000Z-2.json", names);
		Assert.Contains("database-connection.20200101T000000000Z-11.json", names);
	}

	[Fact]
	public void UnreadableLegacyFolder_IsAWarning_NotAPlainFirstRun()
	{
		Assert.SkipWhen(OperatingSystem.IsWindows() || Environment.UserName == "root", "chmod does not restrict root or Windows.");
		using var fixture = new Fixture();
		LegacyFixture.Create(fixture.LegacyRoot, ConnectionA);
		File.SetUnixFileMode(fixture.LegacyRoot, UnixFileMode.None);
		try
		{
			var result = fixture.CreateStore().Load(readOnly: true);

			Assert.Contains(DatabaseSettingsWarnings.LegacyUnreadable, result.WarningCodes);
		}
		finally
		{
			File.SetUnixFileMode(fixture.LegacyRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}
	}

	[Fact]
	public void Save_WithAChangedState_WritesNothingAndReportsAlreadyConfigured()
	{
		using var fixture = new Fixture();
		var store = fixture.CreateStore();
		Assert.Equal(DatabaseSettingsStatus.NotConfigured, store.Load().Status);
		fixture.CreateStore().Save(ConnectionB); // someone saved valid settings after start-up
		var before = File.ReadAllBytes(store.SettingsFilePath);

		var exception = Assert.Throws<DatabaseSettingsStoreException>(
			() => store.Save(ConnectionA, expectedStatus: store.StartupLoad!.Status));

		Assert.Equal(DatabaseSetupErrorCode.AlreadyConfigured, exception.Code);
		Assert.Equal(before, File.ReadAllBytes(store.SettingsFilePath));
		Assert.False(Directory.Exists(Path.Combine(fixture.NewRoot, "backups")));
	}

	[Fact]
	public void Save_WithTheExpectedState_Works()
	{
		using var fixture = new Fixture();
		var store = fixture.CreateStore();
		store.Load();

		store.Save(ConnectionA, expectedStatus: store.StartupLoad!.Status);

		Assert.Equal(ConnectionA, fixture.CreateStore().Load().ConnectionString);
	}

	[Fact]
	public void StartupLoad_IsImmutable_WhileLastLoadFollowsTheState()
	{
		using var fixture = new Fixture();
		var store = fixture.CreateStore();
		var first = store.Load();

		store.Save(ConnectionA);
		store.Load();

		Assert.Same(first, store.StartupLoad);
		Assert.Equal(DatabaseSettingsStatus.NotConfigured, store.StartupLoad!.Status);
		Assert.Equal(DatabaseSettingsStatus.Loaded, store.LastLoad!.Status);
	}

	[Fact]
	public void TokenFileThatCannotBeDeleted_IsAWarning_AndTheTokenIsDeadAnyway()
	{
		using var fixture = new Fixture();
		var store = fixture.CreateStore();
		store.EnsureSetupToken();
		// A non-empty folder at the token path makes File.Delete fail.
		File.Delete(store.SetupTokenFilePath);
		Directory.CreateDirectory(store.SetupTokenFilePath);
		File.WriteAllText(Path.Combine(store.SetupTokenFilePath, "x"), "x");

		store.Save(ConnectionA);

		Assert.Null(store.SetupAccessToken);
		Assert.Contains(DatabaseSettingsWarnings.SetupTokenNotDeleted, store.DrainWarnings());
		Assert.Equal(ConnectionA, fixture.CreateStore().Load().ConnectionString);
	}

	[Fact]
	public void TryEnsureSetupToken_ReturnsFalse_InsteadOfThrowing_WhenTheFolderIsNotUsable()
	{
		using var fixture = new Fixture();
		Directory.CreateDirectory(Path.GetDirectoryName(fixture.NewRoot)!);
		File.WriteAllText(fixture.NewRoot, "a file where the folder should be");

		Assert.False(fixture.CreateStore().TryEnsureSetupToken());
	}
}
