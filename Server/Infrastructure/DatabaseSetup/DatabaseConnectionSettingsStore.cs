using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.DatabaseSetup;

/// <summary>A settings write failed; <see cref="Code" /> says how (no secret is ever in the message).</summary>
public sealed class DatabaseSettingsStoreException : Exception
{
    public DatabaseSettingsStoreException(DatabaseSetupErrorCode code, string message, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
    }

    public DatabaseSetupErrorCode Code { get; }
}

/// <summary>
/// Stores the SQL Server connection string outside the repository and outside the application
/// folder. The payload is protected with ASP.NET Core Data Protection (new application name and
/// purpose, own key ring); on Windows the key ring is protected with DPAPI. A settings file of the
/// previous product version is only ever read (never changed, never deleted) and carried over.
/// SQL data files are never touched by this class.
/// </summary>
public sealed class DatabaseConnectionSettingsStore
{
    private const int BackupsToKeep = 10;
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string? _legacyRoot;
    private readonly ConcurrentQueue<string> _warnings = new();
    private IDataProtector? _protector;

    public DatabaseConnectionSettingsStore(string root, string? legacyRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        SettingsDirectory = Path.GetFullPath(root);
        _legacyRoot = string.IsNullOrWhiteSpace(legacyRoot) ? null : Path.GetFullPath(legacyRoot);
        SettingsFilePath = Path.Combine(SettingsDirectory, FaranoSettingsNames.SettingsFileName);
        SetupTokenFilePath = Path.Combine(SettingsDirectory, FaranoSettingsNames.SetupTokenFileName);
        KeysDirectory = Path.Combine(SettingsDirectory, FaranoSettingsNames.KeysFolderName);
        BackupsDirectory = Path.Combine(SettingsDirectory, FaranoSettingsNames.BackupsFolderName);
        MarkerFilePath = Path.Combine(SettingsDirectory, FaranoSettingsNames.MarkerFileName);
        LockFilePath = Path.Combine(SettingsDirectory, FaranoSettingsNames.LockFileName);
        LegacySettingsFilePath = _legacyRoot is null ? null : Path.Combine(_legacyRoot, LegacySettingsNames.SettingsFileName);
        LegacyKeysDirectory = _legacyRoot is null ? null : Path.Combine(_legacyRoot, LegacySettingsNames.KeysFolderName);
    }

    public string SettingsDirectory { get; }
    public string SettingsFilePath { get; }
    public string SetupTokenFilePath { get; }
    public string KeysDirectory { get; }
    public string BackupsDirectory { get; }
    public string MarkerFilePath { get; }
    public string LockFilePath { get; }
    public string? LegacySettingsFilePath { get; }
    public string? LegacyKeysDirectory { get; }
    public string? SetupAccessToken { get; private set; }

    /// <summary>The result of the last <see cref="Load" /> (shown by the setup page).</summary>
    public DatabaseSettingsLoadResult? LastLoad { get; private set; }

    /// <summary>The first <see cref="Load" /> (start-up). Never changes afterwards; the setup compares against it.</summary>
    public DatabaseSettingsLoadResult? StartupLoad { get; private set; }

    // ---------------------------------------------------------------- setup token

    /// <summary>Creates the one-time bootstrap token file; false when the folder is not writable.</summary>
    public bool TryEnsureSetupToken()
    {
        if (!string.IsNullOrWhiteSpace(SetupAccessToken))
        {
            return true;
        }

        try
        {
            EnsureDirectory(SettingsDirectory);
            var token = System.Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
            File.WriteAllText(SetupTokenFilePath, token);
            RestrictFile(SetupTokenFilePath);
            HardenWindowsAcl(SetupTokenFilePath, isDirectory: false);
            SetupAccessToken = token;
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException or SecurityException or ArgumentException)
        {
            return false;
        }
    }

    public void EnsureSetupToken()
    {
        if (!TryEnsureSetupToken())
        {
            throw new DatabaseSettingsStoreException(
                DatabaseSetupErrorCode.SettingsAccessDenied,
                "The setup token file could not be written to the settings folder.");
        }
    }

    /// <summary>
    /// Evaluates the files without writing anything and without touching <see cref="LastLoad" />
    /// (used to detect that the state changed after start-up).
    /// </summary>
    public DatabaseSettingsLoadResult Inspect() => Evaluate(readOnly: true);

    /// <summary>Warning codes raised while writing (e.g. a Windows ACL that could not be applied); each is returned once.</summary>
    public IReadOnlyList<string> DrainWarnings()
    {
        var list = new List<string>();
        while (_warnings.TryDequeue(out var code))
        {
            list.Add(code);
        }

        return list;
    }

    /// <summary>Lets the host record that the folder turned out to be unusable (e.g. token not writable).</summary>
    public void OverrideLastLoad(DatabaseSettingsLoadResult result) => LastLoad = result;

    // ---------------------------------------------------------------- load

    /// <summary>
    /// Typed load. With <paramref name="readOnly" /> nothing is written (no migration, no marker,
    /// no directory is created); otherwise a valid legacy file is carried over automatically.
    /// </summary>
    public DatabaseSettingsLoadResult Load(bool readOnly = false)
    {
        var result = Evaluate(readOnly);
        LastLoad = result;
        StartupLoad ??= result;
        return result;
    }

    private DatabaseSettingsLoadResult Evaluate(bool readOnly)
    {
        var warnings = new List<string>();
        var currentExists = SafeFileExists(SettingsFilePath, out var currentProbeDenied);
        if (currentProbeDenied)
        {
            return Result(DatabaseSettingsStatus.AccessDenied, null, warnings);
        }

        var legacyDenied = false;
        var legacyExists = LegacySettingsFilePath is not null && SafeFileExists(LegacySettingsFilePath, out legacyDenied);
        if (legacyDenied)
        {
            // An unreadable legacy folder must not look "missing": say so, but keep the behaviour.
            warnings.Add(DatabaseSettingsWarnings.LegacyUnreadable);
        }

        if (!currentExists && !legacyExists)
        {
            return Result(DatabaseSettingsStatus.NotConfigured, null, warnings);
        }

        if (currentExists)
        {
            var current = ReadCurrent();
            if (current.Status != DatabaseSettingsStatus.Loaded)
            {
                // A damaged current file never falls back to the old one: that would hide the damage.
                return Result(current.Status, null, warnings);
            }

            if (!legacyExists)
            {
                return Result(DatabaseSettingsStatus.Loaded, current.Value, warnings);
            }

            var legacy = ReadLegacy();
            if (legacy.Status != DatabaseSettingsStatus.Loaded)
            {
                warnings.Add(DatabaseSettingsWarnings.LegacyUnreadable);
                return Result(DatabaseSettingsStatus.Loaded, current.Value, warnings);
            }

            var marker = ReadMarker();
            if (string.Equals(legacy.Value, current.Value, StringComparison.Ordinal))
            {
                if (marker is null && !readOnly && !TryWriteMarker(legacy.Hash!))
                {
                    warnings.Add(DatabaseSettingsWarnings.MarkerNotWritten);
                }

                return Result(DatabaseSettingsStatus.Loaded, current.Value, warnings);
            }

            if (marker is not null && string.Equals(marker.LegacySha256, legacy.Hash, StringComparison.OrdinalIgnoreCase))
            {
                // The operator already decided: the current value replaced this very legacy file.
                return Result(DatabaseSettingsStatus.Loaded, current.Value, warnings);
            }

            warnings.Add(DatabaseSettingsWarnings.LegacyConflict);
            return Result(DatabaseSettingsStatus.Conflict, current.Value, warnings);
        }

        var onlyLegacy = ReadLegacy();
        if (onlyLegacy.Status != DatabaseSettingsStatus.Loaded)
        {
            return Result(DatabaseSettingsStatus.LegacyUnreadable, null, warnings);
        }

        if (readOnly)
        {
            warnings.Add(DatabaseSettingsWarnings.LegacyMigrationPending);
            return Result(DatabaseSettingsStatus.LoadedFromLegacy, onlyLegacy.Value, warnings);
        }

        try
        {
            Save(onlyLegacy.Value!, FaranoSettingsNames.OriginMigratedFromLegacy, deleteSetupToken: false);
            if (!TryWriteMarker(onlyLegacy.Hash!))
            {
                warnings.Add(DatabaseSettingsWarnings.MarkerNotWritten);
            }

            return Result(DatabaseSettingsStatus.MigratedFromLegacy, onlyLegacy.Value, warnings);
        }
        catch (DatabaseSettingsStoreException)
        {
            warnings.Add(DatabaseSettingsWarnings.NewFolderNotWritable);
            return Result(DatabaseSettingsStatus.LoadedFromLegacy, onlyLegacy.Value, warnings);
        }
    }

    private DatabaseSettingsLoadResult Result(DatabaseSettingsStatus status, string? connectionString, List<string> warnings)
    {
        var carriesValue = status is DatabaseSettingsStatus.Loaded or DatabaseSettingsStatus.LoadedFromLegacy
            or DatabaseSettingsStatus.MigratedFromLegacy or DatabaseSettingsStatus.Conflict;

        return new DatabaseSettingsLoadResult
        {
            Status = status,
            ConnectionString = carriesValue ? connectionString : null,
            SettingsDirectory = SettingsDirectory,
            WarningCodes = warnings.ToArray(),
        };
    }

    private readonly record struct ReadOutcome(DatabaseSettingsStatus Status, string? Value, string? Hash);

    private ReadOutcome ReadCurrent()
    {
        string json;
        try
        {
            json = File.ReadAllText(SettingsFilePath, Encoding.UTF8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ReadOutcome(DatabaseSettingsStatus.AccessDenied, null, null);
        }

        string? protectedValue;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("version", out var versionElement) ||
                !versionElement.TryGetInt32(out var version))
            {
                return new ReadOutcome(DatabaseSettingsStatus.Corrupt, null, null);
            }

            if (version > FaranoSettingsNames.SettingsVersion)
            {
                return new ReadOutcome(DatabaseSettingsStatus.UnsupportedVersion, null, null);
            }

            if (version < FaranoSettingsNames.SettingsVersion ||
                !document.RootElement.TryGetProperty("protectedConnectionString", out var protectedElement) ||
                protectedElement.ValueKind != JsonValueKind.String)
            {
                return new ReadOutcome(DatabaseSettingsStatus.Corrupt, null, null);
            }

            protectedValue = protectedElement.GetString();
        }
        catch (JsonException)
        {
            return new ReadOutcome(DatabaseSettingsStatus.Corrupt, null, null);
        }

        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return new ReadOutcome(DatabaseSettingsStatus.Corrupt, null, null);
        }

        try
        {
            if (!HasKeyFiles(KeysDirectory))
            {
                // Asking the protector would silently create a brand-new key; that can never decrypt this file.
                return new ReadOutcome(DatabaseSettingsStatus.Undecryptable, null, null);
            }

            var value = GetCurrentProtector().Unprotect(protectedValue);
            return string.IsNullOrWhiteSpace(value)
                ? new ReadOutcome(DatabaseSettingsStatus.Corrupt, null, null)
                : new ReadOutcome(DatabaseSettingsStatus.Loaded, value, null);
        }
        catch (UnauthorizedAccessException)
        {
            return new ReadOutcome(DatabaseSettingsStatus.AccessDenied, null, null);
        }
        catch (Exception exception) when (
            exception is CryptographicException or FormatException or XmlException or IOException or InvalidOperationException)
        {
            return new ReadOutcome(DatabaseSettingsStatus.Undecryptable, null, null);
        }
    }

    private ReadOutcome ReadLegacy()
    {
        try
        {
            if (LegacySettingsFilePath is null || LegacyKeysDirectory is null || !File.Exists(LegacySettingsFilePath))
            {
                return new ReadOutcome(DatabaseSettingsStatus.LegacyUnreadable, null, null);
            }

            var bytes = File.ReadAllBytes(LegacySettingsFilePath);
            var hash = System.Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var saved = JsonSerializer.Deserialize<LegacyFile>(Encoding.UTF8.GetString(bytes), ReadOptions);
            if (saved is null || string.IsNullOrWhiteSpace(saved.ProtectedConnectionString) || !HasKeyFiles(LegacyKeysDirectory))
            {
                return new ReadOutcome(DatabaseSettingsStatus.LegacyUnreadable, null, hash);
            }

            var value = UnprotectLegacy(LegacyKeysDirectory, saved.ProtectedConnectionString);
            return string.IsNullOrWhiteSpace(value)
                ? new ReadOutcome(DatabaseSettingsStatus.LegacyUnreadable, null, hash)
                : new ReadOutcome(DatabaseSettingsStatus.Loaded, value, hash);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or CryptographicException
                or FormatException or XmlException or InvalidOperationException)
        {
            return new ReadOutcome(DatabaseSettingsStatus.LegacyUnreadable, null, null);
        }
    }

    private static bool SafeFileExists(string path, out bool denied)
    {
        denied = false;
        try
        {
            if (File.Exists(path))
            {
                return true;
            }

            // File.Exists is false for an unreadable parent folder; tell "missing" from "cannot look".
            var directory = Path.GetDirectoryName(path);
            if (directory is not null && Directory.Exists(directory))
            {
                _ = Directory.EnumerateFileSystemEntries(directory).FirstOrDefault();
            }

            return false;
        }
        catch (UnauthorizedAccessException)
        {
            denied = true;
            return false;
        }
        catch (IOException)
        {
            denied = true;
            return false;
        }
    }

    private static bool HasKeyFiles(string directory) =>
        Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.xml").Any();

    // ---------------------------------------------------------------- crypto

    private IDataProtector GetCurrentProtector()
    {
        if (_protector is not null)
        {
            return _protector;
        }

        var provider = DataProtectionProvider.Create(
            new DirectoryInfo(KeysDirectory),
            dataProtection =>
            {
                dataProtection.SetApplicationName(FaranoSettingsNames.ApplicationName);

                if (OperatingSystem.IsWindows())
                {
                    dataProtection.ProtectKeysWithDpapi();
                }
            });

        _protector = provider.CreateProtector(FaranoSettingsNames.ProtectorPurpose);
        return _protector;
    }

    /// <summary>
    /// Legacy protector over a read-only view of the legacy key ring: the folder is never created,
    /// and a key the ring wants to add (rotation) stays in memory instead of being written there.
    /// </summary>
    private static string UnprotectLegacy(string keysDirectory, string payload)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddDataProtection().SetApplicationName(LegacySettingsNames.ApplicationName);
        if (OperatingSystem.IsWindows())
        {
            builder.ProtectKeysWithDpapi();
        }

        var repository = new ReadOnlyXmlRepository(keysDirectory);
        services.PostConfigure<KeyManagementOptions>(options => options.XmlRepository = repository);

        using var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(LegacySettingsNames.ProtectorPurpose)
            .Unprotect(payload);
    }

    private sealed class ReadOnlyXmlRepository : IXmlRepository
    {
        private readonly string _directory;
        private readonly List<XElement> _inMemory = [];

        public ReadOnlyXmlRepository(string directory) => _directory = directory;

        public IReadOnlyCollection<XElement> GetAllElements()
        {
            var elements = new List<XElement>();
            foreach (var file in Directory.EnumerateFiles(_directory, "*.xml"))
            {
                elements.Add(XElement.Load(file));
            }

            lock (_inMemory)
            {
                elements.AddRange(_inMemory);
            }

            return elements;
        }

        public void StoreElement(XElement element, string friendlyName)
        {
            lock (_inMemory)
            {
                _inMemory.Add(element);
            }
        }
    }

    // ---------------------------------------------------------------- save

    /// <summary>
    /// Protects and stores the connection string atomically. An existing file (even an unreadable
    /// one) is first copied to <c>backups/</c>; the last ten copies are kept.
    /// </summary>
    /// <param name="expectedStatus">
    /// When given, the state of the files is checked again under the lock; if it differs the call
    /// writes nothing and throws <see cref="DatabaseSetupErrorCode.AlreadyConfigured" />.
    /// </param>
    public void Save(
        string connectionString,
        string origin = FaranoSettingsNames.OriginSetup,
        DatabaseSettingsStatus? expectedStatus = null)
        => Save(connectionString, origin, deleteSetupToken: true, expectedStatus);

    private void Save(string connectionString, string origin, bool deleteSetupToken, DatabaseSettingsStatus? expectedStatus = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        try
        {
            EnsureDirectories();
            using var settingsLock = AcquireLock();
            if (expectedStatus is not null && Evaluate(readOnly: true).Status != expectedStatus)
            {
                throw new DatabaseSettingsStoreException(
                    DatabaseSetupErrorCode.AlreadyConfigured,
                    "The settings changed after start-up; nothing was written.");
            }

            var content = JsonSerializer.SerializeToUtf8Bytes(
                new CurrentFile
                {
                    Version = FaranoSettingsNames.SettingsVersion,
                    ProtectedConnectionString = GetCurrentProtector().Protect(connectionString),
                    SavedAtUtc = DateTimeOffset.UtcNow,
                    Origin = origin,
                },
                WriteOptions);

            string? backupPath = File.Exists(SettingsFilePath) ? CreateBackup() : null;
            WriteAtomically(SettingsFilePath, content);
            HardenWindowsAcl(SettingsFilePath, isDirectory: false);

            var verify = ReadCurrent();
            if (verify.Status != DatabaseSettingsStatus.Loaded || !string.Equals(verify.Value, connectionString, StringComparison.Ordinal))
            {
                if (backupPath is not null)
                {
                    File.Copy(backupPath, SettingsFilePath, overwrite: true);
                }
                else
                {
                    File.Delete(SettingsFilePath);
                }

                throw new DatabaseSettingsStoreException(
                    DatabaseSetupErrorCode.SettingsIoError,
                    "The settings file was written but could not be read back; the previous state was restored.");
            }

            LastLoad = new DatabaseSettingsLoadResult
            {
                Status = DatabaseSettingsStatus.Loaded,
                ConnectionString = connectionString,
                SettingsDirectory = SettingsDirectory,
            };

            if (deleteSetupToken)
            {
                // The token is dead from this moment, whether or not its file can be removed.
                SetupAccessToken = null;
                try
                {
                    File.Delete(SetupTokenFilePath); // no error when the file is already gone
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // The settings are saved and verified: this is a warning, not a failed setup.
                    _warnings.Enqueue(DatabaseSettingsWarnings.SetupTokenNotDeleted);
                }
            }
        }
        catch (DatabaseSettingsStoreException)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new DatabaseSettingsStoreException(DatabaseSetupErrorCode.SettingsAccessDenied, "The settings folder is not writable.", exception);
        }
        catch (IOException exception)
        {
            throw new DatabaseSettingsStoreException(DatabaseSetupErrorCode.SettingsIoError, "The settings file could not be written.", exception);
        }
        catch (Exception exception) when (exception is CryptographicException or XmlException or InvalidOperationException)
        {
            throw new DatabaseSettingsStoreException(DatabaseSetupErrorCode.SettingsProtectionFailed, "The connection settings could not be protected.", exception);
        }
    }

    /// <summary>
    /// Operator decision for a legacy value that differs from the current one (the "migrate" command).
    /// Both choices are repeatable; the legacy file is never modified.
    /// </summary>
    public DatabaseSettingsLoadResult MigrateFromLegacy(bool replaceCurrent, bool keepCurrent)
    {
        var loaded = Load();
        if (loaded.Status != DatabaseSettingsStatus.Conflict || (!replaceCurrent && !keepCurrent))
        {
            return loaded;
        }

        var legacy = ReadLegacy();
        if (legacy.Status != DatabaseSettingsStatus.Loaded)
        {
            return Load();
        }

        if (replaceCurrent)
        {
            Save(legacy.Value!, FaranoSettingsNames.OriginMigratedFromLegacy, deleteSetupToken: false);
        }

        if (!TryWriteMarker(legacy.Hash!))
        {
            throw new DatabaseSettingsStoreException(
                DatabaseSetupErrorCode.SettingsIoError, "The migration marker could not be written.");
        }

        return Load();
    }

    private bool TryWriteMarker(string legacySha256)
    {
        try
        {
            EnsureDirectories();
            using var settingsLock = AcquireLock();
            var marker = new MarkerFile
            {
                LegacyPath = LegacySettingsFilePath ?? string.Empty,
                LegacySha256 = legacySha256,
                MigratedAtUtc = DateTimeOffset.UtcNow,
            };

            WriteAtomically(MarkerFilePath, JsonSerializer.SerializeToUtf8Bytes(marker, WriteOptions));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private MarkerFile? ReadMarker()
    {
        try
        {
            return File.Exists(MarkerFilePath)
                ? JsonSerializer.Deserialize<MarkerFile>(File.ReadAllText(MarkerFilePath, Encoding.UTF8), ReadOptions)
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    // ---------------------------------------------------------------- file system helpers

    private void WriteAtomically(string path, byte[] content)
    {
        CleanLeftoverTemporaryFiles();
        var temporaryPath = path + ".tmp";
        try
        {
            using (var stream = new FileStream(
                temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }

            RestrictFile(temporaryPath);
            File.Move(temporaryPath, path, overwrite: true);
            RestrictFile(path);
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // The original failure is the one worth reporting; a stale .tmp is cleaned on the next write.
            }

            throw;
        }
    }

    private void CleanLeftoverTemporaryFiles()
    {
        // Only Farano's own temporary names; a custom folder may hold files that belong to someone else.
        foreach (var leftover in new[] { SettingsFilePath + ".tmp", MarkerFilePath + ".tmp" })
        {
            try
            {
                if (File.Exists(leftover))
                {
                    File.Delete(leftover);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Ignored: leftovers are never read, only removed when possible.
            }
        }
    }

    private string CreateBackup()
    {
        var backupsExisted = Directory.Exists(BackupsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        if (!backupsExisted)
        {
            RestrictDirectory(BackupsDirectory);
        }

        var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var target = Path.Combine(BackupsDirectory, $"database-connection.{stamp}.json");
        for (var counter = 1; File.Exists(target); counter++)
        {
            target = Path.Combine(BackupsDirectory, $"database-connection.{stamp}-{counter}.json");
        }

        File.Copy(SettingsFilePath, target);
        RestrictFile(target);

        var stale = Directory.EnumerateFiles(BackupsDirectory, "database-connection.*.json")
            .OrderByDescending(file => BackupSortKey(file).Stamp, StringComparer.Ordinal)
            .ThenByDescending(file => BackupSortKey(file).Counter)
            .Skip(BackupsToKeep);
        foreach (var file in stale)
        {
            File.Delete(file);
        }

        return target;
    }

    /// <summary>Timestamp then same-millisecond counter ("…Z.json" = 0, "…Z-3.json" = 3): deterministic age order.</summary>
    private static (string Stamp, int Counter) BackupSortKey(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file)["database-connection.".Length..];
        var marker = name.IndexOf('Z');
        if (marker < 0)
        {
            return (name, 0);
        }

        var suffix = name[(marker + 1)..].TrimStart('-');
        return (name[..(marker + 1)], int.TryParse(suffix, out var counter) ? counter : 0);
    }

    private FileStream AcquireLock()
    {
        var deadline = DateTime.UtcNow + LockWait;
        while (true)
        {
            try
            {
                return new FileStream(LockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(100);
            }
        }
    }

    private void EnsureDirectories()
    {
        EnsureDirectory(SettingsDirectory);
        EnsureDirectory(KeysDirectory);
    }

    private void EnsureDirectory(string path)
    {
        // Permissions are tightened only on folders Farano creates, never on a pre-existing custom folder.
        var existed = Directory.Exists(path);
        Directory.CreateDirectory(path);
        if (!existed)
        {
            RestrictDirectory(path);
        }
    }

    private void RestrictDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            HardenWindowsAcl(path, isDirectory: true);
        }
        else
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>
    /// Windows only: no inherited permissions; full control for the current user, SYSTEM and
    /// Administrators. Never throws; a failure is reported as a warning code.
    /// </summary>
    private void HardenWindowsAcl(string path, bool isDirectory)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            ApplyWindowsAcl(path, isDirectory);
        }
        catch (Exception)
        {
            // Never throws, whatever the ACL API or WindowsIdentity does (NotSupported, Security, COM, ...):
            // hardening is best effort and reported as a warning code.
            _warnings.Enqueue(DatabaseSettingsWarnings.WindowsAclNotApplied);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyWindowsAcl(string path, bool isDirectory)
    {
        var sids = new List<SecurityIdentifier>
        {
            new(WellKnownSidType.LocalSystemSid, null),
            new(WellKnownSidType.BuiltinAdministratorsSid, null),
        };
        if (WindowsIdentity.GetCurrent().User is { } current)
        {
            sids.Add(current);
        }

        if (isDirectory)
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            foreach (var sid in sids)
            {
                security.AddAccessRule(new FileSystemAccessRule(
                    sid, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None, AccessControlType.Allow));
            }

            new DirectoryInfo(path).SetAccessControl(security);
        }
        else
        {
            var security = new FileSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            foreach (var sid in sids)
            {
                security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
            }

            new FileInfo(path).SetAccessControl(security);
        }
    }

    private static void RestrictFile(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private sealed class CurrentFile
    {
        [JsonPropertyName("version")] public int Version { get; init; }
        [JsonPropertyName("protectedConnectionString")] public string ProtectedConnectionString { get; init; } = string.Empty;
        [JsonPropertyName("savedAtUtc")] public DateTimeOffset SavedAtUtc { get; init; }
        [JsonPropertyName("origin")] public string Origin { get; init; } = string.Empty;
    }

    private sealed class MarkerFile
    {
        [JsonPropertyName("legacyPath")] public string LegacyPath { get; init; } = string.Empty;
        [JsonPropertyName("legacySha256")] public string LegacySha256 { get; init; } = string.Empty;
        [JsonPropertyName("migratedAtUtc")] public DateTimeOffset MigratedAtUtc { get; init; }
    }

    private sealed class LegacyFile
    {
        public string ProtectedConnectionString { get; init; } = string.Empty;
        public DateTimeOffset SavedAtUtc { get; init; }
    }
}
