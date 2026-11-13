using Faranou.Modules.Trade.Domain.Parties;
using Faranou.Modules.Trade.Domain.Products;
using Faranou.Modules.Trade.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// S2-1 gate: the first TradeDbContext migration creates exactly Parties, PartyRoleAssignments and
/// Products in schema trade; PartyBankAccounts and ProductIdentifiers stay in the model but are
/// excluded from migrations (D-081).
/// </summary>
public sealed class TradeMasterDataMigrationTests
{
    private const string FakeConnection =
        "Server=localhost;Database=Trade_MigrationGate;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True";

    private static readonly string[] ExpectedTables = ["Parties", "PartyRoleAssignments", "Products"];

    private static TradeDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TradeDbContext>();
        TradeDbContextOptionsConfiguration.ConfigureSqlServer(options, connectionString);
        return new TradeDbContext(options.Options);
    }

    [Fact]
    public void CreateScript_ContainsExactlyThreeTables_AndModelKeepsFiveEntities()
    {
        using var context = CreateContext(FakeConnection);

        var script = context.Database.GenerateCreateScript();

        var created = System.Text.RegularExpressions.Regex.Matches(script, @"CREATE TABLE \[trade\]\.\[(\w+)\]")
            .Select(match => match.Groups[1].Value).Order().ToArray();
        Assert.Equal(ExpectedTables, created);
        Assert.DoesNotContain("PartyBankAccounts", script);
        Assert.DoesNotContain("ProductIdentifiers", script);

        foreach (var clrType in new[]
                 {
                     typeof(Party), typeof(PartyRoleAssignment), typeof(PartyBankAccount),
                     typeof(Product), typeof(ProductIdentifier),
                 })
            Assert.NotNull(context.Model.FindEntityType(clrType));
    }

    [Fact]
    public void MigrationScript_ContainsExactlyThreeTables_FromTheMigrationItself()
    {
        using var context = CreateContext(FakeConnection);

        // Unlike GenerateCreateScript (built from the model), this renders the migration's own Up().
        var script = context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>()
            .GenerateScript();

        var created = System.Text.RegularExpressions.Regex.Matches(script, @"CREATE TABLE \[trade\]\.\[(\w+)\]")
            .Select(match => match.Groups[1].Value).Order().ToArray();
        Assert.Equal(ExpectedTables, created);
        Assert.DoesNotContain("PartyBankAccounts", script);
        Assert.DoesNotContain("ProductIdentifiers", script);
    }

    [Fact]
    public void Migration_SnapshotMatchesModel()
    {
        using var context = CreateContext(FakeConnection);

        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Contains(context.Database.GetMigrations(), name => name.EndsWith("_InitialTradeMasterData"));
    }
}

public sealed class TradeMasterDataMigrationSqlServerTests
{
    private static bool SqlServerAvailable()
    {
        if (IntegrationDatabase.UsesSqlServer)
            return true;
        if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
            throw new InvalidOperationException("FARANOU_REQUIRE_SQLSERVER=1 but FARANOU_TEST_SQLSERVER is not set.");
        return false;
    }

    private static string ConnectionFor(string databaseName) =>
        new SqlConnectionStringBuilder(IntegrationDatabase.SqlServerConnectionString!)
        {
            InitialCatalog = databaseName,
        }.ConnectionString;

    private static TradeDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<TradeDbContext>();
        TradeDbContextOptionsConfiguration.ConfigureSqlServer(options, ConnectionFor(databaseName));
        return new TradeDbContext(options.Options);
    }

    private static async Task<List<string>> QueryStringsAsync(
        SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetString(0));
        return result;
    }

    private static async Task<int> InsertAsync(
        SqlConnection connection, string table, Guid tenantId, string code, bool deleted,
        CancellationToken cancellationToken)
    {
        var columns = table == "Parties"
            ? "Id, Code, LegalName, IsActive, TenantId, CreatedAtUtc, CreatedByUserId, IsDeleted"
            : "Id, Code, Name, BaseUnitCode, IsActive, TenantId, CreatedAtUtc, CreatedByUserId, IsDeleted";
        var values = table == "Parties"
            ? "@id, @code, N'x', 1, @tenant, SYSUTCDATETIME(), @user, @deleted"
            : "@id, @code, N'x', 'TON', 1, @tenant, SYSUTCDATETIME(), @user, @deleted";
        await using var command = new SqlCommand(
            $"INSERT INTO [trade].[{table}] ({columns}) VALUES ({values})", connection);
        command.Parameters.AddWithValue("@id", Guid.NewGuid());
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@tenant", tenantId);
        command.Parameters.AddWithValue("@user", Guid.NewGuid());
        command.Parameters.AddWithValue("@deleted", deleted);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    [Fact]
    public async Task Migration_CreatesThreeTables_FilteredUniqueIndexes_AndRejectsActiveDuplicates()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_TradeS21_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using (var database = CreateContext(name))
            {
                await database.Database.MigrateAsync(cancellationToken);
                Assert.Contains(
                    await database.Database.GetAppliedMigrationsAsync(cancellationToken),
                    applied => applied.EndsWith("_InitialTradeMasterData"));
            }

            await using var connection = new SqlConnection(ConnectionFor(name));
            await connection.OpenAsync(cancellationToken);

            var tables = await QueryStringsAsync(connection,
                "SELECT t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id " +
                "WHERE s.name = 'trade' ORDER BY t.name", cancellationToken);
            Assert.Equal(["Parties", "PartyRoleAssignments", "Products"], tables);

            var history = await QueryStringsAsync(connection,
                "SELECT t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id " +
                "WHERE s.name = 'dbo' AND t.name = '__TradeMigrationsHistory'", cancellationToken);
            Assert.Single(history);

            var indexes = await QueryStringsAsync(connection,
                "SELECT name + '|' + filter_definition FROM sys.indexes " +
                "WHERE is_unique = 1 AND is_primary_key = 0 AND is_unique_constraint = 0 " +
                "AND name LIKE 'UX[_]%' ORDER BY name", cancellationToken);
            Assert.Equal(
            [
                "UX_Parties_Tenant_Code_Active|([IsDeleted]=(0))",
                "UX_PartyRoleAssignments_Tenant_Party_Role_Active|([IsDeleted]=(0))",
                "UX_Products_Tenant_Code_Active|([IsDeleted]=(0))",
            ], indexes);

            var fks = await QueryStringsAsync(connection,
                "SELECT name FROM sys.foreign_keys WHERE name LIKE 'FK[_]PartyRoleAssignments%'", cancellationToken);
            Assert.Single(fks);

            var tenant = Guid.NewGuid();
            foreach (var table in new[] { "Parties", "Products" })
            {
                Assert.Equal(1, await InsertAsync(connection, table, tenant, "C1", false, cancellationToken));

                // Another tenant may reuse the code.
                Assert.Equal(1, await InsertAsync(connection, table, Guid.NewGuid(), "C1", false, cancellationToken));

                // A second active row with the same (TenantId, Code) is rejected (unique index violation).
                var duplicate = await Assert.ThrowsAsync<SqlException>(() =>
                    InsertAsync(connection, table, tenant, "C1", false, cancellationToken));
                Assert.Contains(duplicate.Errors.Cast<SqlError>(), error => error.Number is 2601 or 2627);

                // A soft-deleted duplicate is allowed.
                Assert.Equal(1, await InsertAsync(connection, table, tenant, "C1", true, cancellationToken));
            }
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }
}
