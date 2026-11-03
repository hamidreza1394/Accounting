using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Verifies the permission-code query and its company/role isolation on the production database provider.
/// </summary>
public sealed class MePermissionsSqlServerTests
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

    [Fact]
    public async Task GrantedPermissionCodesAreScopedToTheRequestedActiveCompanyMembership()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_MePermissions_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            var user = new Domain.User("me-permissions@example.test")
            {
                Username = "me-permissions",
                IsActive = true,
            };
            var companyA = new Domain.Company { Name = "شرکت مجوز الف", IsActive = true };
            var companyB = new Domain.Company { Name = "شرکت مجوز ب", IsActive = true };
            var roleA = new Domain.CompanyRole
            {
                Company = companyA, TenantId = companyA.Id, Code = "ACCOUNTANT", Name = "حسابدار", IsActive = true,
            };
            var roleB = new Domain.CompanyRole
            {
                Company = companyB, TenantId = companyB.Id, Code = "TRADE", Name = "بازرگانی", IsActive = true,
            };
            var permissionA = new Domain.PermissionDefinition { Code = "users.list", Name = "مشاهده کاربران" };
            var permissionB = new Domain.PermissionDefinition { Code = "trade.import", Name = "ورود بازرگانی" };
            database.AddRange(user, companyA, companyB, roleA, roleB, permissionA, permissionB);
            database.CompanyRolePermissions.AddRange(
                new Domain.CompanyRolePermission { Role = roleA, Permission = permissionA },
                new Domain.CompanyRolePermission { Role = roleB, Permission = permissionB });
            var membershipA = new Domain.UserCompanyMembership
            {
                User = user, UserId = user.Id, Company = companyA, TenantId = companyA.Id,
                Role = roleA, RoleId = roleA.Id, IsActive = true,
            };
            var membershipB = new Domain.UserCompanyMembership
            {
                User = user, UserId = user.Id, Company = companyB, TenantId = companyB.Id,
                Role = roleB, RoleId = roleB.Id, IsActive = false,
            };
            database.UserCompanyMemberships.AddRange(membershipA, membershipB);
            await database.SaveChangesAsync(cancellationToken);

            var access = new Infrastructure.Tenancy.CompanyAccessService(database);
            var companyAPermissions = await access.GetGrantedPermissionCodesAsync(
                user.Id, companyA.Id, cancellationToken);
            Assert.Equal(new[] { "users.list" }, companyAPermissions);

            var inactiveMembershipPermissions = await access.GetGrantedPermissionCodesAsync(
                user.Id, companyB.Id, cancellationToken);
            Assert.Empty(inactiveMembershipPermissions);

            membershipB.IsActive = true;
            await database.SaveChangesAsync(cancellationToken);
            var companyBPermissions = await access.GetGrantedPermissionCodesAsync(
                user.Id, companyB.Id, cancellationToken);
            Assert.Equal(new[] { "trade.import" }, companyBPermissions);

            roleA.IsActive = false;
            await database.SaveChangesAsync(cancellationToken);
            Assert.Empty(await access.GetGrantedPermissionCodesAsync(
                user.Id, companyA.Id, cancellationToken));
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }
}
