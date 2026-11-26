using Faranou.Vendor.Host;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Development-only Vendor database provisioning: identifier safety, request validation and,
/// when a disposable SQL Server is available, the full create/migrate/runtime pipeline with
/// least-privilege verification. No customer database, schema or migration is touched.
/// </summary>
public sealed class VendorDevelopmentDatabaseProvisioningTests
{
    private const string TestDatabasePrefix = "FaranouVendorTests_";

    // ---------- unit tests (no SQL Server required) ----------

    [Fact]
    public void DatabaseIdentifierAcceptsOnlyStrictVendorNames()
    {
        Assert.True(VendorDevelopmentDatabaseProvisioning.IsValidDatabaseIdentifier("VendorDb"));
        Assert.True(VendorDevelopmentDatabaseProvisioning.IsValidDatabaseIdentifier(TestDatabasePrefix + Guid.NewGuid().ToString("N")));

        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidDatabaseIdentifier(string.Empty));
        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidDatabaseIdentifier("Vendor Db"));
        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidDatabaseIdentifier("VendorDb;DROP DATABASE x"));
        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidDatabaseIdentifier("VendorDb]"));
        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidDatabaseIdentifier("[VendorDb]"));
        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidDatabaseIdentifier("دیتابیس"));
        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidDatabaseIdentifier("master"));
        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidDatabaseIdentifier("MASTER"));
        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidDatabaseIdentifier("tempdb"));
        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidDatabaseIdentifier(new string('a', 129)));
        Assert.True(VendorDevelopmentDatabaseProvisioning.IsValidDatabaseIdentifier(new string('a', 128)));
    }

    [Fact]
    public void PrincipalIdentifierRejectsSaAndUnsafeNames()
    {
        Assert.True(VendorDevelopmentDatabaseProvisioning.IsValidPrincipalIdentifier("FaranouVendorRuntime_1"));

        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidPrincipalIdentifier("sa"));
        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidPrincipalIdentifier("SA"));
        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidPrincipalIdentifier(string.Empty));
        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidPrincipalIdentifier("runtime; DROP LOGIN x"));
        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidPrincipalIdentifier("runtime user"));
    }

    [Fact]
    public void WindowsLoginIdentifierAcceptsOnlyAccountShapes()
    {
        Assert.True(VendorDevelopmentDatabaseProvisioning.IsValidWindowsLoginIdentifier("hamid"));
        Assert.True(VendorDevelopmentDatabaseProvisioning.IsValidWindowsLoginIdentifier("DESKTOP-ULV0A9A\\hamid"));

        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidWindowsLoginIdentifier(string.Empty));
        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidWindowsLoginIdentifier("a\\b\\c"));
        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidWindowsLoginIdentifier("DESKTOP-ULV0A9A\\ham id"));
        Assert.False(VendorDevelopmentDatabaseProvisioning.IsValidWindowsLoginIdentifier("DESKTOP-ULV0A9A\\hamid; DROP LOGIN x"));
    }

    [Fact]
    public void QuoteIdentifierEscapesClosingBracket()
    {
        Assert.Equal("[VendorDb]", VendorDevelopmentDatabaseProvisioning.QuoteIdentifier("VendorDb"));
        Assert.Equal("[Ven]]t]", VendorDevelopmentDatabaseProvisioning.QuoteIdentifier("Ven]t"));
    }

    [Fact]
    public void ValidateProducesPersianMessagesForInvalidRequests()
    {
        var valid = new ProvisioningRequest(
            "localhost",
            "VendorDb",
            "windows",
            null,
            null,
            false,
            "windows",
            null,
            null,
            false);
        Assert.Null(VendorDevelopmentDatabaseProvisioning.Validate(valid));

        Assert.Contains("مدیریتی", VendorDevelopmentDatabaseProvisioning.Validate(valid with
        {
            Authentication = "sql",
            Username = "sa",
            Password = null
        })!, StringComparison.Ordinal);

        Assert.Contains("sa", VendorDevelopmentDatabaseProvisioning.Validate(valid with
        {
            RuntimeAuthentication = "sql",
            RuntimeUsername = "sa",
            RuntimePassword = "anything"
        })!, StringComparison.Ordinal);

        Assert.Contains("معنی دارد", VendorDevelopmentDatabaseProvisioning.Validate(valid with
        {
            CreateRuntimeLogin = true
        })!, StringComparison.Ordinal);

        Assert.Contains("دیتابیس", VendorDevelopmentDatabaseProvisioning.Validate(valid with
        {
            Database = "master"
        })!, StringComparison.Ordinal);

        Assert.Contains("گذرواژه", VendorDevelopmentDatabaseProvisioning.Validate(valid with
        {
            RuntimeAuthentication = "sql",
            RuntimeUsername = "RuntimeLogin",
            RuntimePassword = string.Empty
        })!, StringComparison.Ordinal);
    }

    // ---------- SQL Server integration tests (CI: ephemeral SQL Server 2022) ----------

    [Fact]
    public async Task ProvisioningPipelineCreatesMigratesAndRestrictsRuntimeIdentity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var baseConnectionString = RequireSqlServer();
        var baseBuilder = new SqlConnectionStringBuilder(baseConnectionString);
        Assert.Contains("localhost", baseBuilder.DataSource, StringComparison.OrdinalIgnoreCase);

        var databaseName = $"{TestDatabasePrefix}{Guid.NewGuid():N}";
        var runtimeLoginName = $"FaranouVendorRuntime_{Guid.NewGuid():N}";
        var runtimePassword = $"Vp{Guid.NewGuid():N}"[..16] + "!Aa1#";
        var logger = NullLogger.Instance;

        try
        {
            var checkMissing = await VendorDevelopmentDatabaseProvisioning.CheckAsync(
                NewRequest(baseBuilder, databaseName, null, null, false), cancellationToken);
            Assert.True(checkMissing.Success);
            Assert.Equal("missing", checkMissing.DatabaseState);

            var create = await VendorDevelopmentDatabaseProvisioning.CreateAsync(
                NewRequest(baseBuilder, databaseName, null, null, false),
                cancellationToken,
                logger);
            Assert.True(create.Success);
            Assert.True(create.Created);
            Assert.False(create.AlreadyExisted);

            var checkEmpty = await VendorDevelopmentDatabaseProvisioning.CheckAsync(
                NewRequest(baseBuilder, databaseName, null, null, false), cancellationToken);
            Assert.Equal("empty", checkEmpty.DatabaseState);
            Assert.Equal(0, checkEmpty.UserTableCount);

            var migrate = await VendorDevelopmentDatabaseProvisioning.MigrateAsync(
                NewRequest(baseBuilder, databaseName, null, null, false),
                cancellationToken,
                logger);
            Assert.True(migrate.Success, migrate.Message);
            Assert.True(migrate.HistoryVerified);
            Assert.Equal(migrate.ExpectedTotal, migrate.TotalApplied);
            Assert.True(migrate.ExpectedTotal > 0);

            var runtime = await VendorDevelopmentDatabaseProvisioning.GrantRuntimeAccessAsync(
                NewRequest(baseBuilder, databaseName, runtimeLoginName, runtimePassword, true),
                cancellationToken,
                logger);
            Assert.True(runtime.Success, runtime.Message);
            Assert.True(runtime.LoginCreated);
            Assert.False(runtime.LoginExisted);
            Assert.True(runtime.RuntimeVerified);
            Assert.False(runtime.RuntimeIsSysadmin);

            await VerifyRuntimeLeastPrivilegeAsync(baseBuilder, databaseName, runtimeLoginName, runtimePassword, cancellationToken);

            var createAgain = await VendorDevelopmentDatabaseProvisioning.CreateAsync(
                NewRequest(baseBuilder, databaseName, null, null, false),
                cancellationToken,
                logger);
            Assert.True(createAgain.Success);
            Assert.False(createAgain.Created);
            Assert.True(createAgain.AlreadyExisted);

            var migrateAgain = await VendorDevelopmentDatabaseProvisioning.MigrateAsync(
                NewRequest(baseBuilder, databaseName, null, null, false),
                cancellationToken,
                logger);
            Assert.True(migrateAgain.Success, migrateAgain.Message);
            Assert.Equal(0, migrateAgain.AppliedNow);
            Assert.True(migrateAgain.HistoryVerified);

            var checkFinal = await VendorDevelopmentDatabaseProvisioning.CheckAsync(
                NewRequest(baseBuilder, databaseName, null, null, false), cancellationToken);
            Assert.Equal("vendor", checkFinal.DatabaseState);
            Assert.Equal(0, checkFinal.PendingMigrations);
        }
        finally
        {
            await DropTestDatabaseAsync(baseBuilder.ConnectionString, databaseName, cancellationToken);
            await DropTestLoginAsync(baseBuilder.ConnectionString, runtimeLoginName, cancellationToken);
        }
    }

    [Fact]
    public async Task ProvisioningMigrateRefusesForeignDatabaseWithoutTouchingIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var baseConnectionString = RequireSqlServer();
        var baseBuilder = new SqlConnectionStringBuilder(baseConnectionString);
        var databaseName = $"{TestDatabasePrefix}{Guid.NewGuid():N}";

        try
        {
            await CreateTestDatabaseAsync(baseBuilder.ConnectionString, databaseName, cancellationToken);
            await using (var connection = new SqlConnection(
                new SqlConnectionStringBuilder(baseBuilder.ConnectionString) { InitialCatalog = databaseName }.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE dbo.ForeignProbe (Id int NOT NULL)";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            var migrate = await VendorDevelopmentDatabaseProvisioning.MigrateAsync(
                NewRequest(baseBuilder, databaseName, null, null, false),
                cancellationToken,
                NullLogger.Instance);
            Assert.False(migrate.Success);
            Assert.Contains("ناشناس", migrate.Message, StringComparison.Ordinal);
            Assert.False(migrate.HistoryVerified);

            var check = await VendorDevelopmentDatabaseProvisioning.CheckAsync(
                NewRequest(baseBuilder, databaseName, null, null, false), cancellationToken);
            Assert.Equal("foreign", check.DatabaseState);
        }
        finally
        {
            await DropTestDatabaseAsync(baseBuilder.ConnectionString, databaseName, cancellationToken);
        }
    }

    [Fact]
    public async Task ProvisioningCheckMapsLoginFailureToSafePersianMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var baseConnectionString = RequireSqlServer();
        var baseBuilder = new SqlConnectionStringBuilder(baseConnectionString);
        var databaseName = $"{TestDatabasePrefix}{Guid.NewGuid():N}";

        var request = NewRequest(baseBuilder, databaseName, null, null, false) with
        {
            Username = baseBuilder.UserID,
            Password = $"{baseBuilder.Password}x"
        };

        try
        {
            var check = await VendorDevelopmentDatabaseProvisioning.CheckAsync(request, cancellationToken);
            Assert.False(check.Success);
            Assert.Contains("ورود به SQL Server", check.Message, StringComparison.Ordinal);
        }
        finally
        {
            await DropTestDatabaseAsync(baseBuilder.ConnectionString, databaseName, cancellationToken);
        }
    }

    private static ProvisioningRequest NewRequest(
        SqlConnectionStringBuilder baseBuilder,
        string databaseName,
        string? runtimeLogin,
        string? runtimePassword,
        bool createRuntimeLogin) => new(
            Server: baseBuilder.DataSource,
            Database: databaseName,
            Authentication: "sql",
            Username: baseBuilder.UserID,
            Password: baseBuilder.Password,
            TrustServerCertificate: true,
            RuntimeAuthentication: runtimeLogin is null ? "windows" : "sql",
            RuntimeUsername: runtimeLogin,
            RuntimePassword: runtimePassword,
            CreateRuntimeLogin: createRuntimeLogin);

    private static async Task VerifyRuntimeLeastPrivilegeAsync(
        SqlConnectionStringBuilder baseBuilder,
        string databaseName,
        string runtimeLoginName,
        string runtimePassword,
        CancellationToken cancellationToken)
    {
        var runtimeBuilder = new SqlConnectionStringBuilder(baseBuilder.ConnectionString)
        {
            InitialCatalog = databaseName,
            UserID = runtimeLoginName,
            Password = runtimePassword,
            Encrypt = true,
            TrustServerCertificate = true,
            ApplicationName = "FaranouVendorTests"
        };

        await using (var connection = new SqlConnection(runtimeBuilder.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory";
            Assert.True(Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0);
        }

        // The runtime identity must not hold DDL rights: migrations stay provisioning-only.
        await using (var connection = new SqlConnection(runtimeBuilder.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE dbo.ProbeTable (Id int NOT NULL)";
            await Assert.ThrowsAnyAsync<SqlException>(() => command.ExecuteNonQueryAsync(cancellationToken));
        }

        // The runtime identity must not reach other databases on the same server.
        await using (var masterConnection = new SqlConnection(
            new SqlConnectionStringBuilder(runtimeBuilder.ConnectionString) { InitialCatalog = "master" }.ConnectionString))
        {
            await Assert.ThrowsAnyAsync<SqlException>(() => masterConnection.OpenAsync(cancellationToken));
        }
    }

    private static string RequireSqlServer()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (!string.IsNullOrWhiteSpace(baseConnectionString))
        {
            return baseConnectionString;
        }

        const string message = "Set FARANO_VENDOR_TEST_SQLSERVER to a disposable SQL Server connection. Use FARANO_REQUIRE_VENDOR_SQLSERVER=1 to require this integration test in CI.";
        if (string.Equals(Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER"), "1", StringComparison.Ordinal))
        {
            Assert.Fail(message);
        }

        Assert.Skip(message);
        return string.Empty;
    }

    private static void ValidateTestIdentifier(string value, string prefix)
    {
        if (!value.StartsWith(prefix, StringComparison.Ordinal) ||
            value.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new InvalidOperationException("Refusing to mutate a non-test identifier.");
        }
    }

    private static async Task CreateTestDatabaseAsync(
        string baseConnectionString,
        string databaseName,
        CancellationToken cancellationToken)
    {
        ValidateTestIdentifier(databaseName, TestDatabasePrefix);
        var masterConnectionString = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = "master" }.ConnectionString;
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DropTestDatabaseAsync(
        string baseConnectionString,
        string databaseName,
        CancellationToken cancellationToken)
    {
        ValidateTestIdentifier(databaseName, TestDatabasePrefix);
        var masterConnectionString = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = "master" }.ConnectionString;
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DropTestLoginAsync(
        string baseConnectionString,
        string loginName,
        CancellationToken cancellationToken)
    {
        ValidateTestIdentifier(loginName, "FaranouVendorRuntime_");
        var masterConnectionString = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = "master" }.ConnectionString;
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF SUSER_ID(N'{loginName}') IS NOT NULL DROP LOGIN [{loginName}]";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
