using System.Data;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Real SQL Server roundtrips for the Development provisioning safety primitives: the
/// ownership marker (valid and fabricated values) and the provisioning intent ledger used
/// for controlled recovery of the CREATE DATABASE crash window. Runs only when
/// FARANO_VENDOR_TEST_SQLSERVER points at a disposable local SQL Server; the fixture
/// creates and drops only its own uniquely named objects.
/// </summary>
public sealed class VendorDevelopmentDatabaseSafetySqlServerTests
{
    [Fact]
    public async Task OwnershipMarkerRoundTripsAndRejectsFabricatedValues()
    {
        await using var fixture = await SafetyFixture.OpenAsync();
        if (fixture is null) return;
        // The fixture owns only its own uniquely named database; create it before connecting.
        await fixture.CreateEmptyDatabaseAsync();

        var builder = new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = fixture.DatabaseName };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Absent,
            await VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.ReadStateAsync(connection, TestContext.Current.CancellationToken));

        await VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.WriteAsync(connection, TestContext.Current.CancellationToken);
        Assert.Equal(
            VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Valid,
            await VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.ReadStateAsync(connection, TestContext.Current.CancellationToken));
        Assert.True(await VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.ReadAsync(connection, TestContext.Current.CancellationToken));

        // A foreign/fabricated marker value must fail closed instead of proving ownership.
        await fixture.ExecuteAsync(fixture.DatabaseName,
            $"EXEC sys.sp_updateextendedproperty @name = N'{VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerName}', @value = N'someone else claims this';");
        Assert.Equal(
            VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.InvalidValue,
            await VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.ReadStateAsync(connection, TestContext.Current.CancellationToken));
        Assert.False(await VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.ReadAsync(connection, TestContext.Current.CancellationToken));

        await fixture.DropOwnershipMarkerAsync();
        Assert.Equal(
            VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState.Absent,
            await VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.ReadStateAsync(connection, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProvisioningIntentLedgerRecordsMatchesAndClears()
    {
        await using var fixture = await SafetyFixture.OpenAsync();
        if (fixture is null) return;

        var masterBuilder = new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = "master" };
        await using var connection = new SqlConnection(masterBuilder.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var now = DateTimeOffset.UtcNow;
        Assert.Null(await VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.ReadAsync(connection, TestContext.Current.CancellationToken));

        await VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.WriteAsync(connection, fixture.DatabaseName, now, TestContext.Current.CancellationToken);
        var stored = await VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.ReadAsync(connection, TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal(
            VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.IntentState.Matching,
            VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.Classify(stored, fixture.DatabaseName, now.AddMinutes(1)));
        Assert.Equal(
            VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.IntentState.ForOtherDatabase,
            VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.Classify(stored, "AnotherDb", now.AddMinutes(1)));
        Assert.Equal(
            VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.IntentState.Expired,
            VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.Classify(stored, fixture.DatabaseName, now.AddHours(25)));

        await VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.ClearAsync(connection, TestContext.Current.CancellationToken);
        Assert.Null(await VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.ReadAsync(connection, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EmptyUnmarkedDatabaseIsRefusedUnlessAFreshIntentMatches()
    {
        await using var fixture = await SafetyFixture.OpenAsync();
        if (fixture is null) return;
        await fixture.CreateEmptyDatabaseAsync();
        Assert.True(await fixture.DatabaseExistsAsync());

        var markerState = await fixture.ReadMarkerStateAsync();
        var (owned, _) = VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.EvaluateOwnership(
            markerState, historyTableExists: false, appliedCount: 0, unknownCount: 0, userTableCount: 0);
        Assert.False(owned);

        // Without a matching intent the database stays anonymous and fail-closed.
        var noIntent = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.EvaluateRecovery(
            VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.IntentState.Absent,
            historyTableExists: false, userTableCount: 0);
        Assert.False(noIntent.Allowed);

        // With a fresh matching intent (the crash-window ledger) controlled recovery is allowed.
        var (recoverable, refusal) = VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.EvaluateRecovery(
            VendorDevelopmentDatabaseSafety.VendorProvisioningIntent.IntentState.Matching,
            historyTableExists: false, userTableCount: 0);
        Assert.True(recoverable, refusal);
    }

    private sealed class SafetyFixture : IAsyncDisposable
    {
        public string ConnectionString { get; }
        public string DatabaseName { get; } = $"FaranouSafety_{Guid.NewGuid():N}"[..40];

        private SafetyFixture(string connectionString) => ConnectionString = connectionString;

        public static async Task<SafetyFixture?> OpenAsync()
        {
            var connectionString = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                const string message = "Set FARANO_VENDOR_TEST_SQLSERVER to a disposable local SQL Server.";
                if (Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER") == "1")
                {
                    Assert.Fail(message);
                }

                Assert.Skip(message);
                return null;
            }

            return new SafetyFixture(connectionString);
        }

        public async Task CreateEmptyDatabaseAsync()
        {
            var builder = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" };
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{DatabaseName.Replace("]", "]]", StringComparison.Ordinal)}]";
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public async Task<bool> DatabaseExistsAsync()
        {
            var builder = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" };
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @name";
            command.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = DatabaseName });
            return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) is > 0;
        }

        public async Task<VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerState> ReadMarkerStateAsync()
        {
            var builder = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = DatabaseName };
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            return await VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.ReadStateAsync(connection, TestContext.Current.CancellationToken);
        }

        public async Task<int> UserTableCountAsync()
        {
            var builder = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = DatabaseName };
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0";
            var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            return result is int count ? count : 0;
        }

        public async Task DropOwnershipMarkerAsync()
        {
            var builder = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = DatabaseName };
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = @name) EXEC sys.sp_dropextendedproperty @name = @name;";
            command.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = VendorDevelopmentDatabaseSafety.VendorOwnershipMarker.MarkerName });
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public async Task ExecuteAsync(string database, string commandText)
        {
            var builder = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = database };
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" };
                await using var connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP DATABASE IF EXISTS [{DatabaseName.Replace("]", "]]", StringComparison.Ordinal)}]";
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }
            catch (SqlException)
            {
                // Ignore: the CI server is ephemeral and discarded wholesale after the run.
            }
        }
    }
}
