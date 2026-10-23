namespace Infrastructure.DatabaseSetup;

/// <summary>
/// The one place that holds the product name used for the settings folder, the Data Protection
/// application name and the protector purpose of the connection settings.
/// </summary>
public static class FaranoSettingsNames
{
    public const string Name = "Farano";
    public const string FolderName = Name;
    public const string ApplicationName = Name;
    public const string ProtectorPurpose = Name + ".DatabaseConnectionSettings.v2";

    /// <summary>Configuration key (environment variable <c>Farano__Settings__Directory</c>).</summary>
    public const string SettingsDirectoryKey = Name + ":Settings:Directory";

    public const string SettingsFileName = "database-connection.json";
    public const string MarkerFileName = "migration.json";
    public const string SetupTokenFileName = "setup-token.txt";
    public const string LockFileName = ".lock";
    public const string KeysFolderName = "keys";
    public const string BackupsFolderName = "backups";
    public const int SettingsVersion = 2;

    public const string OriginSetup = "Setup";
    public const string OriginMigratedFromLegacy = "MigratedFromTarazAva";
}

/// <summary>
/// Names of the previous product version. They exist only so the old, protected settings file can
/// be read (read-only) and carried over; nothing is ever written to the legacy folder.
/// </summary>
public static class LegacySettingsNames
{
    public const string FolderName = "TarazAva";
    public const string DatabaseFolderName = "Database";
    public const string ApplicationName = "TarazAva";
    public const string ProtectorPurpose = "TarazAva.DatabaseConnectionSettings.v1";
    public const string SettingsFileName = "database-settings.json";
    public const string KeysFolderName = "keys";
}
