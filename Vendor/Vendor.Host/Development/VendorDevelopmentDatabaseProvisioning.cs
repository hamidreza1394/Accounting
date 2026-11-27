using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Faranou.Vendor.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Faranou.Vendor.Host;

/// <summary>
/// The full seven-stage local provisioning flow behind the Development database-setup page:
/// validate and test a temporary privileged "setup" connection, inspect the server, create the
/// dedicated Vendor database when missing, apply the existing EF Core migrations, create a
/// least-privilege SQL runtime identity, and verify readiness through that runtime identity.
/// Mirrors the safety rules the customer-side first-run flow is verified against (identifier
/// validation, never DROP, error 1801 is not fatal) while staying Vendor-only: no customer
/// database is ever referenced, and nothing outside the target Vendor database is granted.
/// </summary>
internal static class VendorDevelopmentDatabaseProvisioning
{
    private const string RuntimeLoginDefaultName = "FaranouVendorRuntime";
    private const string VendorReaderRole = "db_datareader";
    private const string VendorWriterRole = "db_datawriter";
    private const string MigrationsHistoryTable = "__EFMigrationsHistory";
    private const int CreateDatabaseCommandTimeoutSeconds = 120;

    /// <summary>Fixed stage order shared with the setup page renderer.</summary>
    internal static readonly (string Key, string Title)[] Stages =
    [
        ("connection", "اعتبارسنجی اطلاعات اتصال"),
        ("test", "آزمون اتصال به سرور"),
        ("inspect", "بازرسی وضعیت سرور و دیتابیس"),
        ("createDatabase", "ایجاد دیتابیس Vendor در صورت نیاز"),
        ("migrate", "اعمال Migrationهای EF Core"),
        ("runtimeIdentity", "ایجاد هویت Runtime کم‌اختیار"),
        ("verify", "بررسی آمادگی با اتصال Runtime")
    ];

    // One provisioning pipeline at a time, like the customer-side first-run flow: a second
    // concurrent request fails fast instead of racing CREATE DATABASE or migrations.
    private static readonly SemaphoreSlim PipelineLock = new(1, 1);

    private sealed record SetupConnection(SqlConnectionStringBuilder Builder, string Database);

    private sealed record InspectionResult(
        string? ServerVersion,
        bool DatabaseExists,
        int UserTableCount,
        bool HistoryTableExists,
        bool OwnershipMarkerPresent,
        bool RuntimeLoginExists,
        IReadOnlyList<string> UnknownMigrations,
        int AppliedMigrationsCount,
        int PendingMigrationsCount);

    internal sealed record ProvisionInput(
        SqlConnectionStringBuilder SetupBuilder,
        string Database,
        string RuntimeUsername,
        string? RuntimePassword,
        bool GeneratedRuntimePassword);

    internal sealed record ProvisionOutcome(
        bool Success,
        IReadOnlyList<StageResult> StageResults,
        string? RuntimeUsername,
        string? GeneratedPassword,
        bool SavedToCredentialManager,
        string Message)
    {
        public static ProvisionOutcome Failed(List<StageResult> stageResults, string message) =>
            new(false, stageResults, null, null, false, message);
    }

    internal sealed record StageResult(string Key, string State, string Message);

    internal sealed record InspectOutcome(
        bool Success,
        IReadOnlyList<StageResult> StageResults,
        string? ServerVersion,
        bool DatabaseExists,
        int UserTableCount,
        bool HistoryTableExists,
        bool OwnershipMarkerPresent,
        bool RuntimeLoginExists,
        int AppliedMigrationsCount,
        int PendingMigrationsCount,
        string Message);

    public static async Task<InspectOutcome> InspectAsync(
        ProvisionRequest request,
        ILogger logger,
        IReadOnlyList<string> extraAllowedSqlHosts,
        CancellationToken cancellationToken)
    {
        var stages = PendingStages();
        SetStage(stages, "connection", "running", "در حال اعتبارسنجی…");

        if (!TryBuildInput(request, extraAllowedSqlHosts, stages, requireRuntimeIdentity: false, out var input, out var failure))
        {
            return new InspectOutcome(false, stages, null, false, 0, false, false, false, 0, 0, failure!);
        }

        SetStage(stages, "connection", "success", "اطلاعات اتصال معتبر است.");

        InspectionResult? inspection;
        var currentStage = "test";
        try
        {
            SetStage(stages, "test", "running", "در حال آزمون اتصال به سرور…");
            var serverVersion = await OpenServerConnectionAsync(input.SetupBuilder, async connection =>
            {
                var version = await ReadScalarAsync(connection, "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))", cancellationToken);
                return version as string;
            }, cancellationToken);
            if (serverVersion is null)
            {
                SetStage(stages, "test", "failed", "اتصال برقرار شد اما نسخهٔ سرور خوانده نشد.");
                return ToInspect(stages, "اتصال به سرور تأیید نشد.");
            }

            SetStage(stages, "test", "success", $"اتصال به SQL Server نسخهٔ {serverVersion} برقرار شد.");
            currentStage = "inspect";
            SetStage(stages, "inspect", "running", "در حال بازرسی دیتابیس…");
            inspection = await InspectServerAsync(input, cancellationToken);
            SetStage(stages, "inspect", "success", DescribeInspection(inspection));
        }
        catch (VendorDatabaseMismatchException exception)
        {
            logger.LogWarning(exception, "Vendor development database inspection refused a system database name.");
            SetStage(stages, currentStage, "failed", exception.Message);
            return ToInspect(stages, exception.Message);
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException or TimeoutException)
        {
            logger.LogWarning(exception, "Vendor development database inspection failed.");
            var safeMessage = DescribeSqlError(exception);
            SetStage(stages, currentStage, "failed", safeMessage);
            return ToInspect(stages, safeMessage);
        }

        return new InspectOutcome(
            true,
            stages,
            inspection!.ServerVersion,
            inspection.DatabaseExists,
            inspection.UserTableCount,
            inspection.HistoryTableExists,
            inspection.OwnershipMarkerPresent,
            inspection.RuntimeLoginExists,
            inspection.AppliedMigrationsCount,
            inspection.PendingMigrationsCount,
            inspection.DatabaseExists ? DescribeInspection(inspection) : "دیتابیس هنوز وجود ندارد؛ با «راه‌اندازی کامل» ساخته می‌شود.");
    }

    public static async Task<ProvisionOutcome> RunAsync(
        ProvisionRequest request,
        ILogger logger,
        bool canSaveConnection,
        IReadOnlyList<string> extraAllowedSqlHosts,
        CancellationToken cancellationToken)
    {
        if (!await PipelineLock.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return ProvisionOutcome.Failed(PendingStages(), "یک راه‌اندازی دیگر در حال اجراست؛ تا پایان آن صبر کنید.");
        }

        try
        {
            return await RunLockedAsync(request, logger, canSaveConnection, extraAllowedSqlHosts, cancellationToken);
        }
        finally
        {
            PipelineLock.Release();
        }
    }

    private static async Task<ProvisionOutcome> RunLockedAsync(
        ProvisionRequest request,
        ILogger logger,
        bool canSaveConnection,
        IReadOnlyList<string> extraAllowedSqlHosts,
        CancellationToken cancellationToken)
    {
        var stages = PendingStages();
        string? runtimeUsername = null;
        string? generatedPassword = null;
        var savedToCredentialManager = false;

        // ── Stage 1: validate the request and build the temporary setup connection. ──
        SetStage(stages, "connection", "running", "در حال اعتبارسنجی…");
        if (!TryBuildInput(request, extraAllowedSqlHosts, stages, requireRuntimeIdentity: true, out var input, out var failure))
        {
            return ProvisionOutcome.Failed(stages, failure!);
        }

        runtimeUsername = input.RuntimeUsername;
        if (input.GeneratedRuntimePassword)
        {
            generatedPassword = input.RuntimePassword;
        }

        SetStage(stages, "connection", "success", "اطلاعات اتصال معتبر است.");

        // ── Stage 2: prove the setup identity can reach the server. ──
        string? serverVersion;
        try
        {
            SetStage(stages, "test", "running", "در حال آزمون اتصال به سرور…");
            serverVersion = await OpenServerConnectionAsync(input.SetupBuilder, async connection =>
                await ReadScalarAsync(connection, "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))", cancellationToken) as string,
                cancellationToken);
            if (serverVersion is null)
            {
                SetStage(stages, "test", "failed", "اتصال برقرار شد اما نسخهٔ سرور خوانده نشد.");
                return ProvisionOutcome.Failed(stages, "اتصال به سرور تأیید نشد.");
            }

            SetStage(stages, "test", "success", $"اتصال به SQL Server نسخهٔ {serverVersion} برقرار شد.");
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException or TimeoutException)
        {
            logger.LogWarning(exception, "Vendor development provisioning failed while testing the setup connection.");
            SetStage(stages, "test", "failed", DescribeSqlError(exception));
            return ProvisionOutcome.Failed(stages, DescribeSqlError(exception));
        }

        // ── Stage 3: inspect the server, the target database and the runtime login. ──
        InspectionResult inspection;
        try
        {
            SetStage(stages, "inspect", "running", "در حال بازرسی دیتابیس…");
            inspection = await InspectServerAsync(input, cancellationToken);
            SetStage(stages, "inspect", "success", DescribeInspection(inspection));
        }
        catch (VendorDatabaseMismatchException exception)
        {
            logger.LogWarning(exception, "Vendor development provisioning refused a system database name.");
            SetStage(stages, "inspect", "failed", exception.Message);
            return ProvisionOutcome.Failed(stages, exception.Message);
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException or TimeoutException)
        {
            logger.LogWarning(exception, "Vendor development provisioning failed while inspecting the server.");
            SetStage(stages, "inspect", "failed", DescribeSqlError(exception));
            return ProvisionOutcome.Failed(stages, DescribeSqlError(exception));
        }

        // ── Stage 4: create the dedicated database when missing; never drop or rebuild. ──
        var markerEnsured = false;
        if (inspection.DatabaseExists)
        {
            SetStage(stages, "createDatabase", "skipped", "دیتابیس از قبل وجود دارد؛ بدون تغییر ادامه می‌دهیم.");
        }
        else
        {
            try
            {
                SetStage(stages, "createDatabase", "running", "در حال ایجاد دیتابیس…");
                // CREATE DATABASE cannot be parameterized; the name passed the identifier
                // validation (stage 1) and is bracket-quoted here. A browser closing mid-run
                // must not leave a half-started create, so this runs to completion.
                await OpenServerConnectionAsync(input.SetupBuilder, async connection =>
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = $"CREATE DATABASE {QuoteIdentifier(input.Database)}";
                    command.CommandTimeout = CreateDatabaseCommandTimeoutSeconds;
                    await command.ExecuteNonQueryAsync(CancellationToken.None);
                    return true;
                }, CancellationToken.None);
                // Prove ownership immediately: without the marker a freshly created database
                // would be indistinguishable from an empty database created for another purpose.
                await OpenServerConnectionAsync(TargetBuilder(input), async connection =>
                {
                    await VendorOwnershipMarker.WriteAsync(connection, CancellationToken.None);
                    return true;
                }, CancellationToken.None);
                markerEnsured = true;
                SetStage(stages, "createDatabase", "success", $"دیتابیس {input.Database} ساخته شد و نشانهٔ مالکیت Vendor روی آن ثبت شد.");
            }
            catch (SqlException exception) when (exception.Number == 1801)
            {
                // A concurrent CREATE DATABASE is not an error, but an existing database we
                // cannot prove ownership of must not be adopted; the ownership rules decide.
                try
                {
                    inspection = await InspectServerAsync(input, CancellationToken.None);
                }
                catch (Exception inspectException) when (inspectException is SqlException or InvalidOperationException or TimeoutException)
                {
                    logger.LogWarning(inspectException, "Vendor development provisioning could not re-inspect after a concurrent database create.");
                    SetStage(stages, "createDatabase", "failed", "وضعیت دیتابیس پس از ساخت هم‌زمان خوانده نشد؛ دوباره تلاش کنید.");
                    return ProvisionOutcome.Failed(stages, DescribeSqlError(inspectException));
                }

                if (inspection.DatabaseExists)
                {
                    var (concurrentAllowed, concurrentRefusal) = VendorOwnershipMarker.EvaluateOwnership(
                        inspection.OwnershipMarkerPresent,
                        inspection.HistoryTableExists,
                        inspection.AppliedMigrationsCount,
                        inspection.UnknownMigrations.Count,
                        inspection.UserTableCount);
                    if (concurrentAllowed)
                    {
                        SetStage(stages, "createDatabase", "skipped", "دیتابیس هم‌زمان توسط فرایند دیگری ساخته شد؛ مالکیت آن تأیید شد و همان استفاده می‌شود.");
                    }
                    else
                    {
                        SetStage(stages, "createDatabase", "failed", "دیتابیس هم‌زمان ساخته شد اما مالکیت Vendor روی آن قابل اثبات نیست؛ به‌طور خودکار تصاحب نمی‌شود.");
                        return ProvisionOutcome.Failed(stages, concurrentRefusal!);
                    }
                }
                else
                {
                    SetStage(stages, "createDatabase", "failed", "دیتابیس هم‌زمان ساخته شد اما اکنون پیدا نمی‌شود؛ دوباره تلاش کنید.");
                    return ProvisionOutcome.Failed(stages, "وضعیت دیتابیس پس از ساخت هم‌زمان نامشخص است.");
                }
            }
            catch (Exception exception) when (exception is SqlException or InvalidOperationException or TimeoutException)
            {
                logger.LogWarning(exception, "Vendor development provisioning failed while creating the database or writing its ownership marker.");
                SetStage(stages, "createDatabase", "failed", DescribeSqlError(exception));
                return ProvisionOutcome.Failed(stages, DescribeSqlError(exception));
            }
        }

        // ── Stage 5: apply the existing EF Core migrations with the setup identity. ──
        var appliedNow = 0;
        try
        {
            SetStage(stages, "migrate", "running", "در حال اعمال Migrationها…");
            appliedNow = await ApplyMigrationsAsync(input, logger, inspection, markerEnsured);
            SetStage(stages, "migrate", "success", appliedNow == 0
                ? "همهٔ Migrationها از قبل اعمال شده بودند؛ تغییر جدیدی لازم نبود."
                : $"{appliedNow} Migration با موفقیت اعمال شد.");
        }
        catch (VendorDatabaseMismatchException exception)
        {
            // Safety guard: never migrate into a database that is not a Vendor database.
            logger.LogWarning(exception, "Vendor development provisioning refused to migrate a foreign database.");
            SetStage(stages, "migrate", "failed", exception.Message);
            return ProvisionOutcome.Failed(stages, exception.Message);
        }
        catch (Exception exception) when (exception is SqlException or DbUpdateException or InvalidOperationException or TimeoutException)
        {
            logger.LogWarning(exception, "Vendor development provisioning failed while applying migrations.");
            SetStage(stages, "migrate", "failed", "اعمال Migrationها ناموفق بود؛ وضعیت دیتابیس و دسترسی هویت پیکربندی بررسی شود.");
            return ProvisionOutcome.Failed(stages, DescribeSqlError(exception));
        }

        // ── Stage 6: ensure the least-privilege SQL runtime identity, granted only here. ──
        try
        {
            SetStage(stages, "runtimeIdentity", "running", "در حال تنظیم هویت Runtime…");
            await EnsureRuntimeIdentityAsync(input, logger);
            SetStage(stages, "runtimeIdentity", "success",
                $"هویت Runtime کم‌اختیار «{input.RuntimeUsername}» آماده است؛ دسترسی فقط روی همین دیتابیس (خواندن/نوشتن) و هیچ دیتابیس دیگری.");
        }
        catch (VendorDatabaseMismatchException exception)
        {
            // Safety guard: never reuse an over-privileged login as the runtime identity.
            logger.LogWarning(exception, "Vendor development provisioning refused an over-privileged runtime login.");
            SetStage(stages, "runtimeIdentity", "failed", exception.Message);
            return ProvisionOutcome.Failed(stages, exception.Message);
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException or TimeoutException)
        {
            logger.LogWarning(exception, "Vendor development provisioning failed while configuring the runtime identity.");
            SetStage(stages, "runtimeIdentity", "failed", DescribeSqlError(exception));
            return ProvisionOutcome.Failed(stages, DescribeSqlError(exception));
        }

        // ── Stage 7: verify readiness through the runtime identity itself. ──
        var runtimeConnectionString = BuildRuntimeConnectionString(input);
        try
        {
            SetStage(stages, "verify", "running", "در حال بررسی آمادگی با اتصال Runtime…");
            await VerifyReadinessAsync(input, runtimeConnectionString);
            SetStage(stages, "verify", "success", "اتصال Runtime تأیید شد: دیتابیس، هویت و Schema آماده‌اند.");
        }
        catch (Exception exception) when (exception is SqlException or DbUpdateException or InvalidOperationException or TimeoutException)
        {
            logger.LogWarning(exception, "Vendor development provisioning failed while verifying the runtime connection.");
            SetStage(stages, "verify", "failed", "اتصال Runtime آماده نیست؛ گذرواژه و نقش‌های هویت Runtime بررسی شود.");
            return ProvisionOutcome.Failed(stages, "بررسی آمادگی با اتصال Runtime ناموفق بود.");
        }

        // ── Persist the runtime (least-privilege) connection for the next Host run. ──
        var saveFailed = false;
        if (canSaveConnection)
        {
            try
            {
                VendorDevelopmentDatabaseSetup.WriteSavedConnection(runtimeConnectionString);
                savedToCredentialManager = true;
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                // The exception carries only the Win32 error; never log the connection string.
                logger.LogError(exception, "Saving the provisioned Vendor runtime connection to Windows Credential Manager failed.");
                saveFailed = true;
            }
        }

        var message = saveFailed
            ? "راه‌اندازی کامل شد اما ذخیرهٔ اتصال Runtime در Windows Credential Manager ناموفق بود؛ از دکمهٔ ذخیرهٔ صفحه استفاده کنید یا دوباره تلاش کنید."
            : savedToCredentialManager
                ? $"راه‌اندازی کامل شد. اتصال Runtime کم‌اختیار برای کاربر فعلی ذخیره شد؛ برای اعمال، Vendor را دوباره اجرا کنید.{(input.GeneratedRuntimePassword ? " گذرواژهٔ ساخته‌شدهٔ Runtime فقط همین یک‌بار نمایش داده می‌شود." : string.Empty)}"
                : "راه‌اندازی کامل شد؛ اما به‌دلیل اینکه تنظیم اتصال از Environment/Configuration می‌آید، چیزی ذخیره نشد. نام کاربری Runtime و (در صورت ساخته‌شدن) گذرواژهٔ آن را در همان منبع تنظیم کنید.";

        return new ProvisionOutcome(true, stages, runtimeUsername, generatedPassword, savedToCredentialManager, message);
    }

    internal static bool TryBuildInput(
        ProvisionRequest request,
        IReadOnlyList<string> extraAllowedSqlHosts,
        List<StageResult> stages,
        bool requireRuntimeIdentity,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ProvisionInput? input,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? failure)
    {
        input = null;

        if (request.Authentication is not ("windows" or "sql") ||
            string.IsNullOrWhiteSpace(request.Server) ||
            request.Server.Length > 256)
        {
            failure = "نام سرور یا روش ورود معتبر نیست.";
            SetStage(stages, "connection", "failed", failure);
            return false;
        }

        // The Development setup page is a local tool, not a SQL client for arbitrary hosts:
        // the target is restricted server-side to local instances plus the explicit
        // configuration allow-list, before any connection string or SQL is built.
        if (!VendorSqlServerTarget.IsAllowed(request.Server, extraAllowedSqlHosts, out var targetError))
        {
            failure = targetError;
            SetStage(stages, "connection", "failed", failure);
            return false;
        }

        if (!VendorDatabaseName.TryValidate(request.Database, out var database, out var databaseError))
        {
            failure = databaseError!;
            SetStage(stages, "connection", "failed", databaseError!);
            return false;
        }

        // SQL Server itself caps SQL login names and passwords at 128 characters (sysname);
        // rejecting here keeps every layer consistent instead of failing mid-pipeline.
        if (request.Username?.Length > 128 || request.Password?.Length > 128)
        {
            failure = "نام کاربری و گذرواژهٔ SQL حداکثر ۱۲۸ نویسه می‌توانند باشند.";
            SetStage(stages, "connection", "failed", failure);
            return false;
        }

        if (request.RuntimeUsername?.Length > 128 ||
            request.RuntimePassword?.Length > 128 ||
            request.RuntimePassword is { Length: > 0 and < 8 })
        {
            failure = "نام کاربری Runtime حداکثر ۱۲۸ نویسه و گذرواژهٔ Runtime (در صورت واردکردن) باید ۸ تا ۱۲۸ نویسه باشد.";
            SetStage(stages, "connection", "failed", failure);
            return false;
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = request.Server.Trim(),
            // Admin operations run against the server catalog; the target database may not
            // exist yet. Every per-database command switches context explicitly.
            InitialCatalog = "master",
            IntegratedSecurity = string.Equals(request.Authentication, "windows", StringComparison.Ordinal),
            Encrypt = true,
            TrustServerCertificate = request.TrustServerCertificate,
            ConnectTimeout = 15,
            PersistSecurityInfo = false,
            ApplicationName = "Faranou Vendor Development Setup"
        };

        if (!builder.IntegratedSecurity)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                failure = "برای ورود SQL نام کاربری و گذرواژه لازم است.";
                SetStage(stages, "connection", "failed", failure);
                return false;
            }

            builder.UserID = request.Username.Trim();
            builder.Password = request.Password;
        }

        var runtimeUsername = string.IsNullOrWhiteSpace(request.RuntimeUsername)
            ? RuntimeLoginDefaultName
            : request.RuntimeUsername.Trim();
        if (!VendorRuntimeLoginName.TryValidate(runtimeUsername, out var loginError))
        {
            failure = loginError!;
            SetStage(stages, "connection", "failed", loginError!);
            return false;
        }

        string? runtimePassword = request.RuntimePassword;
        var generated = false;
        if (requireRuntimeIdentity && string.IsNullOrEmpty(runtimePassword))
        {
            runtimePassword = VendorRuntimePassword.Generate();
            generated = true;
        }
        else if (string.IsNullOrEmpty(runtimePassword))
        {
            // Inspection never needs the runtime password.
            runtimePassword = null;
        }

        input = new ProvisionInput(builder, database!, runtimeUsername, runtimePassword, generated);
        failure = null;
        return true;
    }

    private static async Task<T> OpenServerConnectionAsync<T>(
        SqlConnectionStringBuilder builder,
        Func<SqlConnection, Task<T>> work,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return await work(connection);
    }

    private static async Task<object?> ReadScalarAsync(
        SqlConnection connection,
        string commandText,
        CancellationToken cancellationToken,
        params SqlParameter[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<InspectionResult> InspectServerAsync(
        ProvisionInput input,
        CancellationToken cancellationToken)
    {
        var databaseExists = false;
        var runtimeLoginExists = false;
        var serverVersion = string.Empty;

        await OpenServerConnectionAsync(input.SetupBuilder, async connection =>
        {
            serverVersion = (await ReadScalarAsync(connection,
                "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))", cancellationToken) as string) ?? string.Empty;
            // Belt and braces against look-alike names: DB_ID resolves the homoglyph names to
            // the system database ids (1..4) even when string comparison misses them.
            var databaseId = await ReadScalarAsync(connection, "SELECT DB_ID(@name)", cancellationToken,
                new SqlParameter("@name", System.Data.SqlDbType.NVarChar, 128) { Value = input.Database });
            if (databaseId is int id && id is >= 1 and <= 4)
            {
                throw new VendorDatabaseMismatchException("نام انتخابی به یک دیتابیس سیستمی SQL Server اشاره می‌کند و مجاز نیست.");
            }

            databaseExists = databaseId is not null and not DBNull;
            var loginId = await ReadScalarAsync(connection,
                "SELECT CAST(principal_id AS int) FROM sys.server_principals WHERE name = @login", cancellationToken,
                new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
            runtimeLoginExists = loginId is int;
            return true;
        }, cancellationToken);

        if (!databaseExists)
        {
            return new InspectionResult(
                serverVersion, false, 0, false, false, runtimeLoginExists, [], 0, 0);
        }

        // The database exists: inspect it with the setup identity before touching anything.
        var targetBuilder = TargetBuilder(input);
        int userTableCount = 0;
        var historyTableExists = false;
        var ownershipMarkerPresent = false;
        var appliedIds = new List<string>();

        await OpenServerConnectionAsync(targetBuilder, async connection =>
        {
            var tables = await ReadScalarAsync(connection,
                "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0", cancellationToken);
            userTableCount = tables is int count ? count : 0;
            var history = await ReadScalarAsync(connection,
                "SELECT COUNT(*) FROM sys.tables WHERE name = @history", cancellationToken,
                new SqlParameter("@history", System.Data.SqlDbType.NVarChar, 128) { Value = MigrationsHistoryTable });
            historyTableExists = history is int h && h > 0;
            ownershipMarkerPresent = await VendorOwnershipMarker.ReadAsync(connection, cancellationToken);
            if (historyTableExists)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT MigrationId FROM dbo.__EFMigrationsHistory";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    appliedIds.Add(reader.GetString(0));
                }
            }

            return true;
        }, cancellationToken);

        var appliedCount = appliedIds.Count;
        var pendingCount = 0;
        var unknown = new List<string>();
        if (historyTableExists)
        {
            await using var context = CreateDbContext(targetBuilder.ConnectionString);
            var knownMigrations = context.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
            unknown.AddRange(appliedIds.Where(id => !knownMigrations.Contains(id)));
            pendingCount = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
        }

        return new InspectionResult(
            serverVersion, true, userTableCount, historyTableExists, ownershipMarkerPresent, runtimeLoginExists, unknown, appliedCount, pendingCount);
    }

    private static string DescribeInspection(InspectionResult inspection)
    {
        var loginState = inspection.RuntimeLoginExists
            ? "هویت Runtime از قبل وجود دارد"
            : "هویت Runtime ساخته خواهد شد";
        if (!inspection.DatabaseExists)
        {
            return $"دیتابیس وجود ندارد؛ با «راه‌اندازی کامل» ساخته می‌شود و {loginState}.";
        }

        var state = (inspection.OwnershipMarkerPresent, inspection.HistoryTableExists, inspection.UserTableCount) switch
        {
            (true, false, _) => "نشانهٔ مالکیت Vendor را دارد اما Migrationها هنوز روی آن اعمال نشده است",
            (false, false, > 0) => "جدول‌هایی دارد اما نه نشانهٔ مالکیت Vendor و نه تاریخچهٔ Migration فارنو در آن نیست (دیتابیس ناشناس)",
            (false, false, _) => "خالی است و هیچ نشانه‌ای از مالکیت Vendor ندارد؛ به‌طور خودکار تصاحب نمی‌شود",
            _ when inspection.UnknownMigrations.Count > 0 => $"شامل {inspection.UnknownMigrations.Count} Migration ناشناس است",
            _ when inspection.PendingMigrationsCount > 0 => $"دارای {inspection.PendingMigrationsCount} Migration معوق و {inspection.AppliedMigrationsCount} اعمال‌شده",
            _ => $"همهٔ {inspection.AppliedMigrationsCount} Migration روی آن اعمال شده"
        };

        return $"دیتابیس {state}؛ {loginState}.";
    }

    private static async Task<int> ApplyMigrationsAsync(
        ProvisionInput input,
        ILogger logger,
        InspectionResult inspection,
        bool markerEnsured)
    {
        var targetBuilder = TargetBuilder(input);

        await using var context = CreateDbContext(targetBuilder.ConnectionString);

        // Safety guards before any DDL, re-evaluated at execution time: a database we cannot
        // prove is ours is never migrated, and applied migration ids the assembly does not
        // know mean a newer application wrote to it.
        var connection = (SqlConnection)context.Database.GetDbConnection();
        await connection.OpenAsync();
        var historyExists = await ReadScalarAsync(connection,
            "SELECT COUNT(*) FROM sys.tables WHERE name = @history",
            CancellationToken.None,
            new SqlParameter("@history", System.Data.SqlDbType.NVarChar, 128) { Value = MigrationsHistoryTable }) is int h && h > 0;
        var userTables = await ReadScalarAsync(connection,
            "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0", CancellationToken.None);
        var userTableCount = userTables is int c && c > 0 ? c : 0;
        var appliedIds = new List<string>();
        if (historyExists)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT MigrationId FROM dbo.__EFMigrationsHistory";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                appliedIds.Add(reader.GetString(0));
            }
        }

        var knownMigrations = context.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
        var unknownCount = appliedIds.Count(id => !knownMigrations.Contains(id));
        // The marker was ensured for databases this run created; otherwise trust only what the
        // database itself can prove right now.
        var hasMarker = markerEnsured || inspection.OwnershipMarkerPresent ||
                        await VendorOwnershipMarker.ReadAsync(connection, CancellationToken.None);
        var (ownershipAllowed, ownershipRefusal) = VendorOwnershipMarker.EvaluateOwnership(
            hasMarker, historyExists, appliedIds.Count, unknownCount, userTableCount);
        if (!ownershipAllowed)
        {
            throw new VendorDatabaseMismatchException(ownershipRefusal!);
        }

        var appliedBefore = appliedIds.Count;
        // Cancellation must not stop the pipeline mid-migration; the browser closing is not a
        // reason to leave a partially applied schema.
        await context.Database.MigrateAsync(CancellationToken.None);
        await using var appliedCommand = connection.CreateCommand();
        appliedCommand.CommandText = "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory";
        var appliedResult = await appliedCommand.ExecuteScalarAsync(CancellationToken.None);
        var appliedAfter = appliedResult is int appliedCount ? appliedCount : appliedBefore;
        if (appliedAfter > appliedBefore)
        {
            logger.LogInformation("Vendor development provisioning applied {AppliedCount} migrations.", appliedAfter - appliedBefore);
        }

        if (!hasMarker)
        {
            // Pre-marker Vendor database proven by its own migration history: record the
            // marker so future runs rely on one consistent ownership proof. History remains
            // sufficient proof on its own, so a marker write failure is not fatal here.
            try
            {
                await VendorOwnershipMarker.WriteAsync(connection, CancellationToken.None);
            }
            catch (Exception exception) when (exception is SqlException or InvalidOperationException or TimeoutException)
            {
                logger.LogWarning(exception, "Vendor development provisioning could not backfill the ownership marker; migration history remains the ownership proof.");
            }
        }

        context.ChangeTracker.Clear();
        return appliedAfter - appliedBefore;
    }

    private static async Task EnsureRuntimeIdentityAsync(ProvisionInput input, ILogger logger)
    {
        var runtimePassword = input.RuntimePassword!;
        var createdFresh = false;

        // Server level: create the login or reset its password to the requested one. The
        // operator is explicitly warned in the UI that an existing login of the same name has
        // its password reset; the default name is Vendor-specific to avoid such collisions.
        await OpenServerConnectionAsync(input.SetupBuilder, async connection =>
        {
            // When the login already exists it must not carry server-level power: a runtime
            // identity is least-privilege by definition, and a privileged login of the same
            // name is refused instead of silently reused.
            var existing = await ReadScalarAsync(connection,
                "SELECT CAST(principal_id AS int) FROM sys.server_principals WHERE name = @login AND type = 'S'",
                CancellationToken.None,
                new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
            var quotedLogin = QuoteIdentifier(input.RuntimeUsername);
            if (existing is int)
            {
                var roles = new List<string>();
                await using (var rolesCommand = connection.CreateCommand())
                {
                    rolesCommand.CommandText = """
                        SELECT r.name
                        FROM sys.server_role_members rm
                        JOIN sys.server_principals r ON r.principal_id = rm.role_principal_id
                        JOIN sys.server_principals m ON m.principal_id = rm.member_principal_id
                        WHERE m.name = @login
                        """;
                    rolesCommand.Parameters.Add(new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
                    await using var reader = await rolesCommand.ExecuteReaderAsync(CancellationToken.None);
                    while (await reader.ReadAsync(CancellationToken.None))
                    {
                        roles.Add(reader.GetString(0));
                    }
                }

                var grants = new List<string>();
                await using (var grantsCommand = connection.CreateCommand())
                {
                    grantsCommand.CommandText = """
                        SELECT pe.permission_name
                        FROM sys.server_permissions pe
                        JOIN sys.server_principals g ON g.principal_id = pe.grantee_principal_id
                        WHERE g.name = @login AND pe.state IN ('G', 'W')
                        """;
                    grantsCommand.Parameters.Add(new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
                    await using var reader = await grantsCommand.ExecuteReaderAsync(CancellationToken.None);
                    while (await reader.ReadAsync(CancellationToken.None))
                    {
                        grants.Add(reader.GetString(0));
                    }
                }

                if (VendorRuntimeIdentity.HasExcessiveServerAccess(roles, grants, out var serverDetail))
                {
                    throw new VendorDatabaseMismatchException($"هویت Runtime «{input.RuntimeUsername}» از قبل مجوزهای سطح سرور دارد ({serverDetail}) و به‌عنوان هویت کم‌اختیار قابل استفاده نیست؛ نام دیگری انتخاب کنید یا مجوزهای سرور را با تصمیم مدیر سرور اصلاح کنید.");
                }
            }

            await using var command = connection.CreateCommand();
            command.CommandText = existing is int
                ? $"ALTER LOGIN {quotedLogin} WITH PASSWORD = @password"
                : $"CREATE LOGIN {quotedLogin} WITH PASSWORD = @password, CHECK_POLICY = ON, DEFAULT_DATABASE = {QuoteIdentifier(input.Database)}";
            command.Parameters.Add(new SqlParameter("@password", System.Data.SqlDbType.NVarChar, 128) { Value = runtimePassword });
            await command.ExecuteNonQueryAsync(CancellationToken.None);
            createdFresh = existing is not int;
            if (createdFresh)
            {
                // Least visibility: without it the login could list every database name on the
                // instance. Databases it has no user in stay invisible and inaccessible anyway.
                await using var deny = connection.CreateCommand();
                deny.CommandText = $"DENY VIEW ANY DATABASE TO {quotedLogin}";
                await deny.ExecuteNonQueryAsync(CancellationToken.None);
            }

            var disabled = await ReadScalarAsync(connection,
                "SELECT is_disabled FROM sys.sql_logins WHERE name = @login",
                CancellationToken.None,
                new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
            if (disabled is bool isDisabled && isDisabled)
            {
                await using var enable = connection.CreateCommand();
                enable.CommandText = $"ALTER LOGIN {quotedLogin} ENABLE";
                await enable.ExecuteNonQueryAsync(CancellationToken.None);
            }

            logger.LogInformation("Vendor development runtime login ensured (fresh: {FreshLogin}).", createdFresh);
            return true;
        }, CancellationToken.None);

        // Database level: map the login to a user of the target database only, with read and
        // write roles. No db_owner, no other database, no server role is granted.
        var targetBuilder = TargetBuilder(input);
        await OpenServerConnectionAsync(targetBuilder, async connection =>
        {
            var userExists = await ReadScalarAsync(connection,
                "SELECT COUNT(*) FROM sys.database_principals WHERE name = @login AND type IN ('S', 'U')",
                CancellationToken.None,
                new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername }) is int u && u > 0;
            var quotedLogin = QuoteIdentifier(input.RuntimeUsername);
            if (!userExists)
            {
                await using var createUser = connection.CreateCommand();
                createUser.CommandText = $"CREATE USER {quotedLogin} FOR LOGIN {quotedLogin}";
                await createUser.ExecuteNonQueryAsync(CancellationToken.None);
            }

            foreach (var role in new[] { VendorReaderRole, VendorWriterRole })
            {
                // IS_ROLEMEMBER returns '1'/'0' as varchar(2); NULL means the role or login
                // could not be resolved, which still warrants adding the membership.
                var membership = await ReadScalarAsync(connection,
                    "SELECT IS_ROLEMEMBER(@role, @login)",
                    CancellationToken.None,
                    new SqlParameter("@role", System.Data.SqlDbType.NVarChar, 128) { Value = role },
                    new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
                if (!string.Equals(membership as string, "1", StringComparison.Ordinal))
                {
                    await using var addMember = connection.CreateCommand();
                    addMember.CommandText = $"ALTER ROLE {role} ADD MEMBER {quotedLogin}";
                    await addMember.ExecuteNonQueryAsync(CancellationToken.None);
                }
            }

            return true;
        }, CancellationToken.None);
    }

    private static async Task VerifyReadinessAsync(ProvisionInput input, string runtimeConnectionString)
    {
        string? actualDatabase = null;
        string? actualLogin = null;
        var isDbOwner = false;
        await OpenServerConnectionAsync(new SqlConnectionStringBuilder(runtimeConnectionString), async connection =>
        {
            actualDatabase = await ReadScalarAsync(connection, "SELECT DB_NAME()", CancellationToken.None) as string;
            actualLogin = await ReadScalarAsync(connection, "SELECT SUSER_SNAME()", CancellationToken.None) as string;
            // IS_ROLEMEMBER returns '1'/'0' as varchar(2) for the current user.
            var ownerMembership = await ReadScalarAsync(
                connection, "SELECT IS_ROLEMEMBER('db_owner')", CancellationToken.None) as string;
            isDbOwner = string.Equals(ownerMembership, "1", StringComparison.Ordinal);
            return true;
        }, CancellationToken.None);

        if (!string.Equals(actualDatabase, input.Database, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("دیتابیس پیش‌فرض اتصال Runtime با دیتابیس هدف مطابقت ندارد.");
        }

        if (!string.Equals(actualLogin, input.RuntimeUsername, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("هویت متصل با هویت Runtime مورد انتظار مطابقت ندارد.");
        }

        if (isDbOwner)
        {
            throw new InvalidOperationException("هویت Runtime نباید عضو db_owner باشد.");
        }

        // Schema check with the application model, through the runtime identity: pending
        // migrations must be zero after stage 5 succeeded.
        await using var context = CreateDbContext(runtimeConnectionString);
        var pending = (await context.Database.GetPendingMigrationsAsync(CancellationToken.None)).ToList();
        if (pending.Count > 0)
        {
            throw new InvalidOperationException("از دید اتصال Runtime، Migrationهای معوق باقی مانده است.");
        }
    }

    private static VendorDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>();
        options.UseSqlServer(
            connectionString,
            sql => sql.MigrationsAssembly(typeof(VendorDbContext).Assembly.FullName));
        return new VendorDbContext(options.Options);
    }

    private static SqlConnectionStringBuilder TargetBuilder(ProvisionInput input) =>
        new(input.SetupBuilder.ConnectionString)
        {
            InitialCatalog = input.Database
        };

    internal static string BuildRuntimeConnectionString(ProvisionInput input)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = input.SetupBuilder.DataSource,
            InitialCatalog = input.Database,
            IntegratedSecurity = false,
            UserID = input.RuntimeUsername,
            Password = input.RuntimePassword!,
            Encrypt = true,
            TrustServerCertificate = input.SetupBuilder.TrustServerCertificate,
            ConnectTimeout = 15,
            PersistSecurityInfo = false,
            ApplicationName = "Faranou Vendor Development Runtime"
        };
        return builder.ConnectionString;
    }

    private static string QuoteIdentifier(string identifier)
    {
        // The identifier passed validation (no ']'), but quote-escape anyway as defense in depth.
        return $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    private static string DescribeSqlError(Exception exception) => exception switch
    {
        SqlException { Number: 18456 } => "ورود به SQL Server رد شد؛ نام کاربری و گذرواژهٔ هویت پیکربندی را بررسی کنید.",
        SqlException { Number: 18452 } => "احراز هویت ویندوز از این نشست پذیرفته نشد.",
        SqlException { Number: 4060 or 916 } => "دیتابیس هدف در دسترس نیست؛ نام دیتابیس و دسترسی کاربر را بررسی کنید.",
        SqlException { Number: 262 or 229 or 15247 } => "دسترسی لازم برای این عملیات داده نشد؛ هویت پیکربندی باید دسترسی مدیریتی روی سرور داشته باشد.",
        SqlException { Number: 5170 or 5133 or 5120 or 1802 } => "ایجاد فایل‌های دیتابیس روی سرور ناموفق بود.",
        SqlException { Number: 1105 or 9002 } => "فضای دیسک سرور برای این عملیات کافی نیست.",
        SqlException { Number: -2 } => "عملیات از مهلت مجاز عبور کرد.",
        SqlException { Number: 53 or 2 or 10060 or 11001 } => "سرور SQL در دسترس نیست؛ نام سرور، شبکه و پورت را بررسی کنید.",
        SqlException { Number: 26 or 40 } => "نمونهٔ SQL Server پیدا نشد؛ نام نمونه را بررسی کنید.",
        TimeoutException => "عملیات از مهلت مجاز عبور کرد.",
        _ => "عملیات روی SQL Server ناموفق بود؛ جزئیات در لاگ برنامه ثبت شد."
    };

    private static List<StageResult> PendingStages() =>
        Stages.Select(stage => new StageResult(stage.Key, "pending", string.Empty)).ToList();

    private static void SetStage(List<StageResult> stages, string key, string state, string message)
    {
        for (var index = 0; index < stages.Count; index++)
        {
            if (stages[index].Key == key)
            {
                stages[index] = new StageResult(key, state, message);
                return;
            }
        }
    }

    private static InspectOutcome ToInspect(List<StageResult> stages, string message) =>
        new(false, stages, null, false, 0, false, false, false, 0, 0, message);

    private sealed class VendorDatabaseMismatchException(string message) : Exception(message);

    /// <summary>
    /// Database identifier validation, mirroring the verified customer-side rules: letters,
    /// digits, underscore, hyphen, dot and ordinary space in the middle only; no control or
    /// format characters (ZWNJ included); maximum 123 characters so the <c>_log</c> file name
    /// still fits the 128-character SQL Server limit; system names rejected after NFKC
    /// normalization so look-alike homoglyph names like full-width «ｍａｓｔｅｒ» cannot pass.
    /// </summary>
    internal static class VendorDatabaseName
    {
        private const int MaximumLength = 123;
        private static readonly HashSet<string> SystemNames = new(StringComparer.OrdinalIgnoreCase)
            { "master", "model", "msdb", "tempdb" };

        public static bool TryValidate(string? input, out string? validated, out string? error)
        {
            validated = null;
            if (string.IsNullOrWhiteSpace(input))
            {
                error = "نام دیتابیس لازم است.";
                return false;
            }

            if (input.Length > MaximumLength)
            {
                error = $"نام دیتابیس حداکثر {MaximumLength} نویسه می‌تواند باشد.";
                return false;
            }

            if (char.IsWhiteSpace(input[0]) || char.IsWhiteSpace(input[^1]) ||
                input[0] == '.' || input[^1] == '.')
            {
                error = "نام دیتابیس نمی‌تواند با نقطه یا فاصله شروع یا پایان یابد.";
                return false;
            }

            foreach (var character in input)
            {
                if (char.IsControl(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format)
                {
                    error = "نام دیتابیس نمی‌تواند نویسهٔ کنترلی یا قالب‌بندی داشته باشد.";
                    return false;
                }

                if (!(char.IsLetter(character) || char.IsDigit(character) ||
                      character is '_' or '-' or '.' or ' '))
                {
                    error = "نام دیتابیس فقط می‌تواند حرف، رقم، «_»، «-»، «.» و فاصله داشته باشد.";
                    return false;
                }
            }

            if (input.Contains("..", StringComparison.Ordinal))
            {
                error = "نام دیتابیس نمی‌تواند «..» متوالی داشته باشد.";
                return false;
            }

            var normalized = input.Normalize(NormalizationForm.FormKC);
            if (SystemNames.Contains(normalized))
            {
                error = "نام دیتابیس سیستمی مجاز نیست.";
                return false;
            }

            validated = input;
            error = null;
            return true;
        }
    }

    /// <summary>Strict login name validation: ASCII letters, digits and underscore only.</summary>
    internal static class VendorRuntimeLoginName
    {
        private const int MinimumLength = 3;
        private const int MaximumLength = 128;
        private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
            { "sa", "dbo", "guest", "sys", "public", "information_schema" };

        public static bool TryValidate(string input, out string? error)
        {
            if (string.IsNullOrWhiteSpace(input) ||
                input.Length < MinimumLength || input.Length > MaximumLength ||
                !char.IsAsciiLetter(input[0]) ||
                !input.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
            {
                error = "نام هویت Runtime باید با حرف انگلیسی شروع شود و فقط حرف، رقم و «_» داشته باشد (۳ تا ۱۲۸ نویسه).";
                return false;
            }

            if (ReservedNames.Contains(input))
            {
                error = "این نام برای SQL Server رزرو شده است؛ نام دیگری انتخاب کنید.";
                return false;
            }

            error = null;
            return true;
        }
    }

    /// <summary>
    /// Cryptographically random runtime password over an unambiguous character set. Quote,
    /// backslash, semicolon and space are excluded so the value stays copy-paste safe in
    /// connection strings and operator notes.
    /// </summary>
    internal static class VendorRuntimePassword
    {
        private const int Length = 32;
        private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        private const string Lower = "abcdefghijkmnpqrstuvwxyz";
        private const string Digits = "23456789";
        private const string Specials = "!@#$%^&*()-_=+[]{}:,.?/~";

        public static string Generate()
        {
            var all = Upper + Lower + Digits + Specials;
            Span<char> buffer = stackalloc char[Length];
            for (var index = 0; index < Length; index++)
            {
                buffer[index] = all[RandomNumberGenerator.GetInt32(all.Length)];
            }

            // Guarantee one of each class so any server password policy accepts it.
            buffer[0] = Upper[RandomNumberGenerator.GetInt32(Upper.Length)];
            buffer[1] = Lower[RandomNumberGenerator.GetInt32(Lower.Length)];
            buffer[2] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
            buffer[3] = Specials[RandomNumberGenerator.GetInt32(Specials.Length)];

            for (var index = Length - 1; index > 0; index--)
            {
                var swap = RandomNumberGenerator.GetInt32(index + 1);
                (buffer[index], buffer[swap]) = (buffer[swap], buffer[index]);
            }

            return new string(buffer);
        }
    }

    /// <summary>
    /// Development provisioning only ever talks to a local SQL Server target. The operator's
    /// input is restricted server-side to loopback/local instance names plus an explicit
    /// configuration allow-list (FARANO_VENDOR_Development__Provisioning__AllowedSqlHosts),
    /// so the Development setup page can never become a SQL execution tool against arbitrary
    /// or internal-network hosts. Nothing about the owner's machine is hardcoded.
    /// </summary>
    internal static class VendorSqlServerTarget
    {
        private static readonly HashSet<string> LocalNames = new(StringComparer.OrdinalIgnoreCase)
            { ".", "(local)", "(localdb)", "localhost", "127.0.0.1", "::1" };

        public static bool IsAllowed(
            string? server,
            IEnumerable<string> extraAllowedHosts,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error,
            string? machineName = null)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(server) || server.Length > 256)
            {
                error = "نام سرور لازم است و حداکثر ۲۵۶ نویسه می‌تواند باشد.";
                return false;
            }

            var value = server.Trim();
            if (value.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            {
                value = value["tcp:".Length..].Trim();
            }

            // Accepted shapes: host, host[,port], host\instance, host\instance[,port]. Anything
            // else (extra separators, empty instance, malformed port, second backslash) is
            // rejected before any connection string is built.
            string host;
            if (value.Contains('\\', StringComparison.Ordinal))
            {
                var instanceSeparator = value.IndexOf('\\');
                host = value[..instanceSeparator];
                var rest = value[(instanceSeparator + 1)..];
                if (rest.Length == 0 || rest.Contains('\\', StringComparison.Ordinal))
                {
                    error = "نام نمونهٔ SQL معتبر نیست.";
                    return false;
                }

                var portSeparator = rest.IndexOf(',');
                var instance = portSeparator < 0 ? rest : rest[..portSeparator];
                if (instance.Length == 0 || instance.Length > 16)
                {
                    error = "نام نمونهٔ SQL معتبر نیست.";
                    return false;
                }

                if (portSeparator >= 0 && !IsValidPort(rest[(portSeparator + 1)..]))
                {
                    error = "پورت SQL باید عددی بین ۱ تا ۶۵۵۳۵ باشد.";
                    return false;
                }
            }
            else
            {
                var portSeparator = value.IndexOf(',');
                if (portSeparator >= 0)
                {
                    host = value[..portSeparator];
                    if (!IsValidPort(value[(portSeparator + 1)..]))
                    {
                        error = "پورت SQL باید عددی بین ۱ تا ۶۵۵۳۵ باشد.";
                        return false;
                    }
                }
                else
                {
                    host = value;
                }
            }

            if (host.Length == 0)
            {
                error = "نام سرور معتبر نیست.";
                return false;
            }

            var hostName = host.Trim('[', ']').Trim();
            if (IPAddress.TryParse(hostName, out var address))
            {
                if (IPAddress.IsLoopback(address))
                {
                    return true;
                }

                error = NotAllowedMessage();
                return false;
            }

            if (LocalNames.Contains(hostName) ||
                string.Equals(hostName, machineName ?? Environment.MachineName, StringComparison.OrdinalIgnoreCase) ||
                extraAllowedHosts.Any(candidate =>
                    string.Equals(candidate.Trim(), hostName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            error = NotAllowedMessage();
            return false;

            static string NotAllowedMessage() =>
                "مقصد انتخاب‌شده مجاز نیست؛ در Development فقط SQL Serverهای محلی مجازند. برای هدف تأییدشدهٔ دیگر، میزبان آن را در FARANO_VENDOR_Development__Provisioning__AllowedSqlHosts اضافه کنید.";
        }

        private static bool IsValidPort(string port) =>
            int.TryParse(port, NumberStyles.Integer, CultureInfo.InvariantCulture, out var portNumber) &&
            portNumber is >= 1 and <= 65535;
    }

    /// <summary>
    /// Ownership marker for a provisioned Vendor database: a database-level extended property
    /// written by the controlled create path (and backfilled for databases whose applied
    /// Vendor migration history already proves ownership). An existing database without the
    /// marker and without proven Vendor migration history is never adopted — an empty
    /// database could have been created for another purpose (including an empty Customer
    /// database), so provisioning fails safely instead of claiming it.
    /// </summary>
    internal static class VendorOwnershipMarker
    {
        private const string MarkerName = "FaranouVendorOwnership";
        private const string MarkerValue = "Faranou Vendor database; created/adopted by the Development provisioning flow.";

        public static async Task<bool> ReadAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            var count = await ReadScalarAsync(connection,
                "SELECT CAST(COUNT(*) AS int) FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = @marker",
                cancellationToken,
                new SqlParameter("@marker", System.Data.SqlDbType.NVarChar, 128) { Value = MarkerName });
            return count is int present && present > 0;
        }

        public static async Task WriteAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = @marker)
                    EXEC sys.sp_updateextendedproperty @name = @marker, @value = @value;
                ELSE
                    EXEC sys.sp_addextendedproperty @name = @marker, @value = @value;
                """;
            command.Parameters.Add(new SqlParameter("@marker", System.Data.SqlDbType.NVarChar, 128) { Value = MarkerName });
            command.Parameters.Add(new SqlParameter("@value", System.Data.SqlDbType.NVarChar, 256) { Value = MarkerValue });
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Pure ownership decision, unit-tested: migration runs only on a database this flow
        /// created (marker present) or whose applied Vendor migration history proves it is a
        /// Vendor database. Everything else fails safely with an actionable Persian message.
        /// </summary>
        public static (bool Allowed, string? Refusal) EvaluateOwnership(
            bool markerPresent,
            bool historyTableExists,
            int appliedCount,
            int unknownCount,
            int userTableCount)
        {
            if (unknownCount > 0)
            {
                return (false, "دیتابیس شامل Migrationهای ناشناس برای این نسخهٔ برنامه است؛ احتمالاً نسخهٔ جدیدتری روی آن اجرا شده. Migration اجرا نمی‌شود.");
            }

            if (markerPresent)
            {
                return (true, null);
            }

            if (historyTableExists && appliedCount > 0)
            {
                // Pre-marker Vendor database: its own Faranou migration history proves
                // ownership; the marker is backfilled after the migration succeeds.
                return (true, null);
            }

            if (userTableCount > 0)
            {
                return (false, "این دیتابیس جدول‌هایی دارد اما نه نشانهٔ مالکیت Vendor و نه تاریخچهٔ Migration فارنو در آن نیست؛ برای حفاظت از دادهٔ ناشناس، Migration اجرا نمی‌شود.");
            }

            if (historyTableExists)
            {
                return (false, "تاریخچهٔ Migration دیتابیس خالی است و مالکیت Vendor روی آن قابل اثبات نیست؛ Migration اجرا نمی‌شود.");
            }

            return (false, "دیتابیس از قبل وجود دارد اما هیچ نشانه‌ای از مالکیت Vendor ندارد. یک دیتابیس خالی ممکن است برای هدف دیگری (مثلاً Customer) ساخته شده باشد؛ به‌طور خودکار تصاحب نمی‌شود. یک نام دیتابیس اختصاصی جدید انتخاب کنید یا مالکیت را با مدیر سرور تأیید کنید.");
        }
    }

    /// <summary>
    /// Least-privilege rule for a runtime identity: no fixed server role membership and no
    /// granted server permission beyond CONNECT SQL is acceptable. A login carrying more is
    /// refused as a runtime identity instead of being silently reused.
    /// </summary>
    internal static class VendorRuntimeIdentity
    {
        private static readonly HashSet<string> FixedServerRoles = new(StringComparer.OrdinalIgnoreCase)
            { "sysadmin", "serveradmin", "securityadmin", "processadmin", "setupadmin", "bulkadmin", "diskadmin", "dbcreator" };
        private const string ConnectSqlPermission = "CONNECT SQL";

        public static bool HasExcessiveServerAccess(
            IEnumerable<string> roleNames,
            IEnumerable<string> grantedPermissions,
            out string detail)
        {
            var roles = roleNames.Where(FixedServerRoles.Contains).ToList();
            var grants = grantedPermissions
                .Where(permission => !string.Equals(permission, ConnectSqlPermission, StringComparison.OrdinalIgnoreCase))
                .ToList();
            detail = string.Join("، ", roles.Concat(grants).Distinct());
            return roles.Count > 0 || grants.Count > 0;
        }
    }
}

/// <summary>Request body of the Development setup inspect/provision endpoints.</summary>
public sealed record ProvisionRequest(
    string Server,
    string Database,
    string Authentication,
    string? Username,
    string? Password,
    bool TrustServerCertificate,
    string? RuntimeUsername,
    string? RuntimePassword);
