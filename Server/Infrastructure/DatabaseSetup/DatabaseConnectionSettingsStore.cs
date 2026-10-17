using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Server.Infrastructure.DatabaseSetup;

/// <summary>
/// Stores the SQL Server connection string outside the repository. The payload is protected
/// with ASP.NET Core Data Protection; on Windows the key ring is protected with DPAPI.
/// </summary>
public sealed class DatabaseConnectionSettingsStore
{
    private const string ProtectorPurpose = "TarazAva.DatabaseConnectionSettings.v1";
    private readonly IDataProtector _protector;
    private readonly ILogger<DatabaseConnectionSettingsStore> _logger;

    public DatabaseConnectionSettingsStore(ILogger<DatabaseConnectionSettingsStore> logger)
    {
        _logger = logger;
        var root = GetRootDirectory();
        Directory.CreateDirectory(root);
        var keyDirectory = Path.Combine(root, "keys");
        Directory.CreateDirectory(keyDirectory);
        RestrictDirectoryPermissions(root);
        RestrictDirectoryPermissions(keyDirectory);

        var provider = DataProtectionProvider.Create(
            new DirectoryInfo(keyDirectory),
            dataProtection =>
            {
                if (OperatingSystem.IsWindows())
                {
                    dataProtection.ProtectKeysWithDpapi();
                }
            });

        _protector = provider.CreateProtector(ProtectorPurpose);
        SettingsFilePath = Path.Combine(root, "database-settings.json");
        SetupTokenFilePath = Path.Combine(root, "setup-token.txt");
    }

    public string SettingsFilePath { get; }
    public string SetupTokenFilePath { get; }
    public string? SetupAccessToken { get; private set; }

    /// <summary>
    /// Creates a short-lived bootstrap token outside the repository. It is only generated when
    /// standard configuration and the protected local settings file do not supply a connection.
    /// </summary>
    public void EnsureSetupToken()
    {
        if (string.IsNullOrWhiteSpace(SetupAccessToken))
        {
            SetupAccessToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            File.WriteAllText(SetupTokenFilePath, SetupAccessToken);
            RestrictFilePermissions(SetupTokenFilePath);
        }
    }

    public bool TryLoadConnectionString(out string? connectionString)
    {
        connectionString = null;

        if (!File.Exists(SettingsFilePath))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            var saved = JsonSerializer.Deserialize<SavedDatabaseSettings>(json);
            if (saved is null || string.IsNullOrWhiteSpace(saved.ProtectedConnectionString))
            {
                _logger.LogWarning("The local database settings file is empty or invalid.");
                return false;
            }

            connectionString = _protector.Unprotect(saved.ProtectedConnectionString);
            return !string.IsNullOrWhiteSpace(connectionString);
        }
        catch (Exception exception) when (
            exception is IOException or JsonException or CryptographicException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "The local database settings could not be read or decrypted. No connection string was logged.");
            return false;
        }
    }

    public void SaveConnectionString(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var saved = new SavedDatabaseSettings
        {
            ProtectedConnectionString = _protector.Protect(connectionString),
            SavedAtUtc = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(saved, new JsonSerializerOptions { WriteIndented = true });
        var temporaryPath = SettingsFilePath + ".tmp";
        File.WriteAllText(temporaryPath, json);
        RestrictFilePermissions(temporaryPath);
        File.Move(temporaryPath, SettingsFilePath, overwrite: true);
        RestrictFilePermissions(SettingsFilePath);

        if (File.Exists(SetupTokenFilePath))
        {
            File.Delete(SetupTokenFilePath);
        }

        SetupAccessToken = null;
    }

    private static string GetRootDirectory()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException(
                "The local application data directory is unavailable. Configure a writable LocalApplicationData directory for Taraz Ava.");
        }

        return Path.Combine(localApplicationData, "TarazAva", "Database");
    }

    private static void RestrictDirectoryPermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static void RestrictFilePermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private sealed class SavedDatabaseSettings
    {
        public string ProtectedConnectionString { get; init; } = string.Empty;
        public DateTimeOffset SavedAtUtc { get; init; }
    }
}
