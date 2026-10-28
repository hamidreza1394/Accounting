using Faranou.Vendor.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorLicenseEntitlementMigrationTests
{
    [Fact]
    public async Task MigrationBackfillsExistingLicenseKindWithoutInventingSubsystemEntitlements()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var baseConnectionString = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            const string message = "Set FARANO_VENDOR_TEST_SQLSERVER to a disposable SQL Server connection. Use FARANO_REQUIRE_VENDOR_SQLSERVER=1 to require this integration test in CI.";
            if (string.Equals(Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER"), "1", StringComparison.Ordinal))
            {
                Assert.Fail(message);
            }

            Assert.Skip(message);
            return;
        }

        var baseBuilder = new SqlConnectionStringBuilder(baseConnectionString);
        Assert.Contains("localhost", baseBuilder.DataSource, StringComparison.OrdinalIgnoreCase);
        var databaseName = $"FaranouVendorTests_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(baseConnectionString, databaseName, cancellationToken);
        var testConnection = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        var customerId = Guid.NewGuid();
        var perpetualId = Guid.NewGuid();
        var termId = Guid.NewGuid();

        try
        {
            await using (var provider = VendorTestServiceProviderFactory.Create(testConnection))
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var migrator = db.GetService<IMigrator>();
                await migrator.MigrateAsync("20261028110000_VendorAuditHashVersionAndLinearChain", cancellationToken);

                await db.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO dbo.VendorCustomers
                        (Id, CustomerNumber, LegalName, DisplayName, Status, CreatedAtUtc, UpdatedAtUtc)
                    VALUES ({0}, N'MIGRATION-CUSTOMER', N'Migration Customer', N'Migration Customer', N'Active',
                            '2026-01-01T00:00:00+00:00', '2026-01-01T00:00:00+00:00');

                    INSERT INTO dbo.VendorLicenses
                        (Id, CustomerId, LicenseNumber, ProductCode, Status, ValidFromUtc, ValidUntilUtc, CreatedAtUtc)
                    VALUES
                        ({1}, {0}, N'MIGRATION-PERPETUAL', N'FARANOU', N'Draft',
                         '2026-01-01T00:00:00+00:00', NULL, '2026-01-01T00:00:00+00:00'),
                        ({2}, {0}, N'MIGRATION-TERM', N'FARANOU', N'Draft',
                         '2026-01-01T00:00:00+00:00', '2027-01-01T00:00:00+00:00', '2026-01-01T00:00:00+00:00');
                    """,
                    new object[] { customerId, perpetualId, termId },
                    cancellationToken);

                await migrator.MigrateAsync(null, cancellationToken);

                var perpetual = await db.Licenses.AsNoTracking().SingleAsync(item => item.Id == perpetualId, cancellationToken);
                var term = await db.Licenses.AsNoTracking().SingleAsync(item => item.Id == termId, cancellationToken);
                Assert.Equal("perpetual", perpetual.LicenseKind);
                Assert.Equal("term", term.LicenseKind);
                Assert.Null(perpetual.MaxCompanies);
                Assert.Null(term.MaxCompanies);
                Assert.Equal("[]", perpetual.SubsystemsJson);
                Assert.Equal("[]", term.SubsystemsJson);
                Assert.Equal(30, perpetual.GraceDays);
                Assert.Equal(30, term.GraceDays);
                Assert.Equal(0, perpetual.LastIssuedSequence);
                Assert.Equal(0, term.LastIssuedSequence);
            }
        }
        finally
        {
            await DropDatabaseAsync(baseConnectionString, databaseName, cancellationToken);
        }
    }

    private static async Task CreateDatabaseAsync(string baseConnectionString, string databaseName, CancellationToken cancellationToken)
    {
        ValidateDatabaseName(databaseName);
        var masterConnectionString = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = "master" }.ConnectionString;
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DropDatabaseAsync(string baseConnectionString, string databaseName, CancellationToken cancellationToken)
    {
        ValidateDatabaseName(databaseName);
        var masterConnectionString = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = "master" }.ConnectionString;
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ValidateDatabaseName(string databaseName)
    {
        if (!databaseName.StartsWith("FaranouVendorTests_", StringComparison.Ordinal) ||
            databaseName.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new InvalidOperationException("Refusing to create or drop a non-test Vendor database.");
        }
    }
}
