using System.Data;
using Microsoft.Data.SqlClient;
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
/// <summary>
/// The Development database provisioning SQL tests share one server-wide pipeline lock and
/// one provisioning intent slot, so they run sequentially (also relative to the safety tests).
/// </summary>
[CollectionDefinition("VendorDevelopmentDatabaseSequential", DisableParallelization = true)]
public sealed class VendorDevelopmentDatabaseSequentialCollection
{
}

[Collection("VendorDevelopmentDatabaseSequential")]
public sealed class VendorDevelopmentDatabaseProvisioningSqlServerTests
{
    /// <summary>
    /// Records the pipeline's logged warnings, including the underlying SQL exception, so a
    /// failing assertion can show the real error instead of only the sanitized stage message.
    /// </summary>
    private sealed class RecordingLogger : Microsoft.Extensions.Logging.ILogger
    {
        private readonly object _lock = new();
        private readonly List<string> _entries = [];

        public string Diagnostics
        {
            get
            {
                lock (_lock)
                {
                    return string.Join(Environment.NewLine + "---" + Environment.NewLine, _entries);
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_lock)
            {
                _entries.Add($"{formatter(state, exception)}{Environment.NewLine}{exception}");
            }
        }
    }

    private static string DescribeOutcome(
        VendorDevelopmentDatabaseProvisioning.ProvisionOutcome outcome,
        RecordingLogger logger)
    {
        var stages = string.Join(" | ", outcome.StageResults.Select(stage => $"{stage.Key}={stage.State}: {stage.Message}"));
        return $"{outcome.Message}{Environment.NewLine}stages: {stages}{Environment.NewLine}log:{Environment.NewLine}{logger.Diagnostics}";
    }

    /// <summary>Runs the pipeline and fails the test with full diagnostics when not successful.</summary>
    private static async Task<VendorDevelopmentDatabaseProvisioning.ProvisionOutcome> RunPipelineAsync(ProvisionRequest request)
    {
        var logger = new RecordingLogger();
        var outcome = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            request, logger, [], TestContext.Current.CancellationToken);
        Assert.True(outcome.Success, DescribeOutcome(outcome, logger));
        return outcome;
    }

    /// <summary>Runs the pipeline expecting a controlled failure; the outcome carries the refusal.</summary>
    private static async Task<(VendorDevelopmentDatabaseProvisioning.ProvisionOutcome Outcome, string StagesAndLog)>
        RunPipelineExpectingFailureAsync(ProvisionRequest request)
    {
        var logger = new RecordingLogger();
        var outcome = await VendorDevelopmentDatabaseProvisioning.RunAsync(
            request, logger, [], TestContext.Current.CancellationToken);
        var stages = string.Join(" | ", outcome.StageResults.Select(stage => $"{stage.Key}={stage.State}: {stage.Message}"));
        return (outcome, $"{outcome.Message}{Environment.NewLine}stages: {stages}{Environment.NewLine}log:{Environment.NewLine}{logger.Diagnostics}");
    }

    private static Dictionary<string, string> States(VendorDevelopmentDatabaseProvisioning.ProvisionOutcome outcome) =>
        outcome.StageResults.ToDictionary(stage => stage.Key, stage => stage.State);

    [Fact]
    public async Task FullPipelineProvisionsDatabaseMigrationsAndRuntimeIdentity()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        var runtimePassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        var request = fixture.CreateRequest(runtimePassword: runtimePassword);

        var outcome = await RunPipelineAsync(request);
        var states = States(outcome);

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
        var rerun = await RunPipelineAsync(fixture.CreateRequest(runtimePassword: runtimePassword));
        var rerunStates = States(rerun);

        Assert.Equal("skipped", rerunStates["createDatabase"]);
        Assert.Equal("success", rerunStates["verify"]);
        Assert.Null(rerun.GeneratedPassword);
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, runtimePassword));
    }

    [Fact]
    public async Task FreshLoginCanBeCreatedWithAGeneratedPassword()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        // No runtime password supplied: the flow generates one for the login it creates.
        var outcome = await RunPipelineAsync(fixture.CreateRequest());
        var states = States(outcome);

        Assert.NotNull(outcome.GeneratedPassword);
        Assert.True(await fixture.LoginExistsAsync(fixture.RuntimeLoginName));
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, outcome.GeneratedPassword!));
    }

    [Fact]
    public async Task ExistingRuntimeLoginPasswordIsNeverResetAndReuseRequiresItsOwnCredentials()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        var originalPassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        await RunPipelineAsync(fixture.CreateRequest(runtimePassword: originalPassword));

        // Regression guard (3.1): a re-run with a DIFFERENT password must NOT reset the
        // existing login; it fails closed instead and the original password keeps working.
        var attackerPassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        var (rerunWithDifferentPassword, diagnostics) = await RunPipelineExpectingFailureAsync(
            fixture.CreateRequest(runtimePassword: attackerPassword));
        var rerunStates = States(rerunWithDifferentPassword);

        Assert.False(rerunWithDifferentPassword.Success);
        Assert.Equal("failed", rerunStates["runtimeIdentity"]);
        Assert.Contains("تغییر نمی", rerunStates["runtimeIdentity"] + rerunWithDifferentPassword.Message, StringComparison.Ordinal);
        Assert.False(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, attackerPassword));
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, originalPassword));
        Assert.Null(rerunWithDifferentPassword.GeneratedPassword);
        GC.KeepAlive(diagnostics);

        // A re-run with NO password (generation requested) must also fail closed.
        var (rerunWithGeneration, _) = await RunPipelineExpectingFailureAsync(fixture.CreateRequest());
        Assert.False(rerunWithGeneration.Success);
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, originalPassword));
    }

    [Fact]
    public async Task PipelineFailsFastForSystemDatabaseNames()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        var request = fixture.CreateRequest(databaseName: "master");

        var (outcome, _) = await RunPipelineExpectingFailureAsync(request);
        var states = States(outcome);

        Assert.False(outcome.Success);
        // Identifier validation is fail-fast at stage 1; nothing reaches the inspect stage.
        Assert.True(states["connection"] == "failed" || states["inspect"] == "failed",
            $"connection={states["connection"]}, inspect={states["inspect"]}");
        Assert.Equal("pending", states["createDatabase"]);
        Assert.Contains("سیستمی", states["connection"] + states["inspect"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task PipelineRefusesAnExistingEmptyDatabaseWithoutOwnershipProof()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        // An empty pre-created database may have been made for another purpose (for example
        // an empty Customer database); without ownership proof it is never adopted.
        await fixture.CreateEmptyForeignDatabaseAsync();

        var (outcome, diagnostics) = await RunPipelineExpectingFailureAsync(
            fixture.CreateRequest(databaseName: fixture.ForeignDatabaseName));
        var states = States(outcome);
        var createMessage = outcome.StageResults.First(stage => stage.Key == "createDatabase").Message;

        Assert.False(outcome.Success);
        Assert.Equal("failed", states["createDatabase"]);
        Assert.Equal("pending", states["migrate"]);
        Assert.Contains("مالکیت", createMessage + outcome.Message, StringComparison.Ordinal);
        Assert.False(await fixture.HasMigrationsHistoryAsync(fixture.ForeignDatabaseName));
        Assert.Equal(0, await fixture.UserTableCountAsync(fixture.ForeignDatabaseName));
        GC.KeepAlive(diagnostics);
    }

    [Fact]
    public async Task PipelineRecoversACrashWindowOrphanOnlyWithMatchingIntentEvidence()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        await fixture.CreateEmptyForeignDatabaseAsync();

        // No intent record: the orphaned database stays anonymous and fail-closed.
        var (withoutIntent, _) = await RunPipelineExpectingFailureAsync(
            fixture.CreateRequest(databaseName: fixture.ForeignDatabaseName, runtimePassword: VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate()));
        Assert.False(withoutIntent.Success);
        Assert.False(await fixture.HasOwnershipMarkerAsync(fixture.ForeignDatabaseName));

        // With a fresh intent recorded before the create and the create-date binding
        // satisfied, the controlled recovery writes the marker and continues the flow.
        var createdUtc = await fixture.ReadDatabaseCreatedUtcAsync(fixture.ForeignDatabaseName);
        Assert.NotNull(createdUtc);
        var intentIssuedUtc = createdUtc.Value.AddMinutes(-2);
        await fixture.WriteProvisioningIntentAsync(fixture.ForeignDatabaseName, intentIssuedUtc);

        var withIntent = await RunPipelineAsync(
            fixture.CreateRequest(databaseName: fixture.ForeignDatabaseName, runtimePassword: VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate()));
        var recoveredStates = States(withIntent);

        Assert.Equal("skipped", recoveredStates["createDatabase"]);
        Assert.Equal("success", recoveredStates["migrate"]);
        Assert.True(await fixture.HasOwnershipMarkerAsync(fixture.ForeignDatabaseName));
        Assert.True(await fixture.HasMigrationsHistoryAsync(fixture.ForeignDatabaseName));
        Assert.Null(await fixture.ReadProvisioningIntentAsync());
    }

    [Fact]
    public async Task PipelineKeepsAcceptingLegacyVendorDatabasesByHistoryAndBackfillsMarker()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        var runtimePassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();

        await RunPipelineAsync(fixture.CreateRequest(runtimePassword: runtimePassword));

        // Simulate a pre-marker Vendor database: history proves ownership, marker removed.
        await fixture.DropOwnershipMarkerAsync();
        Assert.False(await fixture.HasOwnershipMarkerAsync());

        var rerunPassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        var rerun = await RunPipelineAsync(fixture.CreateRequest(runtimePassword: rerunPassword));
        var rerunStates = States(rerun);

        Assert.Equal("skipped", rerunStates["createDatabase"]);
        Assert.Equal("success", rerunStates["migrate"]);
        Assert.True(await fixture.HasOwnershipMarkerAsync());
    }

    [Fact]
    public async Task PipelineRefusesAnOverPrivilegedExistingRuntimeLogin()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        var runtimePassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();

        await RunPipelineAsync(fixture.CreateRequest(runtimePassword: runtimePassword));

        // Simulate server-level drift: the existing login must be refused, not silently reused.
        await fixture.GrantViewAnyDatabaseAsync();
        var rerunPassword = VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate();
        var (rerun, diagnostics) = await RunPipelineExpectingFailureAsync(
            fixture.CreateRequest(runtimePassword: rerunPassword));
        var rerunStates = States(rerun);
        var identityMessage = rerun.StageResults.First(stage => stage.Key == "runtimeIdentity").Message;

        Assert.False(rerun.Success);
        Assert.Equal("failed", rerunStates["runtimeIdentity"]);
        Assert.Contains("سطح سرور", identityMessage, StringComparison.Ordinal);
        // The privileged login's password must not have been reset by the refused run.
        Assert.False(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, rerunPassword));
        Assert.True(await fixture.RuntimeLoginCanConnectAsync(fixture.RuntimeLoginName, runtimePassword));
        GC.KeepAlive(diagnostics);
    }

    [Fact]
    public async Task PipelineRefusesToMigrateAUnknownDatabaseWithUserTables()
    {
        var fixture = await ProvisioningFixture.OpenAsync();
        if (fixture is null) return;
        await using var _ = fixture;
        await fixture.CreateForeignDatabaseAsync();

        var (outcome, diagnostics) = await RunPipelineExpectingFailureAsync(
            fixture.CreateRequest(databaseName: fixture.ForeignDatabaseName, runtimePassword: VendorDevelopmentDatabaseProvisioning.VendorRuntimePassword.Generate()));
        var states = States(outcome);
        var createMessage = outcome.StageResults.First(stage => stage.Key == "createDatabase").Message;

        Assert.False(outcome.Success);
        Assert.Equal("failed", states["createDatabase"]);
        Assert.Contains("جدول", createMessage + outcome.Message, StringComparison.Ordinal);
        Assert.False(await fixture.HasMigrationsHistoryAsync(fixture.ForeignDatabaseName));
        GC.KeepAlive(diagnostics);
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
                ConnectTimeout = 15,
                Pooling = false
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

        // Pooling is disabled for fixture-owned connections so DROP DATABASE never fights an
        // idle pooled connection, and a fixture can never disturb other classes' pools.
        private static SqlConnectionStringBuilder TargetBuilder(string database) =>
            new(Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER")!)
            {
                InitialCatalog = database,
                ConnectTimeout = 15,
                Pooling = false
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

                await ExecuteAsync("master", $"DROP LOGIN IF EXISTS [{RuntimeLoginName.Replace("]", "]]", StringComparison.Ordinal)}]");
                await ExecuteAsync("master",
                    "IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = 'FaranouVendorProvisioningIntent') EXEC sys.sp_dropextendedproperty @name = N'FaranouVendorProvisioningIntent';");
            }
            catch (SqlException)
            {
                // Ignore: the CI server is ephemeral and discarded wholesale after the run.
            }
        }
    }
}
