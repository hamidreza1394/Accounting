using System;
using System.Linq;
using System.Threading.Tasks;
using Domain;
using Faranou.Modules.Trade.Application.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Verifies the data-only Trade permission migration against SQL Server, including existing
/// catalog rows, no automatic role grants, and safe Down/Up behavior.
/// </summary>
public sealed class TradePermissionCatalogSqlServerTests
{
    private const string PreviousMigration = "20261031130000_AddCustomerLicensePersistence";

    private static bool SqlServerAvailable()
    {
        if (IntegrationDatabase.UsesSqlServer)
        {
            return true;
        }

        if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
        {
            throw new InvalidOperationException("FARANOU_REQUIRE_SQLSERVER=1 but FARANOU_TEST_SQLSERVER is not set.");
        }

        return false;
    }

    private static Data.DatabaseContext CreateContext(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(IntegrationDatabase.SqlServerConnectionString)
        {
            InitialCatalog = databaseName,
        };

        return new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseSqlServer(builder.ConnectionString, sql => sql.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName))
            .Options);
    }

    private static async Task WithDatabaseAsync(Func<string, Task> test)
    {
        var name = "FaranouTests_TradePermissions_" + Guid.NewGuid().ToString("N")[..12];
        try
        {
            await test(name);
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Migration_PreservesExistingCatalogRows_SeedsWithoutRoleGrants_AndRollsBackSafely()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithDatabaseAsync(async name =>
        {
            var existingCode = TradePermissionCodes.FormView("party");
            Guid existingId;

            await using (var before = CreateContext(name))
            {
                await before.GetService<IMigrator>().MigrateAsync(
                    PreviousMigration,
                    TestContext.Current.CancellationToken);

                var existingPermission = new PermissionDefinition
                {
                    Code = existingCode,
                    Name = "نام سفارشی مدیر",
                    Description = "متن موجود نباید بازنویسی شود.",
                };
                existingId = existingPermission.Id;
                before.PermissionDefinitions.Add(existingPermission);
                await before.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var context = CreateContext(name))
            {
                await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

                var existing = await context.PermissionDefinitions
                    .SingleAsync(permission => permission.Code == existingCode, TestContext.Current.CancellationToken);
                Assert.Equal(existingId, existing.Id);
                Assert.Equal("نام سفارشی مدیر", existing.Name);
                Assert.Equal("متن موجود نباید بازنویسی شود.", existing.Description);

                foreach (var permission in TradePermissionCatalog.All)
                {
                    var seeded = await context.PermissionDefinitions
                        .SingleAsync(row => row.Code == permission.Code, TestContext.Current.CancellationToken);

                    if (permission.Code != existingCode)
                    {
                        Assert.Equal(permission.Name, seeded.Name);
                        Assert.Equal(permission.Description, seeded.Description);
                    }
                }

                var expectedTradeCodes = TradePermissionCatalog.All
                    .Select(permission => permission.Code)
                    .OrderBy(code => code, StringComparer.Ordinal)
                    .ToArray();
                var actualTradeCodes = await context.PermissionDefinitions
                    .Where(row => row.Code.StartsWith("trade."))
                    .Select(row => row.Code)
                    .OrderBy(code => code)
                    .ToArrayAsync(TestContext.Current.CancellationToken);
                Assert.Equal(expectedTradeCodes, actualTradeCodes);

                // A catalog migration must not silently grant operational access.
                Assert.Empty(await context.CompanyRolePermissions.ToListAsync(TestContext.Current.CancellationToken));

                var company = new Company
                {
                    Name = "شرکت آزمون مجوز بازرگانی",
                    NationalId = "10987654321",
                    IsActive = true,
                };
                context.Companies.Add(company);

                var role = new CompanyRole
                {
                    TenantId = company.Id,
                    Code = "trade-permission-test",
                    Name = "نقش آزمون بازرگانی",
                    IsActive = true,
                };
                context.CompanyRoles.Add(role);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);

                var retainedCode = TradePermissionCodes.FormCreate("party");
                var retainedPermissionId = await context.PermissionDefinitions
                    .Where(permission => permission.Code == retainedCode)
                    .Select(permission => permission.Id)
                    .SingleAsync(TestContext.Current.CancellationToken);

                context.CompanyRolePermissions.Add(new CompanyRolePermission
                {
                    RoleId = role.Id,
                    PermissionId = retainedPermissionId,
                });
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);

                await context.GetService<IMigrator>().MigrateAsync(
                    PreviousMigration,
                    TestContext.Current.CancellationToken);
            }

            await using (var afterDown = CreateContext(name))
            {
                Assert.Equal(1, await afterDown.CompanyRolePermissions.CountAsync(TestContext.Current.CancellationToken));
                Assert.Contains(existingCode, await afterDown.PermissionDefinitions
                    .Select(permission => permission.Code)
                    .ToListAsync(TestContext.Current.CancellationToken));
                Assert.Contains(
                    TradePermissionCodes.FormCreate("party"),
                    await afterDown.PermissionDefinitions.Select(permission => permission.Code).ToListAsync(TestContext.Current.CancellationToken));

                Assert.DoesNotContain(
                    TradePermissionCodes.FormUpdate("party"),
                    await afterDown.PermissionDefinitions.Select(permission => permission.Code).ToListAsync(TestContext.Current.CancellationToken));

                await afterDown.Database.MigrateAsync(TestContext.Current.CancellationToken);

                foreach (var permission in TradePermissionCatalog.All)
                {
                    Assert.Single(await afterDown.PermissionDefinitions
                        .Where(row => row.Code == permission.Code)
                        .ToListAsync(TestContext.Current.CancellationToken));
                }

                Assert.Equal(1, await afterDown.CompanyRolePermissions.CountAsync(TestContext.Current.CancellationToken));
            }
        });
    }
}
