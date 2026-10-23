using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.DatabaseSetup;

/// <summary>
/// Tests the connection, creates or migrates the database and stores the protected connection
/// string. Register as a singleton: one setup runs at a time. It never drops or deletes a database
/// and passes no file paths to CREATE DATABASE (the database name allowlist protects the derived file names).
/// </summary>
public sealed class DatabaseConnectionSetupService
{
    private static readonly string[] SystemDatabases = ["master", "model", "msdb", "tempdb"];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly DatabaseConnectionSettingsStore _settingsStore;
    private readonly ILogger<DatabaseConnectionSetupService> _logger;

    public DatabaseConnectionSetupService(
        DatabaseConnectionSettingsStore settingsStore,
        ILogger<DatabaseConnectionSetupService> logger)
    {
        _settingsStore = settingsStore;
        _logger = logger;
    }

    // ---------------------------------------------------------------- validation

    /// <summary>All independent errors at once; keys are field names. Empty means valid.</summary>
    public static IReadOnlyDictionary<string, string[]> Validate(DatabaseSetupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new Dictionary<string, List<string>>();

        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list))
            {
                errors[field] = list = [];
            }

            list.Add(message);
        }

        var server = request.Server?.Trim();
        if (string.IsNullOrEmpty(server))
        {
            Add("Server", "نام یا نشانی سرور الزامی است. مثال: localhost یا localhost\\SQLEXPRESS یا 192.168.1.10,1433");
        }
        else
        {
            if (server.Length > 256)
            {
                Add("Server", "نام سرور حداکثر ۲۵۶ نویسه است؛ آن را کوتاه کنید.");
            }

            if (server.Any(char.IsControl))
            {
                Add("Server", "نام سرور نباید نویسه کنترلی (مثل Tab یا خط جدید) داشته باشد؛ آن را دوباره تایپ کنید.");
            }
        }

        var database = request.Database?.Trim();
        if (string.IsNullOrEmpty(database))
        {
            Add("Database", "نام دیتابیس الزامی است. مثال: Farano");
        }
        else
        {
            if (database.Length > MaximumDatabaseNameLength)
            {
                Add("Database", "نام دیتابیس حداکثر ۱۲۳ نویسه است (نام فایل لاگ هم از روی آن ساخته می‌شود)؛ آن را کوتاه کنید.");
            }

            if (!HasOnlyAllowedDatabaseNameCharacters(database))
            {
                Add("Database", "نام دیتابیس فقط می‌تواند حروف (فارسی و لاتین)، رقم، زیرخط (_)، خط تیره (-)، نقطهٔ میانی و فاصلهٔ عادی در میانهٔ نام داشته باشد؛ نویسه‌های مسیر و نقل‌قول مثل \\ / : * ? \" < > | ; ' [ ]، نویسهٔ نامرئی، نقطه یا فاصله در ابتدا/انتها و «..» مجاز نیست.");
            }
            else if (IsSystemDatabaseName(database))
            {
                Add("Database", "انتخاب دیتابیس‌های سیستمی SQL Server (master، model، msdb، tempdb) مجاز نیست؛ نام دیگری مثل Farano بدهید.");
            }
        }

        var isWindows = string.Equals(request.AuthenticationMode, "Windows", StringComparison.OrdinalIgnoreCase);
        var isSql = string.Equals(request.AuthenticationMode, "Sql", StringComparison.OrdinalIgnoreCase);
        if (!isWindows && !isSql)
        {
            Add("AuthenticationMode", "روش احراز هویت باید «Windows» یا «Sql» باشد؛ یکی از گزینه‌های فهرست را انتخاب کنید.");
        }

        if (isSql)
        {
            var userName = request.UserName?.Trim();
            if (string.IsNullOrEmpty(userName))
            {
                Add("UserName", "برای ورود با SQL Server نام کاربری لازم است؛ آن را وارد کنید یا روش Windows را انتخاب کنید.");
            }
            else
            {
                if (userName.Length > 128)
                {
                    Add("UserName", "نام کاربری حداکثر ۱۲۸ نویسه است.");
                }

                if (userName.Any(char.IsControl))
                {
                    Add("UserName", "نام کاربری نباید نویسه کنترلی داشته باشد.");
                }
            }

            if (string.IsNullOrEmpty(request.Password))
            {
                Add("Password", "برای ورود با SQL Server رمز عبور لازم است؛ آن را وارد کنید یا روش Windows را انتخاب کنید.");
            }
            else if (request.Password.Length > 256)
            {
                Add("Password", "رمز عبور حداکثر ۲۵۶ نویسه است.");
            }
        }

        return errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    }

    /// <summary>Longest accepted database name: "&lt;name&gt;_log" must still fit the 128-character limit.</summary>
    public const int MaximumDatabaseNameLength = 123;

    /// <summary>
    /// A real allowlist: SQL Server derives the default data and log file names (.mdf, _log.ldf) from
    /// the database name, so the name must be a safe file name. Letters, marks, numbers, "_" and "-";
    /// "." and an ordinary space only inside the name (not first or last, no "..").
    /// </summary>
    internal static bool HasOnlyAllowedDatabaseNameCharacters(string value)
    {
        if (value.Length == 0 || value.StartsWith('.') || value.EndsWith('.') || value.Contains("..", StringComparison.Ordinal)
            || value.StartsWith(' ') || value.EndsWith(' '))
        {
            return false;
        }

        foreach (var rune in value.EnumerateRunes())
        {
            switch (Rune.GetUnicodeCategory(rune))
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.EnclosingMark:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.LetterNumber:
                case UnicodeCategory.OtherNumber:
                    continue;
            }

            if (rune.Value is '_' or '-' or '.' or ' ')
            {
                continue;
            }

            return false; // includes a lone surrogate, which enumerates as U+FFFD
        }

        return true;
    }

    /// <summary>
    /// The blocklist is checked on the NFKC-normalized name: SQL Server collations fold width
    /// variants (fullwidth "ｍａｓｔｅｒ") onto the system databases.
    /// </summary>
    internal static bool IsSystemDatabaseName(string name)
    {
        string normalized;
        try
        {
            normalized = name.Normalize(NormalizationForm.FormKC).Trim();
        }
        catch (ArgumentException)
        {
            // Invalid surrogates are already reported as "invalid characters"; not a system name.
            return false;
        }

        return SystemDatabases.Contains(normalized, StringComparer.OrdinalIgnoreCase)
            || SystemDatabases.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Pure decision about an existing database from its migration history and tables.</summary>
    public static DatabaseSetupDatabaseState ClassifyDatabaseState(
        bool historyTableExists,
        int userTableCount,
        IReadOnlyCollection<string> appliedMigrations,
        IReadOnlyCollection<string> knownMigrations)
    {
        if (!historyTableExists || appliedMigrations.Count == 0)
        {
            return userTableCount == 0 ? DatabaseSetupDatabaseState.Empty : DatabaseSetupDatabaseState.ForeignOrLegacy;
        }

        var known = new HashSet<string>(knownMigrations, StringComparer.Ordinal);
        if (appliedMigrations.Any(id => !known.Contains(id)))
        {
            // Some of ours plus unknown ones: a newer Farano. No overlap at all: somebody else's EF history.
            return appliedMigrations.Any(known.Contains)
                ? DatabaseSetupDatabaseState.NewerThanApplication
                : DatabaseSetupDatabaseState.ForeignOrLegacy;
        }

        return known.IsSubsetOf(appliedMigrations)
            ? DatabaseSetupDatabaseState.UpToDate
            : DatabaseSetupDatabaseState.PartiallyMigrated;
    }

    // ---------------------------------------------------------------- setup

    /// <param name="cancellationToken">
    /// Cancels only the connection test. Once the connection works, creating the database, running
    /// migrations and saving the settings are never interrupted half-way by a closed browser tab.
    /// </param>
    /// <param name="progress">Optional synchronous observer of finished stages (used by tests).</param>
    public async Task<DatabaseSetupResult> ConfigureAsync(
        DatabaseSetupRequest request,
        CancellationToken cancellationToken = default,
        IProgress<DatabaseSetupStageOutcome>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var trackingId = System.Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        var stages = new List<DatabaseSetupStageOutcome>();

        void Record(DatabaseSetupStage stage, DatabaseSetupStageStatus status, string? detail = null)
        {
            // One entry per stage: a failure replaces the success that was recorded for the same stage.
            stages.RemoveAll(existing => existing.Stage == stage);
            var outcome = new DatabaseSetupStageOutcome(stage, status, detail);
            stages.Add(outcome);
            progress?.Report(outcome);
        }

        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return Fail(DatabaseSetupErrorCode.SetupInProgress, DatabaseSetupStage.Ready, trackingId, stages, null);
        }

        var stage = DatabaseSetupStage.Validating;
        try
        {
            var errors = Validate(request);
            if (errors.Count > 0)
            {
                Record(DatabaseSetupStage.Validating, DatabaseSetupStageStatus.Failed);
                return Fail(DatabaseSetupErrorCode.ValidationFailed, stage, trackingId, stages, null, errors);
            }

            Record(DatabaseSetupStage.Validating, DatabaseSetupStageStatus.Succeeded);

            // Single-use gate: the one-time token must still be outstanding and the settings must be
            // in the state the start-up saw (a valid file that appeared meanwhile is never overwritten).
            if (_settingsStore.SetupAccessToken is null || SettingsStateChanged())
            {
                return Fail(DatabaseSetupErrorCode.AlreadyConfigured, DatabaseSetupStage.Validating, trackingId, stages, null);
            }

            var databaseName = request.Database!.Trim();
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = request.Server!.Trim(),
                InitialCatalog = "master",
                IntegratedSecurity = string.Equals(request.AuthenticationMode, "Windows", StringComparison.OrdinalIgnoreCase),
                Encrypt = SqlConnectionEncryptOption.Mandatory,
                TrustServerCertificate = request.TrustServerCertificate,
                ConnectTimeout = 15,
                MultipleActiveResultSets = true,
                PersistSecurityInfo = false,
                ApplicationName = FaranoSettingsNames.ApplicationName,
            };

            if (!builder.IntegratedSecurity)
            {
                builder.UserID = request.UserName!.Trim();
                builder.Password = request.Password;
            }

            stage = DatabaseSetupStage.TestingConnection;
            var databaseCreated = false;

            await using (var master = new SqlConnection(builder.ConnectionString))
            {
                await master.OpenAsync(cancellationToken);
                Record(DatabaseSetupStage.TestingConnection, DatabaseSetupStageStatus.Succeeded);
                stage = DatabaseSetupStage.Connected;

                await using var check = new SqlCommand("SELECT DB_ID(@databaseName);", master);
                check.Parameters.Add(new SqlParameter("@databaseName", SqlDbType.NVarChar, 128) { Value = databaseName });
                var id = await check.ExecuteScalarAsync(cancellationToken);
                Record(DatabaseSetupStage.Connected, DatabaseSetupStageStatus.Succeeded);

                // From here on the work is not tied to the request: see the parameter remarks.
                var work = CancellationToken.None;

                // Authoritative, server-side: whatever the name looks like, SQL Server decides. Ids 1..4 are
                // master, tempdb, model and msdb; look-alike names that collate onto them are refused.
                if (id is not null and not DBNull && System.Convert.ToInt32(id, CultureInfo.InvariantCulture) is >= 1 and <= 4)
                {
                    return Fail(DatabaseSetupErrorCode.SystemDatabaseNotAllowed, stage, trackingId, stages, null);
                }

                if (id is null or DBNull)
                {
                    if (!request.CreateDatabaseIfMissing)
                    {
                        return Fail(DatabaseSetupErrorCode.DatabaseMissing, stage, trackingId, stages, null);
                    }

                    stage = DatabaseSetupStage.CreatingDatabase;
                    try
                    {
                        // QUOTENAME quotes the identifier on the server and the name is a SQL parameter. No file
                        // path is passed, but SQL Server derives its default file names (.mdf, _log.ldf) from the
                        // name, so the name allowlist in Validate is the control that keeps them safe.
                        await using var create = new SqlCommand(
                            "DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@databaseName); EXEC sys.sp_executesql @sql;",
                            master);
                        create.Parameters.Add(new SqlParameter("@databaseName", SqlDbType.NVarChar, 128) { Value = databaseName });
                        create.CommandTimeout = 120;
                        await create.ExecuteNonQueryAsync(work);
                        databaseCreated = true;
                    }
                    catch (SqlException exception) when (exception.Number == 1801)
                    {
                        // Somebody created it between our check and our CREATE: not an error; inspect it below.
                    }

                    Record(DatabaseSetupStage.CreatingDatabase, DatabaseSetupStageStatus.Succeeded,
                        databaseCreated ? "Created" : "AlreadyExisted");
                }
                else
                {
                    Record(DatabaseSetupStage.CreatingDatabase, DatabaseSetupStageStatus.Skipped, "Exists");
                }
            }

            builder.InitialCatalog = databaseName;
            var databaseConnectionString = builder.ConnectionString;

            stage = DatabaseSetupStage.Connected;
            var (databaseId, state) = await InspectDatabaseAsync(databaseConnectionString, databaseCreated, CancellationToken.None);
            if (databaseId <= 4)
            {
                // Connected to a system database after all: touch nothing.
                return Fail(DatabaseSetupErrorCode.SystemDatabaseNotAllowed, stage, trackingId, stages, null);
            }

            if (state == DatabaseSetupDatabaseState.ForeignOrLegacy)
            {
                Record(DatabaseSetupStage.RunningMigrations, DatabaseSetupStageStatus.Skipped, state.ToString());
                return Fail(DatabaseSetupErrorCode.NotFaranoDatabase, DatabaseSetupStage.Connected, trackingId, stages, state);
            }

            if (state == DatabaseSetupDatabaseState.NewerThanApplication)
            {
                Record(DatabaseSetupStage.RunningMigrations, DatabaseSetupStageStatus.Skipped, state.ToString());
                return Fail(DatabaseSetupErrorCode.DatabaseNewerThanApplication, DatabaseSetupStage.Connected, trackingId, stages, state);
            }

            var migrationsApplied = false;
            if (state == DatabaseSetupDatabaseState.PartiallyMigrated && !request.ContinuePartialMigrations)
            {
                Record(DatabaseSetupStage.RunningMigrations, DatabaseSetupStageStatus.Skipped, state.ToString());
                return Fail(DatabaseSetupErrorCode.PendingMigrations, DatabaseSetupStage.RunningMigrations, trackingId, stages, state);
            }

            if (state is DatabaseSetupDatabaseState.Empty or DatabaseSetupDatabaseState.PartiallyMigrated)
            {
                stage = DatabaseSetupStage.RunningMigrations;
                await using var context = CreateContext(databaseConnectionString);
                await context.Database.MigrateAsync(CancellationToken.None);
                migrationsApplied = true;
                Record(DatabaseSetupStage.RunningMigrations, DatabaseSetupStageStatus.Succeeded);
            }
            else
            {
                Record(DatabaseSetupStage.RunningMigrations, DatabaseSetupStageStatus.Skipped, "UpToDate");
            }

            stage = DatabaseSetupStage.SavingSettings;
            _settingsStore.Save(databaseConnectionString, expectedStatus: _settingsStore.StartupLoad?.Status);
            Record(DatabaseSetupStage.SavingSettings, DatabaseSetupStageStatus.Succeeded);
            Record(DatabaseSetupStage.Completed, DatabaseSetupStageStatus.Succeeded);

            var warnings = _settingsStore.DrainWarnings().Select(DatabaseSetupErrorCatalog.DescribeWarning).ToList();
            _logger.LogInformation(
                "Database setup completed. TrackingId={TrackingId}; Stage={Stage}; State={State}; MigrationsApplied={MigrationsApplied}; DatabaseCreated={DatabaseCreated}; WarningCount={WarningCount}.",
                trackingId, DatabaseSetupStage.Completed, state, migrationsApplied, databaseCreated, warnings.Count);

            return new DatabaseSetupResult
            {
                Succeeded = true,
                MigrationsApplied = migrationsApplied,
                DatabaseCreated = databaseCreated,
                DatabaseState = state,
                TrackingId = trackingId,
                Stages = stages,
                Warnings = warnings,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var sql = DatabaseSetupErrorCatalog.FindSqlException(exception);
            var code = DatabaseSetupErrorCatalog.Classify(stage, sql?.Number, exception);
            LogFailure(code, stage, sql, exception, trackingId);
            Record(stage, DatabaseSetupStageStatus.Failed);
            return Fail(code, stage, trackingId, stages, null);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>True when the files no longer match what the start-up loaded (e.g. a valid file appeared).</summary>
    private bool SettingsStateChanged()
    {
        var startup = _settingsStore.StartupLoad;
        return startup is not null && _settingsStore.Inspect().Status != startup.Status;
    }

    private static Data.DatabaseContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseSqlServer(
                connectionString,
                sqlServer => sqlServer
                    .MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName)
                    .CommandTimeout(300))
            .Options);

    private static async Task<(int DatabaseId, DatabaseSetupDatabaseState State)> InspectDatabaseAsync(
        string databaseConnectionString,
        bool justCreated,
        CancellationToken cancellationToken)
    {
        bool historyExists;
        int userTables;
        int databaseId;
        var applied = new List<string>();

        await using (var connection = new SqlConnection(databaseConnectionString))
        {
            await connection.OpenAsync(cancellationToken);

            // Verify where we really are before reading or changing anything.
            await using (var whoAmI = new SqlCommand("SELECT DB_ID();", connection))
            {
                databaseId = System.Convert.ToInt32(await whoAmI.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
            }

            if (databaseId <= 4)
            {
                return (databaseId, DatabaseSetupDatabaseState.ForeignOrLegacy);
            }

            if (justCreated)
            {
                return (databaseId, DatabaseSetupDatabaseState.Empty);
            }

            await using (var probe = new SqlCommand(
                "SELECT CASE WHEN OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL THEN 0 ELSE 1 END, " +
                "(SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0 AND name <> N'__EFMigrationsHistory');",
                connection))
            await using (var reader = await probe.ExecuteReaderAsync(cancellationToken))
            {
                await reader.ReadAsync(cancellationToken);
                historyExists = reader.GetInt32(0) == 1;
                userTables = reader.GetInt32(1);
            }

            if (historyExists)
            {
                await using var history = new SqlCommand("SELECT [MigrationId] FROM [dbo].[__EFMigrationsHistory];", connection);
                await using var rows = await history.ExecuteReaderAsync(cancellationToken);
                while (await rows.ReadAsync(cancellationToken))
                {
                    applied.Add(rows.GetString(0));
                }
            }
        }

        // The model is only used to list the migrations of this application; no connection is opened.
        await using var context = CreateContext(databaseConnectionString);
        return (databaseId, ClassifyDatabaseState(historyExists, userTables, applied, context.Database.GetMigrations().ToList()));
    }

    private DatabaseSetupResult Fail(
        DatabaseSetupErrorCode code,
        DatabaseSetupStage stage,
        string trackingId,
        List<DatabaseSetupStageOutcome> stages,
        DatabaseSetupDatabaseState? state,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
    {
        var (message, remedy) = DatabaseSetupErrorCatalog.Describe(code);
        if (code == DatabaseSetupErrorCode.Unknown)
        {
            message += " شناسه پیگیری: " + trackingId;
        }
        else if (code != DatabaseSetupErrorCode.ValidationFailed)
        {
            message += " (شناسه پیگیری: " + trackingId + ")";
        }

        if (code is not (DatabaseSetupErrorCode.Unknown or DatabaseSetupErrorCode.ValidationFailed))
        {
            _logger.LogWarning(
                "Database setup stopped. TrackingId={TrackingId}; Code={Code}; Stage={Stage}.", trackingId, code, stage);
        }

        return new DatabaseSetupResult
        {
            Succeeded = false,
            ErrorCode = code,
            FailedStage = stage,
            ErrorMessage = message,
            Remedy = remedy,
            TrackingId = trackingId,
            DatabaseState = state,
            Stages = stages,
            ValidationErrors = validationErrors ?? new Dictionary<string, string[]>(),
        };
    }

    private void LogFailure(
        DatabaseSetupErrorCode code,
        DatabaseSetupStage stage,
        SqlException? sql,
        Exception exception,
        string trackingId)
    {
        // Never the connection string, user name or password. SqlException messages do not contain them.
        if (code == DatabaseSetupErrorCode.Unknown)
        {
            _logger.LogError(
                exception,
                "Database setup failed. TrackingId={TrackingId}; Code={Code}; Stage={Stage}; SqlNumber={SqlNumber}; SqlState={SqlState}; SqlClass={SqlClass}; ExceptionType={ExceptionType}.",
                trackingId, code, stage, sql?.Number, sql?.State, sql?.Class, exception.GetType().FullName);
        }
        else
        {
            _logger.LogWarning(
                "Database setup failed. TrackingId={TrackingId}; Code={Code}; Stage={Stage}; SqlNumber={SqlNumber}; SqlState={SqlState}; SqlClass={SqlClass}; ExceptionType={ExceptionType}.",
                trackingId, code, stage, sql?.Number, sql?.State, sql?.Class, exception.GetType().FullName);
        }
    }
}
