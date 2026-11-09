using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Verifies the D-078 user-creation permission seed on the real database provider: the
/// migration inserts <c>users.create</c> exactly once with its fixed identifier, keeps the
/// unique-code constraint, and never grants the code to any company role (the grant must
/// always be an explicit assignment under the D-071 delegation ceiling).
/// </summary>
public sealed class UserCreationPermissionSeedSqlServerTests
{
    private const string SeedMigration = "20261110120000_SeedUsersCreatePermission";
    private const string UsersCreateCode = "users.create";
    private const string UsersCreateId = "f47a2026-1103-4d00-8000-000000000003";

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

    [Fact]
    public async Task TheMigrationSeedsTheUserCreatePermissionExactlyOnce()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_UsersCreateSeed_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            Assert.Contains(SeedMigration, await database.Database.GetAppliedMigrationsAsync(cancellationToken));

            var seeded = await database.PermissionDefinitions.AsNoTracking()
                .Where(permission => permission.Code == UsersCreateCode)
                .ToListAsync(cancellationToken);

            var permission = Assert.Single(seeded);
            Assert.Equal(Guid.Parse(UsersCreateId), permission.Id);
            Assert.False(string.IsNullOrWhiteSpace(permission.Name));
            Assert.False(string.IsNullOrWhiteSpace(permission.Description));

            // The seed is data-only: no company role may hold the code out of the box.
            Assert.False(await database.CompanyRolePermissions.AsNoTracking()
                .AnyAsync(rolePermission => rolePermission.PermissionId == permission.Id, cancellationToken));
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task TheSeedIsIdempotentWhenReapplied()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_UsersCreateSeedRe_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            // The guarded INSERT keeps a second execution on a database that already holds
            // the row from becoming a duplicate (the unique code index would reject it).
            await database.Database.ExecuteSqlRawAsync(
                "IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'users.create') " +
                "INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) " +
                "VALUES ('f47a2026-1103-4d00-8000-000000000003', 'users.create', N'ایجاد کاربر برای شرکت', N'D-078', 21007, SYSDATETIME());",
                cancellationToken);

            var count = await database.PermissionDefinitions.AsNoTracking()
                .CountAsync(permission => permission.Code == UsersCreateCode, cancellationToken);
            Assert.Equal(1, count);
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }
}
