namespace Infrastructure.DatabaseSetup;

public enum DatabaseSettingsStatus
{
    NotConfigured,
    Loaded,
    LoadedFromLegacy,
    MigratedFromLegacy,
    Conflict,
    Corrupt,
    Undecryptable,
    UnsupportedVersion,
    AccessDenied,
    LegacyUnreadable,
}

/// <summary>Machine-readable warning codes of a settings load (no free text, no secrets).</summary>
public static class DatabaseSettingsWarnings
{
    public const string LegacyUnreadable = "LegacyUnreadable";
    public const string LegacyConflict = "LegacyConflict";
    public const string LegacyMigrationPending = "LegacyMigrationPending";
    public const string NewFolderNotWritable = "NewFolderNotWritable";
    public const string MarkerNotWritten = "MarkerNotWritten";
    public const string WindowsAclNotApplied = "WindowsAclNotApplied";
    public const string SetupTokenNotDeleted = "SetupTokenNotDeleted";
}

/// <summary>Typed outcome of loading the protected connection settings.</summary>
public sealed class DatabaseSettingsLoadResult
{
    public required DatabaseSettingsStatus Status { get; init; }

    /// <summary>Only set for Loaded, LoadedFromLegacy, MigratedFromLegacy and Conflict.</summary>
    public string? ConnectionString { get; init; }

    public required string SettingsDirectory { get; init; }
    public IReadOnlyList<string> WarningCodes { get; init; } = [];

    public bool HasConnectionString => !string.IsNullOrWhiteSpace(ConnectionString);

    /// <summary>A file exists but cannot be used; replacing it needs an explicit confirmation.</summary>
    public bool RequiresReplaceConfirmation =>
        Status is DatabaseSettingsStatus.Corrupt or DatabaseSettingsStatus.Undecryptable or DatabaseSettingsStatus.LegacyUnreadable;

    /// <summary>The setup form must not be offered (the operator has to fix the folder or the version).</summary>
    public bool IsBlocked => Status is DatabaseSettingsStatus.AccessDenied or DatabaseSettingsStatus.UnsupportedVersion;

    public override string ToString() =>
        $"Status={Status}; Directory={SettingsDirectory}; Warnings=[{string.Join(",", WarningCodes)}]";
}
