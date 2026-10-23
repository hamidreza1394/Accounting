namespace Infrastructure.DatabaseSetup;

public enum DatabaseSetupStage
{
    Ready,
    Validating,
    TestingConnection,
    Connected,
    CreatingDatabase,
    RunningMigrations,
    SavingSettings,
    Completed,
    Failed,
}

public enum DatabaseSetupErrorCode
{
    None,
    ValidationFailed,
    LoginFailed,
    WindowsAuthNotTrusted,
    LoginDisabledOrExpired,
    CertificateNotTrusted,
    ServerUnreachable,
    InstanceNotFound,
    Timeout,
    ConnectionClosed,
    DatabaseNoAccess,
    PermissionDenied,
    DatabaseFileError,
    DiskFull,
    DatabaseMissing,
    MigrationFailed,
    PendingMigrations,
    NotFaranoDatabase,
    DatabaseNewerThanApplication,
    SystemDatabaseNotAllowed,
    AlreadyConfigured,
    SettingsAccessDenied,
    SettingsIoError,
    SettingsProtectionFailed,
    SetupInProgress,
    Unknown,
}

public enum DatabaseSetupDatabaseState
{
    Missing,
    Empty,
    UpToDate,
    PartiallyMigrated,
    ForeignOrLegacy,
    NewerThanApplication,
}

public enum DatabaseSetupStageStatus
{
    Succeeded,
    Skipped,
    Failed,
}

public sealed record DatabaseSetupStageOutcome(DatabaseSetupStage Stage, DatabaseSetupStageStatus Status, string? Detail = null);

/// <summary>Input of the setup. It holds a password, so it never prints its members.</summary>
public sealed class DatabaseSetupRequest
{
    public string? Server { get; init; }
    public string? Database { get; init; } = FaranoSettingsNames.Name;
    public string? AuthenticationMode { get; init; } = "Windows";

    /// <summary>Required only for SQL authentication.</summary>
    public string? UserName { get; init; }

    /// <summary>Required only for SQL authentication.</summary>
    public string? Password { get; init; }

    public bool TrustServerCertificate { get; init; }
    public bool CreateDatabaseIfMissing { get; init; } = true;

    /// <summary>Allows applying the missing migrations to a Farano database that is behind.</summary>
    public bool ContinuePartialMigrations { get; init; }

    public override string ToString() => nameof(DatabaseSetupRequest);
}

public sealed record DatabaseSetupResult
{
    public bool Succeeded { get; init; }
    public bool MigrationsApplied { get; init; }
    public bool DatabaseCreated { get; init; }
    public DatabaseSetupDatabaseState? DatabaseState { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Remedy { get; init; }
    public DatabaseSetupErrorCode ErrorCode { get; init; }
    public DatabaseSetupStage? FailedStage { get; init; }
    public string? TrackingId { get; init; }
    public IReadOnlyList<DatabaseSetupStageOutcome> Stages { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; init; } =
        new Dictionary<string, string[]>();
}
