using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Real SQL Server cycle for the Development full provisioning flow: create the dedicated
/// Vendor database, apply the existing EF migrations, create a least-privilege SQL runtime
/// identity, verify readiness, and stay idempotent and safe on re-runs and foreign databases.
/// The critical regression guarantees: an existing runtime login is never password-reset or
/// auto-enabled, and reuse requires the operator to prove the login with its own credentials.
/// Runs only when FARANO_VENDOR_TEST_SQLSERVER points at a disposable local SQL Server;
/// the pipeline itself never drops anything, so the fixture cleans up its own objects.
/// </summary>
public sealed class VendorDevelopmentDatabaseProvisioningSqlServerTests
{
    [Fact]
    public async Task FullPipelineProvisionsDatabaseMigrationsAndRuntimeIdentity()
    {
        await using var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        var runtimePassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        var request = fixture.CreateRequest(runtimePassword: runtimePassword);

        var outcome = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            request, NullLogger.Instance, [], TestContext.Current.CancellationToken);
        var states = outcome.StageResults.ToDictionary(stage => stage.Key, stage => stage.State);

        Assert.True(outcome.Success, outcome.Message);
        Assert.Equal("success", states["verify"]);
        Assert.Equal("success", states["createDatabase"]);
        Assert.Null(outcome.GeneratedPassword);
        Assert.Equal(fixture.RuntimeLoginName, outcome.RuntimeUsername);

        // Schema exists through migrations and the runtime identity holds exactly reader/writer.
        Assert.True(await fixture.DatabaseExistsAsync());
        Assert.True(await fixture.HasMigrationsHistoryAsync());
        Assert.True(await fixture.HasOwnershipMarkerAsync());
        Assert.True(await fixture.LoginExistsAsync(fixture.RuntimeLoginName));
        Assert.True(await fixture.IsMemberAsync(fixture.RuntimeLoginName, "db_datareader"));
        Assert.True(await fixture.IsMemberAsync(fixture.RuntimeLoginName, "db_datawriter"));
        Assert.False(await fixture.IsMemberAsync(fixture.RuntimeLoginName, "db_owner"));
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, runtimePassword));

        // Re-run is idempotent: the database is kept, migrations stay applied, the existing
        // login is reused through its own credentials (proof, not reset) and readiness verifies.
        var rerun = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            fixture.CreateRequest(runtimePassword: runtimePassword),
            NullLogger.Instance,
            [],
            TestContext.Current.CancellationToken);
        var rerunStates = rerun.StageResults.ToDictionary(stage => stage.Key, stage => stage.State);

        Assert.True(rerun.Success, rerun.Message);
        Assert.Equal("skipped", rerunStates["createDatabase"]);
        Assert.Equal("success", rerunStates["verify"]);
        Assert.Null(rerun.GeneratedPassword);
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, runtimePassword));
    }

    [Fact]
    public async Task FreshLoginCanBeCreatedWithAGeneratedPassword()
    {
        await using var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        // No runtime password supplied: the flow generates one for the login it creates.
        var outcome = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            fixture.CreateRequest(), NullLogger.Instance, [], TestContext.Current.CancellationToken);
        var states = outcome.StageResults.ToDictionary(stage => stage.Key, stage => stage.State);

        Assert.True(outcome.Success, outcome.Message);
        Assert.NotNull(outcome.GeneratedPassword);
        Assert.True(await fixture.LoginExistsAsync(fixture.RuntimeLoginName));
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, outcome.GeneratedPassword!));
    }

    [Fact]
    public async Task ExistingRuntimeLoginPasswordIsNeverResetAndReuseRequiresItsOwnCredentials()
    {
        await using var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        var originalPassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        var first = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            fixture.CreateRequest(runtimePassword: originalPassword),
            NullLogger.Instance, [], TestContext.Current.CancellationToken);
        Assert.True(first.Success, first.Message);

        // Regression guard (3.1): a re-run with a DIFFERENT password must NOT reset the
        // existing login; it fails closed instead and the original password keeps working.
        var attackerPassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        var rerunWithDifferentPassword = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            fixture.CreateRequest(runtimePassword: attackerPassword),
            NullLogger.Instance, [], TestContext.Current.CancellationToken);
        var rerunStates = rerunWithDifferentPassword.StageResults.ToDictionary(stage => stage.Key, stage => stage.State);

        Assert.False(rerunWithDifferentPassword.Success);
        Assert.Equal("failed", rerunStates["runtimeIdentity"]);
        Assert.Contains("تغییر نمی", rerunStates["runtimeIdentity"] + rerunWithDifferentPassword.Message, StringComparison.Ordinal);
        Assert.False(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, attackerPassword));
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, originalPassword));
        Assert.Null(rerunWithDifferentPassword.GeneratedPassword);

        // A re-run with NO password (generation requested) must also fail closed.
        var rerunWithGeneration = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            fixture.CreateRequest(), NullLogger.Instance, [], TestContext.Current.CancellationToken);
        Assert.False(rerunWithGeneration.Success);
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, originalPassword));
    }

    [Fact]
    public async Task PipelineFailsFastForSystemDatabaseNames()
    {
        await using var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        var request = fixture.CreateRequest(databaseName: "master");

        var outcome = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            request, NullLogger.Instance, [], TestContext.Current.CancellationToken);
        var states = outcome.StageResults.ToDictionary(stage => stage.Key, stage => stage.State);

        Assert.False(outcome.Success);
        Assert.Equal("failed", states["inspect"]);
        Assert.Equal("pending", states["createDatabase"]);
    }

    [Fact]
    public async Task PipelineRefusesAnExistingEmptyDatabaseWithoutOwnershipProof()
    {
        await using var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        // An empty pre-created database may have been made for another purpose (for example
        // an empty Customer database); without ownership proof it is never adopted.
        await fixture.CreateEmptyForeignDatabaseAsync();

        var outcome = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            fixture.CreateRequest(databaseName: fixture.ForeignDatabaseName),
            NullLogger.Instance, [], TestContext.Current.CancellationToken);
        var states = outcome.StageResults.ToDictionary(stage => stage.Key, stage => stage.State);
        var createMessage = outcome.StageResults.First(stage => stage.Key == "createDatabase").Message;

        Assert.False(outcome.Success);
        Assert.Equal("failed", states["createDatabase"]);
        Assert.Equal("pending", states["migrate"]);
        Assert.Contains("مالکیت", createMessage + outcome.Message, StringComparison.Ordinal);
        Assert.False(await fixture.HasMigrationsHistoryAsync(fixture.ForeignDatabaseName));
        Assert.Equal(0, await fixture.UserTableCountAsync(fixture.ForeignDatabaseName));
    }

    [Fact]
    public async Task PipelineRecoversACrashWindowOrphanOnlyWithMatchingIntentEvidence()
    {
        await using var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await fixture.CreateEmptyForeignDatabaseAsync();

        // No intent record: the orphaned database stays anonymous and fail-closed.
        var withoutIntent = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            fixture.CreateRequest(databaseName: fixture.ForeignDatabaseName, runtimePassword: VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate()),
            NullLogger.Instance, [], TestContext.Current.CancellationToken);
        Assert.False(withoutIntent.Success);
        Assert.False(await fixture.HasOwnershipMarkerAsync(fixture.ForeignDatabaseName));

        // With a fresh intent recorded before the create and the create-date binding
        // satisfied, the controlled recovery writes the marker and continues the flow.
        var createdUtc = await fixture.ReadDatabaseCreatedUtcAsync(fixture.ForeignDatabaseName);
        Assert.NotNull(createdUtc);
        var intentIssuedUtc = createdUtc.Value.AddMinutes(-2);
        await fixture.WriteProvisioningIntentAsync(fixture.ForeignDatabaseName, intentIssuedUtc);

        var withIntent = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            fixture.CreateRequest(databaseName: fixture.ForeignDatabaseName, runtimePassword: VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate()),
            NullLogger.Instance, [], TestContext.Current.CancellationToken);
        var recoveredStates = withIntent.StageResults.ToDictionary(stage => stage.Key, stage => stage.State);

        Assert.True(withIntent.Success, withIntent.Message);
        Assert.Equal("skipped", recoveredStates["createDatabase"]);
        Assert.Equal("success", recoveredStates["migrate"]);
        Assert.True(await fixture.HasOwnershipMarkerAsync(fixture.ForeignDatabaseName));
        Assert.True(await fixture.HasMigrationsHistoryAsync(fixture.ForeignDatabaseName));
        Assert.Null(await fixture.ReadProvisioningIntentAsync());
    }

    [Fact]
    public async Task PipelineKeepsAcceptingLegacyVendorDatabasesByHistoryAndBackfillsMarker()
    {
        await using var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        var runtimePassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();

        var first = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            fixture.CreateRequest(runtimePassword: runtimePassword),
            NullLogger.Instance, [], TestContext.Current.CancellationToken);
        Assert.True(first.Success, first.Message);

        // Simulate a pre-marker Vendor database: history proves ownership, marker removed.
        await fixture.DropOwnershipMarkerAsync();
        Assert.False(await fixture.HasOwnershipMarkerAsync());

        var rerunPassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        var rerun = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            fixture.CreateRequest(runtimePassword: rerunPassword),
            NullLogger.Instance, [], TestContext.Current.CancellationToken);
        var rerunStates = rerun.StageResults.ToDictionary(stage => stage.Key, stage => stage.State);

        Assert.True(rerun.Success, rerun.Message);
        Assert.Equal("skipped", rerunStates["createDatabase"]);
        Assert.Equal("success", rerunStates["migrate"]);
        Assert.True(await fixture.HasOwnershipMarkerAsync());
    }

    [Fact]
    public async Task PipelineRefusesAnOverPrivilegedExistingRuntimeLogin()
    {
        await using var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        var runtimePassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();

        var first = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            fixture.CreateRequest(runtimePassword: runtimePassword),
            NullLogger.Instance, [], TestContext.Current.CancellationToken);
        Assert.True(first.Success, first.Message);

        // Simulate server-level drift: the existing login must be refused, not silently reused.
        await fixture.GrantViewAnyDatabaseAsync();
        var rerunPassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        var rerun = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            fixture.CreateRequest(runtimePassword: rerunPassword),
            NullLogger.Instance, [], TestContext.Current.CancellationToken);
        var rerunStates = rerun.StageResults.ToDictionary(stage => stage.Key, stage => stage.State);
        var identityMessage = rerun.StageResults.First(stage => stage.Key == "runtimeIdentity").Message;

        Assert.False(rerun.Success);
        Assert.Equal("failed", rerunStates["runtimeIdentity"]);
        Assert.Contains("سطح سرور", identityMessage, StringComparison.Ordinal);
        // The privileged login's password must not have been reset by the refused run.
        Assert.False(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, rerunPassword));
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, runtimePassword));
    }

    [Fact]
    public async Task PipelineRefusesToMigrateAUnknownDatabaseWithUserTables()
    {
        await using var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await fixture.CreateForeignDatabaseAsync();

        var outcome = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            fixture.CreateRequest(databaseName: fixture.ForeignDatabaseName, runtimePassword: VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate()),
            NullLogger.Instance, [], TestContext.Current.CancellationToken);
        var states = outcome.StageResults.ToDictionary(stage => stage.Key, stage => stage.State);
        var createMessage = outcome.StageResults.First(stage => stage.Key == "createDatabase").Message;

        Assert.False(outcome.Success);
        Assert.Equal("failed", states["createDatabase"]);
        Assert.Contains("جدول", createMessage + outcome.Message, StringComparison.Ordinal);
        Assert.False(await fixture.HasMigrationsHistoryAsync(fixture.ForeignDatabaseName));
    }

    private sealed class ProvisioningFixture : IAsyncDisposable
    {
        private readonly string _baseConnection;

        public string DatabaseName { get; } = $"FaranouVendorDevSetup_{Guid.NewGuid():N}"[..44];
        public string ForeignDatabaseName { get; } = $"FaranouVendorDevSetup_{Guid.NewGuid():N}"[..44];
        public string RuntimeLoginName { get; } = $"FaranouVendorRuntime_{Guid.NewGuid():N}"[..40];

        private ProvisioningFixture(string baseConnection) => _baseConnection = baseConnection;

        public static async Task<ProvisioningFixture?> OpenAsync()
        {
            var baseConnection = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
            if (string.IsNullOrWhiteSpace(baseConnection))
            {
                const string message = "Set FARANO_VENDOR_TEST_SQLSERVER to a disposable local SQL Server.";
                if (Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER") == "1")
                {
                    Assert.Fail(message);
                }

                Assert.Skip(message);
                return null;
            }

            return new ProvisioningFixture(baseConnection);
        }

        public ProvisionRequest CreateRequest(
            string? databaseName = null,
            string? runtimePassword = null,
            string? runtimeUsername = null)
        {
            var builder = new SqlConnectionStringBuilder(_baseConnection);
            return new ProvisionRequest(
                Server: builder.DataSource,
                Database: databaseName ?? DatabaseName,
                Authentication: string.IsNullOrWhiteSpace(builder.UserID) ? "windows" : "sql",
                Username: builder.UserID,
                Password: builder.Password,
                TrustServerCertificate: builder.TrustServerCertificate,
                RuntimeUsername: runtimeUsername ?? RuntimeLoginName,
                RuntimePassword: runtimePassword);
        }

        public async Task<bool> DatabaseExistsAsync(string? databaseName = null) =>
            await ScalarAsync("master", "SELECT COUNT(*) FROM sys.databases WHERE name = @name",
                new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = databaseName ?? DatabaseName }) is > 0;

        public async Task<bool> HasMigrationsHistoryAsync(string? databaseName = null) =>
            await ScalarAsync(databaseName ?? DatabaseName,
                "SELECT COUNT(*) FROM sys.tables WHERE name = '__EFMigrationsHistory'") is > 0;

        public async Task<bool> HasOwnershipMarkerAsync(string? databaseName = null) =>
            await ScalarAsync(databaseName ?? DatabaseName,
                "SELECT COUNT(*) FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = 'FaranouVendorOwnership'") is > 0;

        public async Task<int> UserTableCountAsync(string? databaseName = null) =>
            await ScalarAsync(databaseName ?? DatabaseName,
                "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0") is int count ? count : 0;

        public async Task<DateTimeOffset?> ReadDatabaseCreatedUtcAsync(string databaseName) =>
            await ScalarAsync("master", "SELECT create_date FROM sys.databases WHERE name = @name",
                new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = databaseName }) is DateTime created
                ? new DateTimeOffset(created, TimeSpan.Zero)
                : null;

        public async Task WriteProvisioningIntentAsync(string databaseName, DateTimeOffset issuedUtc)
        {
            await using var connection = new SqlConnection(TargetBuilder("master").ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = 'FaranouVendorProvisioningIntent')
                    EXEC sys.sp_updateextendedproperty @name = N'FaranouVendorProvisioningIntent', @value = @value;
                ELSE
                    EXEC sys.sp_addextendedproperty @name = N'FaranouVendorProvisioningIntent', @value = @value;
                """;
            command.Parameters.Add(new SqlParameter("@value", SqlDbType.NVarChar, 512)
            {
                Value = FormattableString.Invariant($"VendorDb={databaseName};IssuedUtc={issuedUtc:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}")
            });
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public async Task<string?> ReadProvisioningIntentAsync()
        {
            var result = await ScalarAsync("master",
                "SELECT CAST(value AS nvarchar(512)) FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = 'FaranouVendorProvisioningIntent'");
            return result as string;
        }

        private static string FormatIntentValue(string databaseName, DateTimeOffset issuedUtc) =>
            FormattableString.Invariant($"VendorDb={databaseName};IssuedUtc={issuedUtc:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}");

        public async Task DropOwnershipMarkerAsync(string? databaseName = null) =>
            await ExecuteAsync(databaseName ?? DatabaseName,
                "IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = 'FaranouVendorOwnership') EXEC sys.sp_dropextendedproperty @name = 'FaranouVendorOwnership';");

        public async Task GrantViewAnyDatabaseAsync() =>
            await ExecuteAsync("master", $"GRANT VIEW ANY DATABASE TO [{RuntimeLoginName.Replace("]", "]]", StringComparison.Ordinal)}]");

        public async Task CreateEmptyForeignDatabaseAsync() =>
            await ExecuteAsync("master", $"CREATE DATABASE [{ForeignDatabaseName.Replace("]", "]]", StringComparison.Ordinal)}]");

        public async Task<bool> LoginExistsAsync(string loginName) =>
            await ScalarAsync("master", "SELECT COUNT(*) FROM sys.server_principals WHERE name = @name AND type = 'S'",
                new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = loginName }) is > 0;

        public async Task<bool> IsMemberAsync(string loginName, string role)
        {
            // IS_ROLEMEMBER answers '1'/'0' as varchar(2); NULL means the pair could not be resolved.
            var membership = await ScalarAsync(DatabaseName, "SELECT IS_ROLEMEMBER(@role, @login)",
                new SqlParameter("@role", SqlDbType.NVarChar, 128) { Value = role },
                new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = loginName }) as string;
            return string.Equals(membership, "1", StringComparison.Ordinal);
        }

        public async Task<bool> RuntimeLoginCanConnectAsync(string loginName, string runtimePassword)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = new SqlConnectionStringBuilder(_baseConnection).DataSource,
                InitialCatalog = DatabaseName,
                UserID = loginName,
                Password = runtimePassword,
                Encrypt = true,
                TrustServerCertificate = new SqlConnectionStringBuilder(_baseConnection).TrustServerCertificate,
                ConnectTimeout = 15
            };
            try
            {
                await using var connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM VendorAdminUsers";
                var count = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
                return count is int;
            }
            catch (SqlException)
            {
                // A refused login (bad password, denied access) is a valid negative answer.
                return false;
            }
        }

        public async Task CreateForeignDatabaseAsync()
        {
            await ExecuteAsync("master", $"CREATE DATABASE [{ForeignDatabaseName.Replace("]", "]]", StringComparison.Ordinal)}]");
            await ExecuteAsync(ForeignDatabaseName, "CREATE TABLE dbo.ForeignMarker (Id int NOT NULL PRIMARY KEY)");
        }

        private static SqlConnectionStringBuilder TargetBuilder(string database) =>
            new(Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER")!)
            {
                InitialCatalog = database,
                ConnectTimeout = 15
            };

        private static async Task<object?> ScalarAsync(string database, string commandText, params SqlParameter[] parameters)
        {
            await using var connection = new SqlConnection(TargetBuilder(database).ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        }

        private static async Task ExecuteAsync(string database, string commandText)
        {
            await using var connection = new SqlConnection(TargetBuilder(database).ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                foreach (var database in new[] { DatabaseName, ForeignDatabaseName })
                {
                    await ExecuteAsync("master", $"DROP DATABASE IF EXISTS [{database.Replace("]", "]]", StringComparison.Ordinal)}]");
                }

                await ExecuteAsync("master", $"DROP LOGIN [{RuntimeLoginName.Replace("]", "]]", StringComparison.Ordinal)}]");
                await ExecuteAsync("master",
                    "IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = 'FaranouVendorProvisioningIntent') EXEC sys.sp_dropextendedproperty @name = 'FaranouVendorProvisioningIntent';");
            }
            catch (SqlException)
            {
                // Ignore: the CI server is ephemeral and discarded wholesale after the run.
            }
            finally
            {
                // Idle pooled connections can keep a dropped database alive; clear the pools.
                SqlConnection.ClearAllPools();
            }
        }
    }
}
