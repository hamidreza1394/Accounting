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
            string.IsNullOrWhiteSpace(request.Database))
        {
            return DatabaseSetupResult.Failure("نام سرور و نام پایگاه داده الزامی است.");
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

            var masterConnectionString = builder.ConnectionString;
            var databaseName = request.Database.Trim();
            var databaseCreated = false;

            await using (var connection = new SqlConnection(masterConnectionString))
            {
                await connection.OpenAsync(cancellationToken);

                await using var checkCommand = new SqlCommand("SELECT DB_ID(@databaseName);", connection);
                checkCommand.Parameters.AddWithValue("@databaseName", databaseName);
                var databaseId = await checkCommand.ExecuteScalarAsync(cancellationToken);

                if (databaseId is null or DBNull)
                {
                    if (!request.CreateDatabaseIfMissing)
                    {
                        return DatabaseSetupResult.Failure(
                            "اتصال به SQL Server موفق بود، اما دیتابیس وجود ندارد. گزینه ساخت دیتابیس را فعال کنید.");
                    }

                    // The database identifier is quoted by SQL Server's QUOTENAME; it is never concatenated raw.
                    await using var createCommand = new SqlCommand(
                        "DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@databaseName); EXEC sys.sp_executesql @sql;",
                        connection);
                    createCommand.Parameters.AddWithValue("@databaseName", databaseName);
                    await createCommand.ExecuteNonQueryAsync(cancellationToken);
                    databaseCreated = true;
                }
            }

            builder.InitialCatalog = databaseName;
            var databaseConnectionString = builder.ConnectionString;

            if (databaseCreated)
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
                "Database setup completed. DatabaseCreated={DatabaseCreated}; connection details are not logged.",
                databaseCreated);

            return DatabaseSetupResult.Success(databaseCreated);
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

public sealed record DatabaseSetupResult(bool Succeeded, bool DatabaseCreated, string? ErrorMessage)
{
    public static DatabaseSetupResult Success(bool databaseCreated) => new(true, databaseCreated, null);
    public static DatabaseSetupResult Failure(string message) => new(false, false, message);
}
