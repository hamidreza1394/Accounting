using Faranou.Vendor.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Faranou.Vendor.Host;

/// <summary>
/// The full seven-stage local provisioning flow behind the Development database-setup page:
/// validate and test a temporary privileged "setup" connection, inspect the server, create the
/// dedicated Vendor database when missing, apply the existing EF Core migrations, create a
/// least-privilege SQL runtime identity, and verify readiness through that runtime identity.
/// Safety rules: identifiers are validated and bracket-quoted, the target is restricted to the
/// local allow-list, ownership is proven by the marker/intent primitives before any DDL, an
/// existing SQL login is never password-reset or auto-enabled (reuse requires a successful
/// login with the operator-supplied credentials of that login), and nothing is ever dropped.
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
    // concurrent request fails fast instead of racing CREATE DATABASE, the intent record or
    // migrations. The intent ledger write/clear below must only ever run under this lock.
    private static readonly SemaphoreSlim PipelineLock = new(1, 1);

    private sealed record InspectionResult(
        string? ServerVersion,
        bool DatabaseExists,
        int UserTableCount,
        bool HistoryTableExists,
        VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState MarkerState,
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
        bool OwnershipMarkerValid,
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
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))";
                return await ExecuteScalarAsync(command, cancellationToken) as string;
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
            logger.LogWarning(exception, "Vendor development database inspection refused the target.");
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
            inspection.MarkerState == VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Valid,
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

        // ── Stage 1: validate the request and build the temporary setup connection. ──
        SetStage(stages, "connection", "running", "در حال اعتبارسنجی…");
        if (!TryBuildInput(request, extraAllowedSqlHosts, stages, requireRuntimeIdentity: true, out var input, out var failure))
        {
            return ProvisionOutcome.Failed(stages, failure!);
        }

        SetStage(stages, "connection", "success", "اطلاعات اتصال معتبر است.");

        // ── Stage 2: prove the setup identity can reach the server. ──
        try
        {
            SetStage(stages, "test", "running", "در حال آزمون اتصال به سرور…");
            var serverVersion = await OpenServerConnectionAsync(input.SetupBuilder, async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))";
                return await ExecuteScalarAsync(command, cancellationToken) as string;
            }, cancellationToken);
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
            logger.LogWarning(exception, "Vendor development provisioning refused the inspected target.");
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
        // Ownership is decided here: a database we cannot prove is ours is refused before any
        // migration runs, and a crash-window orphan with a matching intent is recovered.
        var markerEnsured = false;
        if (inspection.DatabaseExists)
        {
            var (ownershipAllowed, ownershipRefusal) = VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.EvaluateOwnership(
                inspection.MarkerState,
                inspection.HistoryTableExists,
                inspection.AppliedMigrationsCount,
                inspection.UnknownMigrations.Count,
                inspection.UserTableCount);
            if (ownershipAllowed)
            {
                SetStage(stages, "createDatabase", "skipped", "دیتابیس از قبل وجود دارد و مالکیت آن تأیید شد؛ بدون تغییر ادامه می‌دهیم.");
            }
            else
            {
                var (recoveryAllowed, recoveryRefusal, recoveryMessage) = await TryRecoverCrashWindowOrphanAsync(input, logger, inspection, cancellationToken);
                if (recoveryAllowed)
                {
                    markerEnsured = true;
                    SetStage(stages, "createDatabase", "skipped", recoveryMessage!);
                }
                else
                {
                    SetStage(stages, "createDatabase", "failed", "مالکیت دیتابیس موجود قابل اثبات نیست؛ عملیات متوقف شد.");
                    return ProvisionOutcome.Failed(stages, recoveryRefusal ?? ownershipRefusal!);
                }
            }
        }
        else
        {
            try
            {
                SetStage(stages, "createDatabase", "running", "در حال ایجاد دیتابیس…");
                // The intent record binds a crash after CREATE DATABASE to this very flow: the
                // next run may then recover the empty unmarked database in a controlled way.
                try
                {
                    await WriteProvisioningIntentAsync(input, logger, cancellationToken);
                }
                catch (Exception intentException) when (intentException is SqlException or InvalidOperationException or TimeoutException)
                {
                    logger.LogWarning(intentException, "Vendor development provisioning could not record the provisioning intent; the database create is stopped.");
                    SetStage(stages, "createDatabase", "failed", "ثبت رکورد قصد راه‌اندازی روی سرور ممکن نشد؛ برای جلوگیری از گذاشتن دیتابیس بی‌نشانِ بدون امکان Recovery، ایجاد دیتابیس متوقف شد.");
                    return ProvisionOutcome.Failed(stages, "ثبت رکورد قصد راه‌اندازی روی سرور ممکن نشد؛ ایجاد دیتابیس متوقف شد. دسترسی هویت پیکربندی روی master بررسی شود.");
                }
                // CREATE DATABASE cannot be parameterized; the name passed the identifier
                // validation (stage 1) and is bracket-quoted here. A browser closing mid-run
                // must not leave a half-started create, so this runs to completion.
                await OpenServerConnectionAsync(input.SetupBuilder, async connection =>
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = $"CREATE DATABASE {VendorDevelopmentDatabaseSafety.VendorSqlIdentifier.Quote(input.Database)}";
                    command.CommandTimeout = CreateDatabaseCommandTimeoutSeconds;
                    await command.ExecuteNonQueryAsync(CancellationToken.None);
                    return true;
                }, CancellationToken.None);
                // Prove ownership immediately: without the marker a freshly created database
                // would be indistinguishable from an empty database created for another purpose.
                await OpenServerConnectionAsync(TargetBuilder(input), async connection =>
                {
                    // The in-database intent record is written BEFORE the ownership marker: it
                    // is the durable evidence that this flow (not another service) created the
                    // database, and recovery of a crash before the marker depends on it.
                    await VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.WriteInDatabaseAsync(connection, input.Database, DateTimeOffset.UtcNow, CancellationToken.None);
                    await VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.WriteAsync(connection, CancellationToken.None);
                    await VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.ClearInDatabaseAsync(connection, CancellationToken.None);
                    return true;
                }, CancellationToken.None);
                markerEnsured = true;
                await ClearProvisioningIntentAsync(input, logger, CancellationToken.None);
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

                if (!inspection.DatabaseExists)
                {
                    SetStage(stages, "createDatabase", "failed", "دیتابیس هم‌زمان ساخته شد اما اکنون پیدا نمی‌شود؛ دوباره تلاش کنید.");
                    return ProvisionOutcome.Failed(stages, "وضعیت دیتابیس پس از ساخت هم‌زمان نامشخص است.");
                }

                var (concurrentAllowed, concurrentRefusal) = VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.EvaluateOwnership(
                    inspection.MarkerState,
                    inspection.HistoryTableExists,
                    inspection.AppliedMigrationsCount,
                    inspection.UnknownMigrations.Count,
                    inspection.UserTableCount);
                if (!concurrentAllowed)
                {
                    // This run's intent record cannot prove that ANOTHER process's create
                    // belongs to Vendor: after a 1801 race, only independent ownership proof
                    // (marker or Vendor migration history) is accepted, and recovery of a
                    // crash-window orphan is left to the next clean run.
                    await ClearProvisioningIntentAsync(input, logger, CancellationToken.None);
                    SetStage(stages, "createDatabase", "failed", "دیتابیس هم‌زمان ساخته شد اما مالکیت Vendor روی آن قابل اثبات نیست؛ به‌طور خودکار تصاحب نمی‌شود.");
                    return ProvisionOutcome.Failed(stages, concurrentRefusal!);
                }
                else
                {
                    markerEnsured = inspection.MarkerState == VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Valid;
                    await ClearProvisioningIntentAsync(input, logger, CancellationToken.None);
                    SetStage(stages, "createDatabase", "skipped", "دیتابیس هم‌زمان توسط فرایند دیگری ساخته شد؛ مالکیت آن تأیید شد و همان استفاده می‌شود.");
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
        try
        {
            SetStage(stages, "migrate", "running", "در حال اعمال Migrationها…");
            var appliedNow = await ApplyMigrationsAsync(input, logger, inspection, markerEnsured);
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
        var createdFresh = false;
        try
        {
            SetStage(stages, "runtimeIdentity", "running", "در حال تنظیم هویت Runtime…");
            createdFresh = await EnsureRuntimeIdentityAsync(input, logger);
            SetStage(stages, "runtimeIdentity", "success",
                createdFresh
                    ? $"هویت Runtime کم‌اختیار «{input.RuntimeUsername}» ساخته شد؛ دسترسی آن فقط روی همین دیتابیس (خواندن/نوشتن) است و هیچ نقش یا مجوز سطح سرور ندارد."
                    : $"از Login موجود «{input.RuntimeUsername}» با اثبات گذرواژه استفاده شد (بدون تغییر گذرواژه)؛ نقش‌های خواندن/نوشتن روی همین دیتابیس تضمین شد و هیچ نقش یا مجوز سطح سرور مجاز شمرده نمی‌شود.");
        }
        catch (VendorDatabaseMismatchException exception)
        {
            // Safety guard: never take over an unproven or over-privileged login, never reset
            // an existing password, never re-enable a disabled login.
            logger.LogWarning(exception, "Vendor development provisioning refused the requested runtime identity.");
            SetStage(stages, "runtimeIdentity", "failed", exception.Message);
            return ProvisionOutcome.Failed(stages, exception.Message);
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException or TimeoutException)
        {
            logger.LogWarning(exception, "Vendor development provisioning failed while configuring the runtime identity.");
            SetStage(stages, "runtimeIdentity", "failed", DescribeSqlError(exception));
            if (createdFresh)
            {
                await RollBackFreshLoginAsync(input, logger);
            }

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
            if (createdFresh)
            {
                await RollBackFreshLoginAsync(input, logger);
            }

            return ProvisionOutcome.Failed(stages, "بررسی آمادگی با اتصال Runtime ناموفق بود.");
        }

        // Persist the runtime (least-privilege) connection for the next Host run. Only the
        // runtime identity is ever persisted; the admin/provisioning credentials from the
        // request are never stored, logged or returned.
        var saveFailed = false;
        var savedToCredentialManager = false;
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

        string finalMessage;
        if (saveFailed)
        {
            finalMessage = "راه‌اندازی کامل شد اما ذخیرهٔ اتصال Runtime در Windows Credential Manager ناموفق بود؛ از دکمهٔ ذخیرهٔ صفحه استفاده کنید یا دوباره تلاش کنید.";
        }
        else if (savedToCredentialManager)
        {
            finalMessage = $"راه‌اندازی کامل شد. اتصال Runtime کم‌اختیار برای کاربر فعلی ذخیره شد؛ برای اعمال، Vendor را دوباره اجرا کنید.{(input.GeneratedRuntimePassword ? " گذرواژهٔ ساخته‌شدهٔ Runtime فقط همین یک‌بار نمایش داده می‌شود." : string.Empty)}";
        }
        else if (input.GeneratedRuntimePassword)
        {
            finalMessage = "راه‌اندازی کامل شد؛ اما به‌دلیل اینکه تنظیم اتصال از Environment/Configuration می‌آید، چیزی ذخیره نشد. نام کاربری Runtime و گذرواژهٔ ساخته‌شده را در همان منبع تنظیم کنید.";
        }
        else
        {
            finalMessage = $"راه‌اندازی کامل شد؛ اتصال Runtime با هویت «{input.RuntimeUsername}» آماده است.";
        }

        return new ProvisionOutcome(true, stages, input.RuntimeUsername, input.GeneratedRuntimePassword ? input.RuntimePassword : null, savedToCredentialManager, finalMessage);
    }

    /// <summary>
    /// Controlled recovery of the crash window between CREATE DATABASE and the ownership
    /// marker write: the database exists, is empty and unmarked, and the intent ledger on
    /// master carries a fresh, name-matching record whose time is not older than the
    /// server-recorded creation date. Anything less keeps the database fail-closed.
    /// </summary>
    private static async Task<(bool Allowed, string? Refusal, string? RecoveryMessage)> TryRecoverCrashWindowOrphanAsync(
        ProvisionInput input,
        ILogger logger,
        InspectionResult inspection,
        CancellationToken cancellationToken)
    {
        try
        {
            string? intentValue = null;
            DateTimeOffset? createdUtc = null;
            await OpenServerConnectionAsync(input.SetupBuilder, async connection =>
            {
                intentValue = await VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.ReadAsync(connection, cancellationToken);
                createdUtc = await VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.ReadDatabaseCreatedUtcAsync(connection, input.Database, cancellationToken);
                return true;
            }, cancellationToken);

            // A foreign/fabricated marker blocks recovery: only a genuinely unmarked orphan
            // may be recovered, and only with the full evidence set below.
            if (inspection.MarkerState != VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Absent)
            {
                return (false, "دیتابیس هدف نشانهٔ مالکیت (درست یا نادرست) دارد و مسیر Recovery رکورد قصد روی آن اعمال نمی‌شود؛ وضعیت را با مدیر سرور بررسی کنید.", null);
            }

            var intentState = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.Classify(intentValue, input.Database, DateTimeOffset.UtcNow);
            var parsed = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.TryParse(intentValue, out _, out var issuedUtc);
            string? inDatabaseIntentValue = null;
            await OpenServerConnectionAsync(TargetBuilder(input), async connection =>
            {
                inDatabaseIntentValue = await VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.ReadInDatabaseAsync(connection, cancellationToken);
                return true;
            }, cancellationToken);
            var inDatabaseIntentPresent = !string.IsNullOrEmpty(inDatabaseIntentValue);
            var (allowed, refusal) = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.EvaluateRecovery(
                intentState, createdUtc, parsed ? issuedUtc : null, inspection.HistoryTableExists, inspection.UserTableCount,
                inDatabaseIntentPresent);
            if (!allowed)
            {
                return (false, refusal, null);
            }

            await OpenServerConnectionAsync(TargetBuilder(input), async connection =>
            {
                await VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.WriteAsync(connection, CancellationToken.None);
                return true;
            }, CancellationToken.None);
            await ClearProvisioningIntentAsync(input, logger, CancellationToken.None);
            logger.LogWarning(
                "Vendor development provisioning recovered an orphaned database ({Database}) left by a crashed run between CREATE DATABASE and the ownership marker.",
                input.Database);
            return (true, null, VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.RecoveryMessage(input.Database));
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException or TimeoutException)
        {
            // Recovery evidence could not be evaluated: stay closed rather than adopt.
            logger.LogWarning(exception, "Vendor development provisioning could not evaluate the crash-window recovery evidence.");
            return (false, "بررسی رکورد راه‌اندازی برای Recovery ناموفق بود؛ دیتابیس ناشناس باقی می‌ماند و عملیات انجام نمی‌شود.", null);
        }
    }

    private static async Task WriteProvisioningIntentAsync(ProvisionInput input, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            await OpenServerConnectionAsync(input.SetupBuilder, async connection =>
            {
                await VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.WriteAsync(
                    connection, input.Database, DateTimeOffset.UtcNow, cancellationToken);
                return true;
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException or TimeoutException)
        {
            // The intent ledger only enables crash-window recovery; without it the next run
            // simply fails closed on the empty unmarked database. The create is still safe.
            logger.LogWarning(exception, "Vendor development provisioning could not record the provisioning intent before creating the database.");
        }
    }

    private static async Task ClearProvisioningIntentAsync(ProvisionInput input, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            await OpenServerConnectionAsync(input.SetupBuilder, async connection =>
            {
                await VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.ClearAsync(connection, cancellationToken);
                return true;
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException or TimeoutException)
        {
            logger.LogWarning(exception, "Vendor development provisioning could not clear the provisioning intent after a successful marker write.");
        }
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
        if (!VendorDevelopmentDatabaseSafety.VendorSqlServerTarget.IsAllowed(request.Server, extraAllowedSqlHosts, out var targetError))
        {
            failure = targetError;
            SetStage(stages, "connection", "failed", failure);
            return false;
        }

        if (!VendorDevelopmentDatabaseSafety.VendorDatabaseName.TryValidate(request.Database, out var database, out var databaseError))
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
        if (!VendorDevelopmentDatabaseSafety.VendorRuntimeLoginName.TryValidate(runtimeUsername, out var loginError))
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

    /// <summary>
    /// Executes an already-built command; SQL text is always a literal at the call site and
    /// the values are parameters, so no SQL string is ever assembled dynamically here.
    /// </summary>
    private static async Task<object?> ExecuteScalarAsync(SqlCommand command, CancellationToken cancellationToken)
    {
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
            await using var versionCommand = connection.CreateCommand();
            versionCommand.CommandText = "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))";
            serverVersion = (await ExecuteScalarAsync(versionCommand, cancellationToken) as string) ?? string.Empty;
            // Belt and braces against look-alike names: DB_ID resolves the homoglyph names to
            // the system database ids (1..4) even when string comparison misses them.
            await using var databaseIdCommand = connection.CreateCommand();
            databaseIdCommand.CommandText = "SELECT DB_ID(@name)";
            databaseIdCommand.Parameters.Add(new SqlParameter("@name", System.Data.SqlDbType.NVarChar, 128) { Value = input.Database });
            var databaseId = await ExecuteScalarAsync(databaseIdCommand, cancellationToken);
            if (databaseId is int id && id is >= 1 and <= 4)
            {
                throw new VendorDatabaseMismatchException("نام انتخابی به یک دیتابیس سیستمی SQL Server اشاره می‌کند و مجاز نیست.");
            }

            databaseExists = databaseId is not null and not DBNull;
            await using var loginIdCommand = connection.CreateCommand();
            loginIdCommand.CommandText = "SELECT CAST(principal_id AS int) FROM sys.server_principals WHERE name = @login";
            loginIdCommand.Parameters.Add(new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
            var loginId = await ExecuteScalarAsync(loginIdCommand, cancellationToken);
            runtimeLoginExists = loginId is int;
            return true;
        }, cancellationToken);

        if (!databaseExists)
        {
            return new InspectionResult(
                serverVersion, false, 0, false, VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Absent, runtimeLoginExists, [], 0, 0);
        }

        // The database exists: inspect it with the setup identity before touching anything.
        var targetBuilder = TargetBuilder(input);
        int userTableCount = 0;
        var historyTableExists = false;
        var markerState = VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Absent;
        var appliedIds = new List<string>();

        await OpenServerConnectionAsync(targetBuilder, async connection =>
        {
            await using var tablesCommand = connection.CreateCommand();
            tablesCommand.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0";
            var tables = await ExecuteScalarAsync(tablesCommand, cancellationToken);
            userTableCount = tables is int count ? count : 0;
            await using var historyCommand = connection.CreateCommand();
            historyCommand.CommandText = """
                SELECT COUNT(*)
                FROM sys.tables t
                JOIN sys.schemas s ON s.schema_id = t.schema_id
                WHERE t.name = @history AND s.name = N'dbo'
                """;
            historyCommand.Parameters.Add(new SqlParameter("@history", System.Data.SqlDbType.NVarChar, 128) { Value = MigrationsHistoryTable });
            var history = await ExecuteScalarAsync(historyCommand, cancellationToken);
            historyTableExists = history is int h && h > 0;
            markerState = await VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.ReadStateAsync(connection, cancellationToken);
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
            serverVersion, true, userTableCount, historyTableExists, markerState, runtimeLoginExists, unknown, appliedCount, pendingCount);
    }

    private static string DescribeInspection(InspectionResult inspection)
    {
        var loginState = inspection.RuntimeLoginExists
            ? "هویت Runtime از قبل وجود دارد (استفادهٔ مجدد فقط با گذرواژهٔ خودِ همان Login)"
            : "هویت Runtime ساخته خواهد شد";
        if (!inspection.DatabaseExists)
        {
            return $"دیتابیس وجود ندارد؛ با «راه‌اندازی کامل» ساخته می‌شود و {loginState}.";
        }

        var ownership = inspection.MarkerState switch
        {
            VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Valid => "نشانهٔ مالکیت Vendor را دارد",
            VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.InvalidValue => "نشانهٔ مالکیت با مقدار Vendor مطابقت ندارد (نشانهٔ خارجی یا جعلی)",
            _ => "نشانهٔ مالکیت Vendor ندارد"
        };
        var state = (inspection.MarkerState, inspection.HistoryTableExists, inspection.UserTableCount) switch
        {
            (VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Valid, false, _) => $"{ownership} اما Migrationها هنوز روی آن اعمال نشده است",
            (VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.InvalidValue, _, _) => ownership,
            (_, false, > 0) => "جدول‌هایی دارد اما نه نشانهٔ مالکیت Vendor و نه تاریخچهٔ Migration فارنو در آن نیست (دیتابیس ناشناس)",
            (_, false, _) => "خالی است و هیچ نشانه‌ای از مالکیت Vendor ندارد؛ به‌طور خودکار تصاحب نمی‌شود",
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
        await using var historyCommand = connection.CreateCommand();
        historyCommand.CommandText = """
            SELECT COUNT(*)
            FROM sys.tables t
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE t.name = @history AND s.name = N'dbo'
            """;
        historyCommand.Parameters.Add(new SqlParameter("@history", System.Data.SqlDbType.NVarChar, 128) { Value = MigrationsHistoryTable });
        var historyExists = await ExecuteScalarAsync(historyCommand, CancellationToken.None) is int h && h > 0;
        await using var userTablesCommand = connection.CreateCommand();
        userTablesCommand.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0";
        var userTables = await ExecuteScalarAsync(userTablesCommand, CancellationToken.None);
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
        // Trust only what the database itself can prove RIGHT NOW on the migration
        // connection: if a marker this run wrote earlier is not visible here, it is written
        // again before the ownership decision (never bypassed).
        var markerState = await VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.ReadStateAsync(connection, CancellationToken.None);
        if (markerState != VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Valid && markerEnsured)
        {
            await VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.WriteAsync(connection, CancellationToken.None);
            markerState = await VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.ReadStateAsync(connection, CancellationToken.None);
        }
        var (ownershipAllowed, ownershipRefusal) = VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.EvaluateOwnership(
            markerState, historyExists, appliedIds.Count, unknownCount, userTableCount);
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
        var appliedResult = await ExecuteScalarAsync(appliedCommand, CancellationToken.None);
        var appliedAfter = appliedResult is int appliedCount ? appliedCount : appliedBefore;
        if (appliedAfter > appliedBefore)
        {
            logger.LogInformation("Vendor development provisioning applied {AppliedCount} migrations.", appliedAfter - appliedBefore);
        }

        if (markerState != VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Valid)
        {
            // Pre-marker Vendor database proven by its own migration history: record the
            // marker so future runs rely on one consistent ownership proof. History remains
            // sufficient proof on its own, so a marker write failure is not fatal here.
            try
            {
                await VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.WriteAsync(connection, CancellationToken.None);
            }
            catch (Exception exception) when (exception is SqlException or InvalidOperationException or TimeoutException)
            {
                logger.LogWarning(exception, "Vendor development provisioning could not backfill the ownership marker; migration history remains the ownership proof.");
            }
        }

        context.ChangeTracker.Clear();
        return appliedAfter - appliedBefore;
    }

    /// <summary>
    /// Ensures the least-privilege SQL runtime identity. A fresh login is created first
    /// (CHECK_POLICY=ON, least catalog visibility), then mapped to a user of the target
    /// database only, with read/write roles. For an existing login this flow NEVER resets
    /// the password and NEVER re-enables a disabled login: reuse happens only when the
    /// operator proves the login with its own credentials BEFORE any role grant, and a
    /// mapping created by this run for the proof is removed again when the proof fails.
    /// The database user is matched to the login by SID, so a same-name user of another
    /// login is refused instead of silently receiving the grants.
    /// </summary>
    private static async Task<bool> EnsureRuntimeIdentityAsync(ProvisionInput input, ILogger logger)
    {
        var createdFresh = false;

        await OpenServerConnectionAsync(input.SetupBuilder, async connection =>
        {
            // Fail closed BEFORE the reuse-vs-fresh decision: a setup identity that cannot
            // read server principals sees only itself in sys.server_principals, so an
            // existing runtime login looks absent and the flow would misroute into
            // CREATE LOGIN. The existing-check below is only trustworthy when the audit
            // of server principals is provably complete.
            await using (var readinessCommand = connection.CreateCommand())
            {
                readinessCommand.CommandText = """
                    SELECT CAST(CASE
                        WHEN IS_SRVROLEMEMBER('sysadmin') = 1 THEN 1
                        WHEN EXISTS (
                            SELECT 1 FROM fn_my_permissions(NULL, 'SERVER')
                            WHERE permission_name IN ('VIEW ANY DEFINITION', 'CONTROL SERVER'))
                            THEN 1
                        ELSE 0 END AS int)
                    """;
                var canAuditServerPrincipals = await ExecuteScalarAsync(readinessCommand, CancellationToken.None) is 1;
                var (readinessAllowed, readinessRefusal) = VendorDevelopmentRuntimeIdentity.VendorCrossDatabaseAccess.EvaluatePrincipalAuditReadiness(
                    input.RuntimeUsername, canAuditServerPrincipals);
                if (!readinessAllowed)
                {
                    throw new VendorDatabaseMismatchException(readinessRefusal!);
                }
            }

            await using var existingCommand = connection.CreateCommand();
            existingCommand.CommandText = "SELECT CAST(principal_id AS int) FROM sys.server_principals WHERE name = @login AND type = 'S'";
            existingCommand.Parameters.Add(new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
            var existing = await ExecuteScalarAsync(existingCommand, CancellationToken.None);
            var reusedExistingLogin = existing is int;

            if (reusedExistingLogin)
            {
                // An existing login must carry no server-level power at all: no fixed or
                // instance-created server role membership, no granted server permission
                // beyond CONNECT SQL, and no CONNECT SQL deny.
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

                var denies = new List<string>();
                await using (var deniesCommand = connection.CreateCommand())
                {
                    deniesCommand.CommandText = """
                        SELECT pe.permission_name
                        FROM sys.server_permissions pe
                        JOIN sys.server_principals g ON g.principal_id = pe.grantee_principal_id
                        WHERE g.name = @login AND pe.state = 'D'
                        """;
                    deniesCommand.Parameters.Add(new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
                    await using var reader = await deniesCommand.ExecuteReaderAsync(CancellationToken.None);
                    while (await reader.ReadAsync(CancellationToken.None))
                    {
                        denies.Add(reader.GetString(0));
                    }
                }

                if (VendorDevelopmentRuntimeIdentity.VendorRuntimeIdentity.HasExcessiveServerAccess(roles, grants, denies, out var serverDetail))
                {
                    throw new VendorDatabaseMismatchException($"هویت Runtime «{input.RuntimeUsername}» از قبل مجوزهای سطح سرور دارد ({serverDetail}) و به‌عنوان هویت کم‌اختیار قابل استفاده نیست؛ نام دیگری انتخاب کنید یا مجوزهای سرور را با تصمیم مدیر سرور اصلاح کنید.");
                }

                // A least-privilege runtime identity must not hold user mappings in any other
                // database: that is exactly how access would leak beyond the vendor database.
                // The mapping audit is only trustworthy when the setup identity can enumerate
                // and open every non-target database, so both gaps fail closed before reuse
                // is accepted.
                var otherDatabases = new List<string>();
                var unauditableDatabases = new List<string>();
                var canEnumerateAllDatabases = false;
                await using (var coverageCommand = connection.CreateCommand())
                {
                    // Effective permissions (fn_my_permissions) include role-inherited grants,
                    // so a setup identity holding VIEW ANY DATABASE through any source counts.
                    coverageCommand.CommandText = """
                        SELECT CAST(CASE
                            WHEN IS_SRVROLEMEMBER('sysadmin') = 1 THEN 1
                            WHEN EXISTS (
                                SELECT 1 FROM fn_my_permissions(NULL, 'SERVER')
                                WHERE permission_name IN ('VIEW ANY DATABASE', 'CONTROL SERVER'))
                                THEN 1
                            ELSE 0 END AS int)
                        """;
                    canEnumerateAllDatabases = await ExecuteScalarAsync(coverageCommand, CancellationToken.None) is 1;

                    await using (var otherDatabasesCommand = connection.CreateCommand())
                    {
                        otherDatabasesCommand.CommandText = """
                            SELECT d.name, CAST(HAS_DBACCESS(d.name) AS int) AS HasAccess
                            FROM sys.databases d
                            WHERE d.database_id <> DB_ID(@target)
                            """;
                        otherDatabasesCommand.Parameters.Add(new SqlParameter("@target", System.Data.SqlDbType.NVarChar, 128) { Value = input.Database });
                        await using var reader = await otherDatabasesCommand.ExecuteReaderAsync(CancellationToken.None);
                        while (await reader.ReadAsync(CancellationToken.None))
                        {
                            var databaseName = reader.GetString(0);
                            var hasAccess = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                            if (hasAccess == 1)
                            {
                                otherDatabases.Add(databaseName);
                            }
                            else
                            {
                                // HAS_DBACCESS = 0 (or NULL) describes the SETUP identity's access,
                                // not the absence of a runtime mapping: an offline or inaccessible
                                // database keeps its mapping and exposes it when it comes online.
                                unauditableDatabases.Add(databaseName);
                            }
                        }
                    }
                }

                var mappedInOther = new List<string>();
                foreach (var otherDatabase in otherDatabases)
                {
                    // Principal-visibility canary: metadata visibility can hide OTHER users'
                    // rows even where HAS_DBACCESS = 1, so a zero mapping count is only
                    // trustworthy when this database's principal rows are actually readable.
                    // Every real database holds at least the dbo principal with a different
                    // SID, so zero visible others proves the rows are NOT readable and the
                    // database is added to the un-auditable list (fail closed) instead of
                    // being counted as "no mapping found".
                    await using var visibilityCommand = connection.CreateCommand();
                    visibilityCommand.CommandText = $"SELECT CAST(COUNT(*) AS int) FROM {VendorDevelopmentDatabaseSafety.VendorSqlIdentifier.Quote(otherDatabase)}.sys.database_principals AS dp WHERE dp.sid <> SUSER_SID()";
                    var visibleOthers = await ExecuteScalarAsync(visibilityCommand, CancellationToken.None);
                    if (visibleOthers is not > 0)
                    {
                        unauditableDatabases.Add(otherDatabase);
                        continue;
                    }

                    await using var mappedCommand = connection.CreateCommand();
                    mappedCommand.CommandText = $"SELECT CAST(COUNT(*) AS int) FROM {VendorDevelopmentDatabaseSafety.VendorSqlIdentifier.Quote(otherDatabase)}.sys.database_principals AS dp WHERE dp.type IN ('S', 'U') AND dp.sid = (SELECT sp.sid FROM sys.server_principals AS sp WHERE sp.name = @login AND sp.type = 'S')";
                    mappedCommand.Parameters.Add(new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
                    if (await ExecuteScalarAsync(mappedCommand, CancellationToken.None) is int mappedCount && mappedCount > 0)
                    {
                        mappedInOther.Add(otherDatabase);
                    }
                }

                var (coverageAllowed, coverageRefusal) = VendorDevelopmentRuntimeIdentity.VendorCrossDatabaseAccess.EvaluateMappingAuditCoverage(
                    input.RuntimeUsername, canEnumerateAllDatabases, unauditableDatabases);
                if (!coverageAllowed)
                {
                    throw new VendorDatabaseMismatchException(coverageRefusal!);
                }

                var (crossDbAllowed, crossDbRefusal) = VendorDevelopmentRuntimeIdentity.VendorCrossDatabaseAccess.EvaluateUserMappings(input.RuntimeUsername, mappedInOther);
                if (!crossDbAllowed)
                {
                    throw new VendorDatabaseMismatchException(crossDbRefusal!);
                }

                // A generated password can never prove ownership of an existing login: fail
                // closed instead of silently resetting a login this flow does not own.
                if (input.GeneratedRuntimePassword || input.RuntimePassword is null)
                {
                    throw new VendorDatabaseMismatchException($"هویت Runtime «{input.RuntimeUsername}» از قبل وجود دارد؛ استفادهٔ مجدد فقط با واردکردن گذرواژهٔ خودِ همان Login ممکن است. گذرواژهٔ Login موجود هرگز تغییر داده نمی‌شود؛ اگر گذرواژهٔ آن را نمی‌دانید، نام Runtime جدیدی انتخاب کنید.");
                }

                await using var disabledCommand = connection.CreateCommand();
                disabledCommand.CommandText = "SELECT is_disabled FROM sys.sql_logins WHERE name = @login";
                disabledCommand.Parameters.Add(new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
                var disabled = await ExecuteScalarAsync(disabledCommand, CancellationToken.None);
                if (disabled is bool isDisabled && isDisabled)
                {
                    throw new VendorDatabaseMismatchException($"هویت Runtime «{input.RuntimeUsername}» غیرفعال است؛ فعال‌کردن Login تصمیم مدیر سرور است و به‌طور خودکار انجام نمی‌شود.");
                }
            }
            else
            {
                createdFresh = true;
                await OpenServerConnectionAsync(input.SetupBuilder, async connection =>
                {
                    await using var command = connection.CreateCommand();
                    // CREATE LOGIN does not accept variables for the password in T-SQL, so the
                    // value must be embedded as a literal: single quotes are doubled, which
                    // makes the embedding safe for any password the validation accepted.
                    command.CommandText = $"CREATE LOGIN {VendorDevelopmentDatabaseSafety.VendorSqlIdentifier.Quote(input.RuntimeUsername)} WITH PASSWORD = N'{input.RuntimePassword!.Replace("'", "''", StringComparison.Ordinal)}', CHECK_POLICY = ON, DEFAULT_DATABASE = {VendorDevelopmentDatabaseSafety.VendorSqlIdentifier.Quote(input.Database)}";
                    await command.ExecuteNonQueryAsync(CancellationToken.None);

                    // Least visibility: without it the login could list every database name on the
                    // instance. Databases it has no user in stay invisible and inaccessible anyway.
                    await using var deny = connection.CreateCommand();
                    deny.CommandText = $"DENY VIEW ANY DATABASE TO {VendorDevelopmentDatabaseSafety.VendorSqlIdentifier.Quote(input.RuntimeUsername)}";
                    await deny.ExecuteNonQueryAsync(CancellationToken.None);
                    return true;
                }, CancellationToken.None);
            }

            // Database level: map the login to a user of the target database only. The user is
            // matched to the login by SID: a same-name user belonging to another login is
            // refused instead of receiving this identity's grants.
            var mappingPreExisted = false;
            await OpenServerConnectionAsync(TargetBuilder(input), async connection =>
            {
                await using var userCommand = connection.CreateCommand();
                userCommand.CommandText = """
                    SELECT CAST(COUNT(*) AS int)
                    FROM sys.database_principals dp
                    WHERE dp.name = @login AND dp.type IN ('S', 'U')
                    """;
                userCommand.Parameters.Add(new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
                var userCount = await ExecuteScalarAsync(userCommand, CancellationToken.None) is int users && users > 0;
                mappingPreExisted = userCount;
                if (userCount)
                {
                    // A same-SID user can still carry management-level access (db_owner-class
                    // roles or direct CONTROL/ALTER grants) that contradicts least privilege.
                    await using var excessRolesCommand = connection.CreateCommand();
                    excessRolesCommand.CommandText = """
                        SELECT r.name
                        FROM sys.database_role_members drm
                        JOIN sys.database_principals r ON r.principal_id = drm.role_principal_id
                        JOIN sys.database_principals m ON m.principal_id = drm.member_principal_id
                        WHERE m.name = @login
                          AND r.name IN (N'db_owner', N'db_securityadmin', N'db_accessadmin', N'db_backupoperator', N'db_ddladmin')
                        """;
                    excessRolesCommand.Parameters.Add(new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
                    var excessRoles = new List<string>();
                    await using (var reader = await excessRolesCommand.ExecuteReaderAsync(CancellationToken.None))
                    {
                        while (await reader.ReadAsync(CancellationToken.None))
                        {
                            excessRoles.Add(reader.GetString(0));
                        }
                    }

                    await using var excessGrantsCommand = connection.CreateCommand();
                    excessGrantsCommand.CommandText = """
                        SELECT pe.permission_name
                        FROM sys.database_permissions pe
                        JOIN sys.database_principals g ON g.principal_id = pe.grantee_principal_id
                        WHERE g.name = @login AND pe.state IN ('G', 'W')
                          AND pe.permission_name IN (N'CONTROL', N'ALTER', N'TAKE OWNERSHIP', N'IMPERSONATE', N'VIEW DEFINITION')
                        """;
                    excessGrantsCommand.Parameters.Add(new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
                    var excessGrants = new List<string>();
                    await using (var reader = await excessGrantsCommand.ExecuteReaderAsync(CancellationToken.None))
                    {
                        while (await reader.ReadAsync(CancellationToken.None))
                        {
                            excessGrants.Add(reader.GetString(0));
                        }
                    }

                    if (excessRoles.Count > 0 || excessGrants.Count > 0)
                    {
                        var detail = string.Join("، ", excessRoles.Concat(excessGrants).Distinct(StringComparer.OrdinalIgnoreCase));
                        throw new VendorDatabaseMismatchException($"کاربر «{input.RuntimeUsername}» در دیتابیس هدف دسترسی مدیریتی دارد ({detail}) و به‌عنوان هویت کم‌اختیار پذیرفته نمی‌شود؛ دسترسی‌های اضافه را با مدیر سرور اصلاح کنید یا نام Runtime دیگری انتخاب کنید.");
                    }

                    await using var sidCommand = connection.CreateCommand();
                    sidCommand.CommandText = """
                        SELECT CAST(COUNT(*) AS int)
                        FROM sys.database_principals dp
                        WHERE dp.name = @login AND dp.type IN ('S', 'U')
                          AND dp.sid = (SELECT sp.sid FROM sys.server_principals sp WHERE sp.name = @login AND sp.type = 'S')
                        """;
                    sidCommand.Parameters.Add(new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
                    var sameSid = await ExecuteScalarAsync(sidCommand, CancellationToken.None) is int sidCount && sidCount > 0;
                    if (!sameSid)
                    {
                        throw new VendorDatabaseMismatchException($"کاربر هم‌نام «{input.RuntimeUsername}» در دیتابیس هدف به Login دیگری تعلق دارد؛ برای جلوگیری از اعطای دسترسی به هویت اشتباه، عملیات متوقف شد. نام Runtime دیگری انتخاب کنید یا mapping موجود را با مدیر سرور اصلاح کنید.");
                    }
                }
                else
                {
                    await using var createUser = connection.CreateCommand();
                    createUser.CommandText = $"CREATE USER {VendorDevelopmentDatabaseSafety.VendorSqlIdentifier.Quote(input.RuntimeUsername)} FOR LOGIN {VendorDevelopmentDatabaseSafety.VendorSqlIdentifier.Quote(input.RuntimeUsername)}";
                    await createUser.ExecuteNonQueryAsync(CancellationToken.None);
                }

                return true;
            }, CancellationToken.None);

            if (reusedExistingLogin)
            {
                // Ownership proof BEFORE any role grant: a real login attempt with the
                // credentials the operator supplied. A wrong password leaves the existing
                // login and its pre-existing mapping untouched.
                if (!await RuntimeLoginCanConnectAsync(input))
                {
                    await RollBackUserMappingAsync(input, logger, mappingPreExisted);
                    throw new VendorDatabaseMismatchException($"اتصال با گذرواژهٔ ارائه‌شده برای «{input.RuntimeUsername}» تأیید نشد؛ مالکیت Login موجود اثبات نشد. گذرواژهٔ درست را وارد کنید یا نام Runtime جدیدی انتخاب کنید؛ گذرواژهٔ Login موجود تغییر نمی‌کند و دسترسی تازه‌ای به آن داده نمی‌شود.");
                }

                logger.LogInformation("Vendor development reuses the existing runtime login after a successful credential proof.");
            }

            // Role grants come after the (fresh creation or) successful ownership proof. For
            // the fresh path there is nothing to prove: this flow created the login itself.
            await OpenServerConnectionAsync(TargetBuilder(input), async connection =>
            {
                foreach (var role in new[] { VendorReaderRole, VendorWriterRole })
                {
                    await using var membershipCommand = connection.CreateCommand();
                    membershipCommand.CommandText = "SELECT IS_ROLEMEMBER(@role, @login)";
                    membershipCommand.Parameters.Add(new SqlParameter("@role", System.Data.SqlDbType.NVarChar, 128) { Value = role });
                    membershipCommand.Parameters.Add(new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
                    var membership = await ExecuteScalarAsync(membershipCommand, CancellationToken.None);
                    // IS_ROLEMEMBER answers with int 1/0 (NULL when the pair is unresolved);
                    // treat anything but int 1 as "not a member".
                    if (!IsMembershipTrue(membership))
                    {
                        // sp_addrolemember keeps the role and member names fully parameterized;
                        // no dynamic SQL is needed for the fixed database roles.
                        await using var addMember = connection.CreateCommand();
                        addMember.CommandText = "EXEC sys.sp_addrolemember @rolename = @role, @membername = @member";
                        addMember.Parameters.Add(new SqlParameter("@role", System.Data.SqlDbType.NVarChar, 128) { Value = role });
                        addMember.Parameters.Add(new SqlParameter("@member", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
                        await addMember.ExecuteNonQueryAsync(CancellationToken.None);
                    }
                }

                return true;
            }, CancellationToken.None);

            logger.LogInformation(
                "Vendor development runtime identity ensured (fresh login: {FreshLogin}).",
                !reusedExistingLogin);
            return true;
        }, CancellationToken.None);

        return createdFresh;
    }

    private static bool IsMembershipTrue(object? membership) =>
        membership is 1 || string.Equals(membership as string, "1", StringComparison.Ordinal);

    /// <summary>
    /// Removes a login (and its mapping) that THIS run created when a later stage fails:
    /// the generated password is shown once and would otherwise be lost, leaving an
    /// unusable login behind. Only principals created by this attempt are removed.
    /// </summary>
    private static async Task RollBackFreshLoginAsync(ProvisionInput input, ILogger logger)
    {
        try
        {
            await RollBackUserMappingAsync(input, logger, mappingPreExisted: false);
            await OpenServerConnectionAsync(input.SetupBuilder, async connection =>
            {
                await using var guardCommand = connection.CreateCommand();
                guardCommand.CommandText = "SELECT CAST(COUNT(*) AS int) FROM sys.server_principals WHERE name = @login AND type = 'S'";
                guardCommand.Parameters.Add(new SqlParameter("@login", System.Data.SqlDbType.NVarChar, 128) { Value = input.RuntimeUsername });
                var loginExists = await ExecuteScalarAsync(guardCommand, CancellationToken.None) is int existingLogins && existingLogins > 0;
                if (!loginExists)
                {
                    return true;
                }

                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP LOGIN {VendorDevelopmentDatabaseSafety.VendorSqlIdentifier.Quote(input.RuntimeUsername)}";
                await command.ExecuteNonQueryAsync(CancellationToken.None);
                return true;
            }, CancellationToken.None);
            logger.LogWarning("Vendor development removed the freshly created runtime login after a later-stage failure.");
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException or TimeoutException)
        {
            logger.LogWarning(exception, "Vendor development could not remove the freshly created runtime login after a later-stage failure.");
        }
    }

    private static async Task RollBackUserMappingAsync(ProvisionInput input, ILogger logger, bool mappingPreExisted)
    {
        if (mappingPreExisted)
        {
            // The user mapping existed before this run: never remove something we did not add.
            return;
        }

        try
        {
            await OpenServerConnectionAsync(TargetBuilder(input), async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP USER IF EXISTS {VendorDevelopmentDatabaseSafety.VendorSqlIdentifier.Quote(input.RuntimeUsername)}";
                await command.ExecuteNonQueryAsync(CancellationToken.None);
                return true;
            }, CancellationToken.None);
            logger.LogInformation("Vendor development reverted the runtime user mapping after a failed credential proof.");
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException or TimeoutException)
        {
            logger.LogWarning(exception, "Vendor development could not revert the runtime user mapping after a failed credential proof.");
        }
    }

    private static async Task<bool> RuntimeLoginCanConnectAsync(ProvisionInput input)
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
            // Every ownership proof must be a real login: a pooled connection from an earlier
            // proof would bypass authentication entirely.
            Pooling = false,
            ApplicationName = "Faranou Vendor Development Runtime Proof"
        };
        try
        {
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(CancellationToken.None);
            return true;
        }
        catch (SqlException)
        {
            // Any refused login (wrong password, denied access) fails the ownership proof.
            return false;
        }
    }

    private static async Task VerifyReadinessAsync(ProvisionInput input, string runtimeConnectionString)
    {
        string? actualDatabase = null;
        string? actualLogin = null;
        var isDbOwner = false;
        await OpenServerConnectionAsync(new SqlConnectionStringBuilder(runtimeConnectionString) { Pooling = false }, async connection =>
        {
            await using var databaseNameCommand = connection.CreateCommand();
            databaseNameCommand.CommandText = "SELECT DB_NAME()";
            actualDatabase = await ExecuteScalarAsync(databaseNameCommand, CancellationToken.None) as string;
            await using var loginNameCommand = connection.CreateCommand();
            loginNameCommand.CommandText = "SELECT SUSER_SNAME()";
            actualLogin = await ExecuteScalarAsync(loginNameCommand, CancellationToken.None) as string;
            // IS_ROLEMEMBER returns '1'/'0' as varchar(2) for the current user.
            await using var ownerMembershipCommand = connection.CreateCommand();
            ownerMembershipCommand.CommandText = "SELECT IS_ROLEMEMBER('db_owner')";
            isDbOwner = IsMembershipTrue(await ExecuteScalarAsync(ownerMembershipCommand, CancellationToken.None));
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

        // The effective write/read path the application needs must really be available: a
        // db_denydatareader/db_denydatawriter membership would silently break the app after
        // readiness reported success.
        await OpenServerConnectionAsync(new SqlConnectionStringBuilder(runtimeConnectionString) { Pooling = false }, async connection =>
        {
            await using var denyReaderCommand = connection.CreateCommand();
            denyReaderCommand.CommandText = "SELECT IS_ROLEMEMBER('db_denydatareader')";
            var denyReader = IsMembershipTrue(await ExecuteScalarAsync(denyReaderCommand, CancellationToken.None));
            await using var denyWriterCommand = connection.CreateCommand();
            denyWriterCommand.CommandText = "SELECT IS_ROLEMEMBER('db_denydatawriter')";
            var denyWriter = IsMembershipTrue(await ExecuteScalarAsync(denyWriterCommand, CancellationToken.None));
            if (denyReader || denyWriter)
            {
                throw new InvalidOperationException("هویت Runtime عضو نقش‌های db_denydatareader/db_denydatawriter است؛ دسترسی مؤثر خواندن/نوشتن برقرار نیست و readiness گزارش نمی‌شود.");
            }

            return true;
        }, CancellationToken.None);

        // Schema check with the application model, through the runtime identity: pending
        // migrations must be zero after stage 5 succeeded. The readiness connections use no
        // pooling so they never leave authenticated sessions behind (a rollback after a
        // failed verify can then drop the login without waiting for pool retirement).
        var readinessBuilder = new SqlConnectionStringBuilder(runtimeConnectionString) { Pooling = false };
        await using var context = CreateDbContext(readinessBuilder.ConnectionString);
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

    internal static string DescribeSqlError(Exception exception) => exception switch
    {
        SqlException sql => DescribeSqlError(sql.Number),
        TimeoutException => "عملیات از مهلت مجاز عبور کرد.",
        _ => "عملیات روی SQL Server ناموفق بود؛ جزئیات در لاگ برنامه ثبت شد."
    };

    /// <summary>
    /// Maps a SQL Server error number to a fixed Persian guidance message. The exception's
    /// own text (user names, hosts, connection details) is never included in the answer.
    /// </summary>
    internal static string DescribeSqlError(int sqlErrorNumber) => sqlErrorNumber switch
    {
        18456 => "ورود به SQL Server رد شد؛ نام کاربری و گذرواژهٔ هویت پیکربندی را بررسی کنید.",
        18452 => "احراز هویت ویندوز از این نشست پذیرفته نشد.",
        4060 or 916 => "دیتابیس هدف در دسترس نیست؛ نام دیتابیس و دسترسی کاربر را بررسی کنید.",
        262 or 229 or 15247 => "دسترسی لازم برای این عملیات داده نشد؛ هویت پیکربندی باید دسترسی مدیریتی روی سرور داشته باشد.",
        5170 or 5133 or 5120 or 1802 => "ایجاد فایل‌های دیتابیس روی سرور ناموفق بود.",
        1105 or 9002 => "فضای دیسک سرور برای این عملیات کافی نیست.",
        -2 => "عملیات از مهلت مجاز عبور کرد.",
        53 or 2 or 10060 or 11001 => "سرور SQL در دسترس نیست؛ نام سرور، شبکه و پورت را بررسی کنید.",
        26 or 40 => "نمونهٔ SQL Server پیدا نشد؛ نام نمونه را بررسی کنید.",
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
    /// Cryptographically random runtime password over an unambiguous character set. Quote,
    /// backslash, semicolon and space are excluded so the value stays copy-paste safe in
    /// connection strings and operator notes. Generation is only ever used for a login this
    /// flow creates itself; an existing login is never assigned a generated password.
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
