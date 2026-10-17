using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Server.Infrastructure.DatabaseSetup;

public sealed class DatabaseConnectionSetupService
{
    private readonly DatabaseConnectionSettingsStore _settingsStore;
    private readonly ILogger<DatabaseConnectionSetupService> _logger;

    public DatabaseConnectionSetupService(
        DatabaseConnectionSettingsStore settingsStore,
        ILogger<DatabaseConnectionSetupService> logger)
    {
        _settingsStore = settingsStore;
        _logger = logger;
    }

    public async Task<DatabaseSetupResult> ConfigureAsync(
        DatabaseSetupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Server) ||
            string.IsNullOrWhiteSpace(request.Database) ||
            request.Database.Trim().Length > 128)
        {
            return DatabaseSetupResult.Failure("نام سرور و نام پایگاه داده الزامی است؛ نام دیتابیس حداکثر ۱۲۸ نویسه است.");
        }

        var requestedDatabaseName = request.Database.Trim();
        if (requestedDatabaseName.Equals("master", StringComparison.OrdinalIgnoreCase) ||
            requestedDatabaseName.Equals("model", StringComparison.OrdinalIgnoreCase) ||
            requestedDatabaseName.Equals("msdb", StringComparison.OrdinalIgnoreCase) ||
            requestedDatabaseName.Equals("tempdb", StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseSetupResult.Failure("انتخاب دیتابیس‌های سیستمی SQL Server مجاز نیست.");
        }

        if (!string.Equals(request.AuthenticationMode, "Windows", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.AuthenticationMode, "Sql", StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseSetupResult.Failure("نوع احراز هویت انتخاب‌شده معتبر نیست.");
        }

        if (string.Equals(request.AuthenticationMode, "Sql", StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrEmpty(request.Password)))
        {
            return DatabaseSetupResult.Failure("برای ورود SQL Server، نام کاربری و رمز عبور لازم است.");
        }

        try
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = request.Server.Trim(),
                InitialCatalog = "master",
                IntegratedSecurity = string.Equals(request.AuthenticationMode, "Windows", StringComparison.OrdinalIgnoreCase),
                Encrypt = true,
                TrustServerCertificate = request.TrustServerCertificate,
                ConnectTimeout = 10,
                MultipleActiveResultSets = true,
                PersistSecurityInfo = false,
                ApplicationName = "Taraz Ava Database Setup"
            };

            if (!builder.IntegratedSecurity)
            {
                builder.UserID = request.UserName.Trim();
                builder.Password = request.Password;
            }

            var databaseName = request.Database.Trim();
            var databaseCreated = false;

            await using (var masterConnection = new SqlConnection(builder.ConnectionString))
            {
                await masterConnection.OpenAsync(cancellationToken);

                await using var checkCommand = new SqlCommand("SELECT DB_ID(@databaseName);", masterConnection);
                checkCommand.Parameters.AddWithValue("@databaseName", databaseName);
                var databaseId = await checkCommand.ExecuteScalarAsync(cancellationToken);

                if (databaseId is null or DBNull)
                {
                    if (!request.CreateDatabaseIfMissing)
                    {
                        return DatabaseSetupResult.Failure(
                            "اتصال به SQL Server موفق بود، اما دیتابیس وجود ندارد. گزینه ساخت دیتابیس را فعال کنید.");
                    }

                    // QUOTENAME quotes the identifier on the server; the user input is a SQL parameter.
                    await using var createCommand = new SqlCommand(
                        "DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@databaseName); EXEC sys.sp_executesql @sql;",
                        masterConnection);
                    createCommand.Parameters.AddWithValue("@databaseName", databaseName);
                    await createCommand.ExecuteNonQueryAsync(cancellationToken);
                    databaseCreated = true;
                }
            }

            builder.InitialCatalog = databaseName;
            var databaseConnectionString = builder.ConnectionString;
            var databaseNeedsInitialMigration = false;

            if (!databaseCreated)
            {
                await using var databaseConnection = new SqlConnection(databaseConnectionString);
                await databaseConnection.OpenAsync(cancellationToken);
                await using var emptyDatabaseCommand = new SqlCommand(
                    "SELECT CASE WHEN OBJECT_ID(N'__EFMigrationsHistory', N'U') IS NULL AND NOT EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped = 0) THEN 1 ELSE 0 END;",
                    databaseConnection);
                databaseNeedsInitialMigration = Convert.ToInt32(
                    await emptyDatabaseCommand.ExecuteScalarAsync(cancellationToken)) == 1;
            }

            var migrationsApplied = databaseCreated || databaseNeedsInitialMigration;
            if (migrationsApplied)
            {
                var options = new DbContextOptionsBuilder<Data.DatabaseContext>()
                    .UseSqlServer(
                        databaseConnectionString,
                        sqlServer => sqlServer.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName))
                    .Options;

                await using var context = new Data.DatabaseContext(options);
                await context.Database.MigrateAsync(cancellationToken);
            }

            _settingsStore.SaveConnectionString(databaseConnectionString);

            _logger.LogInformation(
                "Database setup completed. MigrationsApplied={MigrationsApplied}; connection details are not logged.",
                migrationsApplied);

            return DatabaseSetupResult.Success(migrationsApplied);
        }
        catch (SqlException exception)
        {
            _logger.LogWarning(
                "SQL Server setup failed with SQL error number {SqlErrorNumber}; sensitive connection details were not logged.",
                exception.Number);
            return DatabaseSetupResult.Failure(
                "اتصال یا عملیات پایگاه داده ناموفق بود. سرور، مجوزها و تنظیمات رمزنگاری اتصال را بررسی کنید. کد خطا: " +
                exception.Number);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or CryptographicException or InvalidOperationException)
        {
            _logger.LogError(exception, "Database setup failed; connection string contents were not logged.");
            return DatabaseSetupResult.Failure(
                "ذخیره تنظیمات یا آماده‌سازی دیتابیس ناموفق بود. دسترسی پوشه برنامه و گزارش خطای سرور را بررسی کنید.");
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Database setup failed with exception type {ExceptionType}; sensitive connection details were not logged.",
                exception.GetType().Name);
            return DatabaseSetupResult.Failure(
                "راه‌اندازی دیتابیس ناموفق بود. گزارش فنی سرور را بررسی کنید؛ اطلاعات اتصال در پیام خطا نمایش داده نمی‌شود.");
        }
    }
}

public sealed class DatabaseSetupRequest
{
    public string Server { get; init; } = string.Empty;
    public string Database { get; init; } = string.Empty;
    public string AuthenticationMode { get; init; } = "Windows";
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public bool TrustServerCertificate { get; init; }
    public bool CreateDatabaseIfMissing { get; init; } = true;
}

public sealed record DatabaseSetupResult(bool Succeeded, bool MigrationsApplied, string? ErrorMessage)
{
    public static DatabaseSetupResult Success(bool migrationsApplied) => new(true, migrationsApplied, null);
    public static DatabaseSetupResult Failure(string message) => new(false, false, message);
}
