using System.Text.RegularExpressions;
using Faranou.Vendor.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Faranou.Vendor.Host;

/// <summary>
/// Development-only Vendor database provisioning: creates the dedicated Vendor database when
/// it does not exist, applies the existing Vendor.Persistence migrations, and prepares a
/// least-privileged runtime identity. The provisioning (administrative) credential is supplied
/// per request, lives only in memory for that request, and is never persisted, logged, or
/// echoed. The runtime identity is the connection the operator later tests and saves through
/// the existing development setup surface; it must never be a sysadmin principal.
/// </summary>
internal static class VendorDevelopmentDatabaseProvisioning
{
    private const string ProvisioningLockResourcePrefix = "FaranouVendorDevelopmentProvisioning:";
    private const string ProvisioningApplicationName = "Faranou Vendor Development Provisioning";
    private const string RuntimeApplicationName = "Faranou Vendor Development Setup";
    private const int MigrationCommandTimeoutSeconds = 300;

    private static readonly Regex SafeIdentifier = new(
        @"^[A-Za-z_][A-Za-z0-9_]{0,127}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WindowsLoginPart = new(
        @"^[A-Za-z0-9._-]{1,104}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> ReservedDatabaseNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "master",
        "model",
        "msdb",
        "tempdb"
    };

    public static bool IsValidDatabaseIdentifier(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        SafeIdentifier.IsMatch(value) &&
        !ReservedDatabaseNames.Contains(value);

    public static bool IsValidPrincipalIdentifier(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        SafeIdentifier.IsMatch(value) &&
        !string.Equals(value, "sa", StringComparison.OrdinalIgnoreCase);

    public static bool IsValidWindowsLoginIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('\\');
        if (parts.Length is < 1 or > 2)
        {
            return false;
        }

        return parts.All(part => WindowsLoginPart.IsMatch(part));
    }

    /// <summary>
    /// Bracket-quotes a strictly validated identifier. Validation above already excludes every
    /// character that could break out of the brackets; this remains defense in depth.
    /// </summary>
    public static string QuoteIdentifier(string value) =>
        "[" + value.Replace("]", "]]", StringComparison.Ordinal) + "]";

    public static string? Validate(ProvisioningRequest request)
    {
        if (request.Authentication is not ("windows" or "sql") ||
            request.RuntimeAuthentication is not ("windows" or "sql"))
        {
            return "روش ورود مدیریتی یا اجرا معتبر نیست.";
        }

        if (string.IsNullOrWhiteSpace(request.Server) || request.Server.Length > 256)
        {
            return "آدرس SQL Server معتبر نیست.";
        }

        if (!IsValidDatabaseIdentifier(request.Database))
        {
            return "نام دیتابیس معتبر نیست؛ فقط حروف لاتین، عدد و خط زیر مجاز است و نام دیتابیس‌های سیستمی پذیرفته نمی‌شود.";
        }

        if (request.Authentication == "sql" &&
            (string.IsNullOrWhiteSpace(request.Username) ||
             string.IsNullOrEmpty(request.Password) ||
             request.Username.Length > 256 ||
             request.Password.Length > 1024))
        {
            return "برای ورود مدیریتی SQL نام کاربری و گذرواژهٔ معتبر لازم است.";
        }

        if (request.RuntimeAuthentication == "sql")
        {
            if (!IsValidPrincipalIdentifier(request.RuntimeUsername ?? string.Empty))
            {
                return "نام کاربری اجرای SQL معتبر نیست؛ فقط حروف لاتین، عدد و خط زیر مجاز است و استفاده از sa ممنوع است.";
            }

            if (string.IsNullOrEmpty(request.RuntimePassword) || request.RuntimePassword.Length > 1024)
            {
                return "برای آزمون و آماده‌سازی هویت اجرای SQL گذرواژه لازم است.";
            }
        }

        if (request.CreateRuntimeLogin && request.RuntimeAuthentication == "windows")
        {
            return "ساخت نام کاربری اجرا فقط برای ورود SQL معنی دارد؛ برای احراز هویت ویندوز از حساب جاری استفاده می‌شود.";
        }

        return null;
    }

    public static string BuildAdminConnectionString(ProvisioningRequest request, string initialCatalog)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = request.Server.Trim(),
            InitialCatalog = initialCatalog,
            IntegratedSecurity = string.Equals(request.Authentication, "windows", StringComparison.Ordinal),
            Encrypt = true,
            TrustServerCertificate = request.TrustServerCertificate,
            ConnectTimeout = 8,
            PersistSecurityInfo = false,
            ApplicationName = ProvisioningApplicationName
        };

        if (!builder.IntegratedSecurity)
        {
            builder.UserID = request.Username!.Trim();
            builder.Password = request.Password;
        }

        return builder.ConnectionString;
    }

    public static string BuildRuntimeConnectionString(ProvisioningRequest request, string initialCatalog)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = request.Server.Trim(),
            InitialCatalog = initialCatalog,
            IntegratedSecurity = string.Equals(request.RuntimeAuthentication, "windows", StringComparison.Ordinal),
            Encrypt = true,
            TrustServerCertificate = request.TrustServerCertificate,
            ConnectTimeout = 8,
            PersistSecurityInfo = false,
            ApplicationName = RuntimeApplicationName
        };

        if (!builder.IntegratedSecurity)
        {
            builder.UserID = request.RuntimeUsername!.Trim();
            builder.Password = request.RuntimePassword;
        }

        return builder.ConnectionString;
    }

    public static async Task<ProvisionCheckResult> CheckAsync(
        ProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return ProvisionCheckResult.Failed(validationError);
        }

        DatabaseInspection inspection;
        try
        {
            inspection = await InspectAsync(request, cancellationToken);
        }
        catch (SqlException exception)
        {
            return ProvisionCheckResult.Failed(SafeMessageForSqlError(exception, "check"));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return ProvisionCheckResult.Failed("اطلاعات اتصال مدیریتی معتبر نیست.");
        }

        var message = inspection.State switch
        {
            "missing" => "اتصال مدیریتی برقرار شد؛ دیتابیس هدف موجود نیست و می‌توان آن را ایجاد کرد.",
            "empty" => "دیتابیس هدف موجود است و خالی است؛ آمادهٔ اجرای مهاجرت‌ها.",
            "vendor" => inspection.PendingCount > 0
                ? $"دیتابیس Vendor معتبر است؛ {inspection.PendingCount} مهاجرت باقی‌مانده دارد."
                : "دیتابیس Vendor معتبر است و همهٔ مهاجرت‌ها اعمال شده‌اند.",
            _ => "دیتابیس هدف جدول‌ها یا تاریخچهٔ مهاجرت ناشناس دارد و به‌عنوان دیتابیس Vendor قابل تشخیص نیست؛ برای ایمنی هیچ تغییری اعمال نمی‌شود."
        };

        return new ProvisionCheckResult(
            true,
            message,
            ServerReachable: true,
            inspection.State,
            inspection.UserTableCount,
            inspection.AppliedCount,
            inspection.ExpectedCount,
            inspection.PendingCount);
    }

    public static async Task<ProvisionCreateResult> CreateAsync(
        ProvisioningRequest request,
        CancellationToken cancellationToken,
        ILogger logger)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return ProvisionCreateResult.Failed(validationError);
        }

        var masterConnectionString = BuildAdminConnectionString(request, "master");
        SqlConnection? lockConnection = null;
        try
        {
            lockConnection = new SqlConnection(masterConnectionString);
            await lockConnection.OpenAsync(cancellationToken);
            if (!await TryAcquireLockAsync(lockConnection, request.Database, cancellationToken))
            {
                return ProvisionCreateResult.Failed("عملیات راه‌اندازی دیگری روی همین دیتابیس در جریان است؛ چند لحظه بعد تلاش کنید.");
            }

            var databaseNameParameter = new SqlParameter("@databaseName", System.Data.SqlDbType.NVarChar, 128)
            {
                Value = request.Database
            };

            await using (var existsCommand = lockConnection.CreateCommand())
            {
                existsCommand.CommandText = "SELECT DB_ID(@databaseName)";
                existsCommand.Parameters.Add(databaseNameParameter);
                if (await existsCommand.ExecuteScalarAsync(cancellationToken) is not null and not DBNull)
                {
                    // The database already exists. It is never dropped or recreated; the caller
                    // sees the classification through the check stage.
                    return new ProvisionCreateResult(
                        true,
                        "دیتابیس هدف از قبل موجود است؛ ایجاد جدید انجام نشد.",
                        Created: false,
                        AlreadyExisted: true);
                }
            }

            // DDL identifiers cannot be parameterized in T-SQL. The database name passed the
            // strict ASCII identifier validation and is bracket-quoted server-side by QUOTENAME,
            // so no operator input can break out of the identifier token.
            await using (var createCommand = lockConnection.CreateCommand())
            {
                createCommand.CommandText =
                    """
                    DECLARE @sql nvarchar(300) = N'CREATE DATABASE ' + QUOTENAME(@databaseName) + N';';
                    EXEC sp_executesql @sql;
                    """;
                createCommand.Parameters.Add(new SqlParameter("@databaseName", System.Data.SqlDbType.NVarChar, 128)
                {
                    Value = request.Database
                });
                await createCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            logger.LogInformation(
                "Vendor development provisioning created database. Stage: create; database identifier length: {Length}.",
                request.Database.Length);
            return new ProvisionCreateResult(
                true,
                "دیتابیس مستقل Vendor ایجاد شد.",
                Created: true,
                AlreadyExisted: false);
        }
        catch (SqlException exception)
        {
            logger.LogError(
                exception,
                "Vendor development provisioning stage failed. Stage: create; SQL error number: {Number}. Sensitive details omitted.",
                exception.Number);
            return ProvisionCreateResult.Failed(SafeMessageForSqlError(exception, "create"));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return ProvisionCreateResult.Failed("اطلاعات اتصال مدیریتی معتبر نیست.");
        }
        finally
        {
            if (lockConnection is not null)
            {
                await ReleaseLockAsync(lockConnection, request.Database);
                await lockConnection.DisposeAsync();
            }
        }
    }

    public static async Task<ProvisionMigrateResult> MigrateAsync(
        ProvisioningRequest request,
        CancellationToken cancellationToken,
        ILogger logger)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return ProvisionMigrateResult.Failed(validationError);
        }

        DatabaseInspection inspection;
        try
        {
            inspection = await InspectAsync(request, cancellationToken);
        }
        catch (SqlException exception)
        {
            logger.LogError(
                exception,
                "Vendor development provisioning stage failed. Stage: migrate-inspect; SQL error number: {Number}. Sensitive details omitted.",
                exception.Number);
            return ProvisionMigrateResult.Failed(SafeMessageForSqlError(exception, "migrate"));
        }

        if (inspection.State == "missing")
        {
            return ProvisionMigrateResult.Failed("دیتابیس هدف هنوز ایجاد نشده است؛ ابتدا مرحلهٔ ایجاد دیتابیس را اجرا کنید.");
        }

        if (inspection.State == "foreign")
        {
            return ProvisionMigrateResult.Failed(
                "دیتابیس هدف جدول‌ها یا تاریخچهٔ مهاجرت ناشناس دارد و به‌عنوان دیتابیس Vendor قابل تشخیص نیست؛ برای ایمنی مهاجرت اجرا نشد.");
        }

        var masterConnectionString = BuildAdminConnectionString(request, "master");
        var targetConnectionString = BuildAdminConnectionString(request, request.Database);
        SqlConnection? lockConnection = null;
        try
        {
            lockConnection = new SqlConnection(masterConnectionString);
            await lockConnection.OpenAsync(cancellationToken);
            if (!await TryAcquireLockAsync(lockConnection, request.Database, cancellationToken))
            {
                return ProvisionMigrateResult.Failed("عملیات راه‌اندازی دیگری روی همین دیتابیس در جریان است؛ چند لحظه بعد تلاش کنید.");
            }

            var appliedBefore = inspection.AppliedCount;
            var appliedNow = 0;
            await using (var database = CreateVendorDatabase(targetConnectionString))
            {
                var migrator = database.GetService<IMigrator>();
                await migrator.MigrateAsync(null, cancellationToken);

                var appliedAfter = (await database.Database.GetAppliedMigrationsAsync(cancellationToken))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var expected = database.Database.GetMigrations().ToHashSet(StringComparer.OrdinalIgnoreCase);
                appliedNow = Math.Max(0, appliedAfter.Count - appliedBefore);
                var historyVerified = expected.Count > 0 &&
                    !expected.Except(appliedAfter, StringComparer.OrdinalIgnoreCase).Any();

                if (!historyVerified)
                {
                    return ProvisionMigrateResult.Failed(
                        "مهاجرت‌ها اجرا شد اما تأیید تاریخچهٔ مهاجرت ناموفق بود؛ برای ایمنی ادامه متوقف شد.");
                }

                logger.LogInformation(
                    "Vendor development provisioning applied migrations. Stage: migrate; applied now: {AppliedNow}; total: {Total}.",
                    appliedNow,
                    appliedAfter.Count);
                return new ProvisionMigrateResult(
                    true,
                    appliedNow > 0
                        ? $"مهاجرت‌های Vendor با موفقیت اجرا شد؛ تعداد اعمال‌شده در این اجرا: {appliedNow}."
                        : "همهٔ مهاجرت‌های Vendor از قبل اعمال شده بودند؛ مهاجرت جدیدی اجرا نشد.",
                    appliedNow,
                    appliedAfter.Count,
                    expected.Count,
                    true);
            }
        }
        catch (SqlException exception)
        {
            logger.LogError(
                exception,
                "Vendor development provisioning stage failed. Stage: migrate; SQL error number: {Number}. Sensitive details omitted.",
                exception.Number);
            return ProvisionMigrateResult.Failed(SafeMessageForSqlError(exception, "migrate"));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return ProvisionMigrateResult.Failed("اطلاعات اتصال مدیریتی معتبر نیست.");
        }
        finally
        {
            if (lockConnection is not null)
            {
                await ReleaseLockAsync(lockConnection, request.Database);
                await lockConnection.DisposeAsync();
            }
        }
    }

    public static async Task<ProvisionRuntimeResult> GrantRuntimeAccessAsync(
        ProvisioningRequest request,
        CancellationToken cancellationToken,
        ILogger logger)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return ProvisionRuntimeResult.Failed(validationError);
        }

        var runtimeLoginName = request.RuntimeAuthentication == "windows"
            ? CurrentWindowsLogin
            : request.RuntimeUsername!.Trim();

        if (request.RuntimeAuthentication == "windows" && !IsValidWindowsLoginIdentifier(runtimeLoginName))
        {
            return ProvisionRuntimeResult.Failed("نام حساب ویندوز برای هویت اجرا قابل تشخیص نیست.");
        }

        var masterConnectionString = BuildAdminConnectionString(request, "master");
        var targetConnectionString = BuildAdminConnectionString(request, request.Database);
        SqlConnection? lockConnection = null;
        try
        {
            lockConnection = new SqlConnection(masterConnectionString);
            await lockConnection.OpenAsync(cancellationToken);
            if (!await TryAcquireLockAsync(lockConnection, request.Database, cancellationToken))
            {
                return ProvisionRuntimeResult.Failed("عملیات راه‌اندازی دیگری روی همین دیتابیس در جریان است؛ چند لحظه بعد تلاش کنید.");
            }

            await using (var existsCommand = lockConnection.CreateCommand())
            {
                existsCommand.CommandText = "SELECT DB_ID(@databaseName)";
                existsCommand.Parameters.Add(new SqlParameter("@databaseName", System.Data.SqlDbType.NVarChar, 128)
                {
                    Value = request.Database
                });
                if (await existsCommand.ExecuteScalarAsync(cancellationToken) is null or DBNull)
                {
                    return ProvisionRuntimeResult.Failed("دیتابیس هدف هنوز ایجاد نشده است؛ ابتدا مرحلهٔ ایجاد دیتابیس را اجرا کنید.");
                }
            }

            var loginNameParameter = new SqlParameter("@loginName", System.Data.SqlDbType.NVarChar, 256)
            {
                Value = runtimeLoginName
            };
            bool loginExisted;
            bool loginCreated = false;

            await using (var loginCommand = lockConnection.CreateCommand())
            {
                loginCommand.CommandText = "SELECT SUSER_ID(@loginName)";
                loginCommand.Parameters.Add(loginNameParameter);
                loginExisted = await loginCommand.ExecuteScalarAsync(cancellationToken) is not null and not DBNull;
            }

            if (!loginExisted)
            {
                if (request.RuntimeAuthentication == "sql")
                {
                    if (!request.CreateRuntimeLogin)
                    {
                        return ProvisionRuntimeResult.Failed(
                            "نام کاربری اجرای SQL در سرور موجود نیست؛ گزینهٔ ساخت نام کاربری اجرا را فعال کنید یا ابتدا آن را ایجاد کنید.");
                    }

                    // T-SQL cannot parameterize the password inside CREATE LOGIN. The identifier
                    // passed strict ASCII validation and is bracket-quoted server-side by
                    // QUOTENAME; the password is quoted server-side by QUOTENAME with a quote
                    // delimiter, which escapes embedded quotes safely. No credential is ever
                    // written to source, logs, or responses.
                    await using var createLoginCommand = lockConnection.CreateCommand();
                    createLoginCommand.CommandText =
                        """
                        DECLARE @sql nvarchar(600) =
                            N'CREATE LOGIN ' + QUOTENAME(@loginName) +
                            N' WITH PASSWORD = ' + QUOTENAME(@password, '''') +
                            N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';
                        EXEC sp_executesql @sql;
                        """;
                    createLoginCommand.Parameters.Add(new SqlParameter("@loginName", System.Data.SqlDbType.NVarChar, 256)
                    {
                        Value = runtimeLoginName
                    });
                    createLoginCommand.Parameters.Add(new SqlParameter("@password", System.Data.SqlDbType.NVarChar, 128)
                    {
                        Value = request.RuntimePassword
                    });
                    await createLoginCommand.ExecuteNonQueryAsync(cancellationToken);
                    loginCreated = true;
                }
                else
                {
                    await using var createWindowsLoginCommand = lockConnection.CreateCommand();
                    createWindowsLoginCommand.CommandText =
                        """
                        DECLARE @sql nvarchar(600) = N'CREATE LOGIN ' + QUOTENAME(@loginName) + N' FROM WINDOWS;';
                        EXEC sp_executesql @sql;
                        """;
                    createWindowsLoginCommand.Parameters.Add(new SqlParameter("@loginName", System.Data.SqlDbType.NVarChar, 256)
                    {
                        Value = runtimeLoginName
                    });
                    await createWindowsLoginCommand.ExecuteNonQueryAsync(cancellationToken);
                    loginCreated = true;
                }

                logger.LogInformation(
                    "Vendor development provisioning prepared the runtime login. Stage: runtime; login created: {Created}.",
                    loginCreated);
            }

            // The runtime identity must never be a sysadmin principal. This check runs before
            // any grant so a rejected identity leaves no changes behind.
            await using (var sysadminCommand = lockConnection.CreateCommand())
            {
                sysadminCommand.CommandText = "SELECT IS_SRVROLEMEMBER('sysadmin', @loginName)";
                sysadminCommand.Parameters.Add(new SqlParameter("@loginName", System.Data.SqlDbType.NVarChar, 256)
                {
                    Value = runtimeLoginName
                });
                var sysadminResult = await sysadminCommand.ExecuteScalarAsync(cancellationToken);
                if (sysadminResult is not null and not DBNull && Convert.ToInt32(sysadminResult) == 1)
                {
                    return ProvisionRuntimeResult.Failed(request.RuntimeAuthentication == "sql"
                        ? "این نام کاربری روی سرور sysadmin است و به‌عنوان هویت اجرا پذیرفته نمی‌شود؛ نام کاربری محدود دیگری انتخاب کنید."
                        : "حساب ویندوز جاری روی این SQL Server sysadmin است؛ برای جداسازی مجوزها گزینهٔ ساخت نام کاربری SQL محدود را انتخاب کنید.");
                }
            }

            await using var targetConnection = new SqlConnection(targetConnectionString);
            await targetConnection.OpenAsync(cancellationToken);

            bool userCreated = false;
            await using (var userCommand = targetConnection.CreateCommand())
            {
                userCommand.CommandText =
                    """
                    IF DATABASE_PRINCIPAL_ID(@loginName) IS NULL
                    BEGIN
                        DECLARE @sql nvarchar(600) =
                            N'CREATE USER ' + QUOTENAME(@loginName) + N' FOR LOGIN ' + QUOTENAME(@loginName) + N';';
                        EXEC sp_executesql @sql;
                        SELECT 1;
                    END
                    ELSE
                    BEGIN
                        SELECT 0;
                    END
                    """;
                userCommand.Parameters.Add(new SqlParameter("@loginName", System.Data.SqlDbType.NVarChar, 256)
                {
                    Value = runtimeLoginName
                });
                var userResult = await userCommand.ExecuteScalarAsync(cancellationToken);
                userCreated = userResult is 1;
            }

            var rolesGranted = await EnsureRoleMembershipAsync(targetConnection, runtimeLoginName, "db_datareader", cancellationToken) &&
                await EnsureRoleMembershipAsync(targetConnection, runtimeLoginName, "db_datawriter", cancellationToken);
            if (!rolesGranted)
            {
                return ProvisionRuntimeResult.Failed("اعطای مجوزهای دادهٔ اجرا ناموفق بود؛ مجوز حساب Provisioning را بررسی کنید.");
            }

            await using (var verifyConnection = new SqlConnection(BuildRuntimeConnectionString(request, request.Database)))
            {
                await verifyConnection.OpenAsync(cancellationToken);
                await using var databaseNameCommand = verifyConnection.CreateCommand();
                databaseNameCommand.CommandText = "SELECT DB_NAME()";
                var databaseName = await databaseNameCommand.ExecuteScalarAsync(cancellationToken) as string;
                if (!string.Equals(databaseName, request.Database, StringComparison.OrdinalIgnoreCase))
                {
                    return ProvisionRuntimeResult.Failed("اتصال اجرا برقرار شد اما دیتابیس هدف تأیید نشد.");
                }
            }

            return new ProvisionRuntimeResult(
                true,
                loginCreated
                    ? "هویت اجرای محدود ساخته و به دیتابیس Vendor متصل شد؛ فقط خواندن و نوشتن داده در همین دیتابیس مجاز است و sysadmin نیست."
                    : "دسترسی اجرا روی دیتابیس Vendor آماده شد؛ مجوزهای داده اعطا شد و هویت اجرا sysadmin نیست.",
                loginExisted,
                loginCreated,
                userCreated,
                rolesGranted,
                RuntimeVerified: true,
                RuntimeIsSysadmin: false);
        }
        catch (SqlException exception)
        {
            logger.LogError(
                exception,
                "Vendor development provisioning stage failed. Stage: runtime; SQL error number: {Number}. Sensitive details omitted.",
                exception.Number);
            return ProvisionRuntimeResult.Failed(SafeMessageForSqlError(exception, "runtime"));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return ProvisionRuntimeResult.Failed("اطلاعات اتصال مدیریتی یا اجرا معتبر نیست.");
        }
        finally
        {
            if (lockConnection is not null)
            {
                await ReleaseLockAsync(lockConnection, request.Database);
                await lockConnection.DisposeAsync();
            }
        }
    }

    private static string CurrentWindowsLogin =>
        $"{Environment.UserDomainName}\\{Environment.UserName}";

    private static VendorDbContext CreateVendorDatabase(string targetConnectionString)
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseSqlServer(
                targetConnectionString,
                sql =>
                {
                    sql.MigrationsAssembly(typeof(VendorDbContext).Assembly.FullName);
                    sql.CommandTimeout(MigrationCommandTimeoutSeconds);
                })
            .Options;

        return new VendorDbContext(options);
    }

    private static async Task<DatabaseInspection> InspectAsync(
        ProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        var masterConnectionString = BuildAdminConnectionString(request, "master");
        await using var masterConnection = new SqlConnection(masterConnectionString);
        await masterConnection.OpenAsync(cancellationToken);

        await using var existsCommand = masterConnection.CreateCommand();
        existsCommand.CommandText = "SELECT DB_ID(@databaseName)";
        existsCommand.Parameters.Add(new SqlParameter("@databaseName", System.Data.SqlDbType.NVarChar, 128)
        {
            Value = request.Database
        });
        if (await existsCommand.ExecuteScalarAsync(cancellationToken) is null or DBNull)
        {
            return new DatabaseInspection(true, false, "missing", 0, 0, 0, 0, 0);
        }

        var targetConnectionString = BuildAdminConnectionString(request, request.Database);
        await using var targetConnection = new SqlConnection(targetConnectionString);
        await targetConnection.OpenAsync(cancellationToken);

        int userTableCount;
        await using (var tablesCommand = targetConnection.CreateCommand())
        {
            tablesCommand.CommandText =
                "SELECT COUNT(*) FROM sys.tables WHERE name <> N'__EFMigrationsHistory' AND is_ms_shipped = 0";
            userTableCount = Convert.ToInt32(await tablesCommand.ExecuteScalarAsync(cancellationToken));
        }

        await using var database = CreateVendorDatabase(targetConnectionString);
        var applied = (await database.Database.GetAppliedMigrationsAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expected = database.Database.GetMigrations().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = applied.Except(expected, StringComparer.OrdinalIgnoreCase).ToList();
        var pending = expected.Except(applied, StringComparer.OrdinalIgnoreCase).ToList();

        var state = unknown.Count > 0 || (userTableCount > 0 && applied.Count == 0)
            ? "foreign"
            : applied.Count == 0 && userTableCount == 0
                ? "empty"
                : "vendor";

        return new DatabaseInspection(
            true,
            true,
            state,
            userTableCount,
            applied.Count,
            expected.Count,
            unknown.Count,
            pending.Count);
    }

    private static async Task<bool> TryAcquireLockAsync(
        SqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', " +
            "@LockOwner = 'Session', @LockTimeout = 0; SELECT @r;";
        command.Parameters.Add(new SqlParameter("@resource", System.Data.SqlDbType.NVarChar, 256)
        {
            Value = ProvisioningLockResourcePrefix + databaseName
        });
        var result = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        return result >= 0;
    }

    private static async Task ReleaseLockAsync(SqlConnection connection, string databaseName)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC sp_releaseapplock @Resource = @resource, @LockOwner = 'Session';";
            command.Parameters.Add(new SqlParameter("@resource", System.Data.SqlDbType.NVarChar, 256)
            {
                Value = ProvisioningLockResourcePrefix + databaseName
            });
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception)
        {
            // The lock is bound to this connection; disposing the connection releases it.
        }
    }

    private static async Task<bool> EnsureRoleMembershipAsync(
        SqlConnection targetConnection,
        string principalName,
        string roleName,
        CancellationToken cancellationToken)
    {
        await using var command = targetConnection.CreateCommand();
        command.CommandText =
            """
            IF IS_ROLEMEMBER(@roleName, @principalName) <> 1
            BEGIN
                DECLARE @sql nvarchar(600) = N'ALTER ROLE ' + QUOTENAME(@roleName) + N' ADD MEMBER ' + QUOTENAME(@principalName) + N';';
                EXEC sp_executesql @sql;
                SELECT 1;
            END
            ELSE
            BEGIN
                SELECT 0;
            END
            """;
        command.Parameters.Add(new SqlParameter("@roleName", System.Data.SqlDbType.NVarChar, 128)
        {
            Value = roleName
        });
        command.Parameters.Add(new SqlParameter("@principalName", System.Data.SqlDbType.NVarChar, 256)
        {
            Value = principalName
        });
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    internal static string SafeMessageForSqlError(SqlException exception, string stage) => exception.Number switch
    {
        18456 => "ورود به SQL Server با اطلاعات Provisioning رد شد؛ نام کاربری و گذرواژهٔ مدیریتی را بررسی کنید.",
        4060 => "دیتابیس هدف در دسترس نیست؛ نام دیتابیس و مجوزهای مدیریتی را بررسی کنید.",
        1801 => "دیتابیسی با همین نام از قبل موجود است؛ برای ادامه مرحلهٔ بررسی وضعیت را اجرا کنید.",
        15025 => "نام کاربری SQL با همین نام از قبل موجود است.",
        15023 => "کاربر با همین نام در دیتابیس موجود است.",
        15401 => "حساب ویندوز در SQL Server یافت نشد یا ایجاد login ویندوز ناموفق بود.",
        15151 => "تغییر نقش‌های دیتابیس با مجوز فعلی مجاز نیست؛ حساب Provisioning باید مجوز کافی روی سرور داشته باشد.",
        229 => "حساب Provisioning مجوز لازم روی این دیتابیس را ندارد.",
        _ => $"مرحلهٔ «{stage}» با خطای SQL مواجه شد؛ برای جلوگیری از افشای اطلاعات، جزئیات ثبت نمی‌شود."
    };

    private sealed record DatabaseInspection(
        bool ServerReachable,
        bool DatabaseExists,
        string State,
        int UserTableCount,
        int AppliedCount,
        int ExpectedCount,
        int UnknownMigrationCount,
        int PendingCount);

    internal sealed record ProvisionCheckResult(
        bool Success,
        string Message,
        bool ServerReachable,
        string? DatabaseState,
        int UserTableCount,
        int AppliedMigrations,
        int ExpectedMigrations,
        int PendingMigrations)
    {
        internal static ProvisionCheckResult Failed(string message) =>
            new(false, message, false, null, 0, 0, 0, 0);
    }

    internal sealed record ProvisionCreateResult(bool Success, string Message, bool Created, bool AlreadyExisted)
    {
        internal static ProvisionCreateResult Failed(string message) => new(false, message, false, false);
    }

    internal sealed record ProvisionMigrateResult(
        bool Success,
        string Message,
        int AppliedNow,
        int TotalApplied,
        int ExpectedTotal,
        bool HistoryVerified)
    {
        internal static ProvisionMigrateResult Failed(string message) =>
            new(false, message, 0, 0, 0, false);
    }

    internal sealed record ProvisionRuntimeResult(
        bool Success,
        string Message,
        bool LoginExisted,
        bool LoginCreated,
        bool UserCreated,
        bool RolesGranted,
        bool RuntimeVerified,
        bool RuntimeIsSysadmin)
    {
        internal static ProvisionRuntimeResult Failed(string message) =>
            new(false, message, false, false, false, false, false, false);
    }
}

internal sealed record ProvisioningRequest(
    string Server,
    string Database,
    string Authentication,
    string? Username,
    string? Password,
    bool TrustServerCertificate,
    string RuntimeAuthentication,
    string? RuntimeUsername,
    string? RuntimePassword,
    bool CreateRuntimeLogin);
