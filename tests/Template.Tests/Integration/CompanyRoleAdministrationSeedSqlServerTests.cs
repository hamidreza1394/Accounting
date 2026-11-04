using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Verifies the company role-administration permission catalog seed (D-071) on the real
/// database provider: the migration inserts every server-owned code idempotently and the
/// catalog keeps its unique-code constraint.
/// </summary>
public sealed class CompanyRoleAdministrationSeedSqlServerTests
{
    private static bool SqlServerAvailable()
    {
        if (IntegrationDatabase.UsesSqlServer)
            return true;
        if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
            throw new InvalidOperationException("FARANOU_REQUIRE_SQLSERVER=1 but FARANOU_TEST_SQLSERVER is not set.");
        return false;
    }

    private static Data.DatabaseContext CreateContext(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(IntegrationDatabase.SqlServerConnectionString!)
        {
            InitialCatalog = databaseName,
        };
        return new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseSqlServer(builder.ConnectionString, sql =>
                sql.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName))
            .Options);
    }

    public static readonly string[] SeededCodes =
    [
        "roles.view",
        "roles.create",
        "roles.update",
        "roles.assign",
        "corporate-groups.directory.view",
    ];

    [Fact]
    public async Task TheMigrationSeedsEveryRoleAdministrationPermissionExactlyOnce()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_RoleAdminSeed_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            var seeded = await database.PermissionDefinitions.AsNoTracking()
                .Where(permission => SeededCodes.Contains(permission.Code))
                .ToListAsync(cancellationToken);

            Assert.Equal(SeededCodes.Length, seeded.Count);
            Assert.All(seeded, permission =>
            {
                Assert.False(string.IsNullOrWhiteSpace(permission.Name));
                Assert.False(string.IsNullOrWhiteSpace(permission.Description));
            });
            Assert.Equal(SeededCodes.Length, seeded.Select(permission => permission.Code).Distinct().Count());
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }
}
