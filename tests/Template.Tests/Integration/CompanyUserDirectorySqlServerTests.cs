using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

public sealed class CompanyUserDirectorySqlServerTests
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

    private static ClaimsPrincipal Principal(Guid id) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, id.ToString()),
        new Claim("AccountKind", "User"),
    ], "test"));

    [Fact]
    public async Task CompanyDirectoryFiltersAndPagesWithinTenantOnSqlServer()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_CUD_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            var company = new Domain.Company { Name = "شرکت SQL", IsActive = true };
            var otherCompany = new Domain.Company { Name = "شرکت خارجی", IsActive = true };
            var permission = await database.PermissionDefinitions.SingleAsync(
                item => item.Code == "users.list", cancellationToken);
            var managerRole = new Domain.CompanyRole
            {
                Company = company, TenantId = company.Id, Code = "MANAGER", Name = "مدیر", IsActive = true,
            };
            var memberRole = new Domain.CompanyRole
            {
                Company = company, TenantId = company.Id, Code = "MEMBER", Name = "عضو", IsActive = true,
            };
            var foreignRole = new Domain.CompanyRole
            {
                Company = otherCompany, TenantId = otherCompany.Id, Code = "FOREIGN", Name = "شرکت دیگر", IsActive = true,
            };
            var manager = new Domain.User("sql-manager@example.test")
            {
                Username = "sql-manager", FirstName = "مدیر", LastName = "شرکت", IsActive = true,
            };
            var memberA = new Domain.User("sql-member-a@example.test")
            {
                Username = "sql-member-a", FirstName = "عضو", LastName = "A", IsActive = true,
            };
            var memberB = new Domain.User("sql-member-b@example.test")
            {
                Username = "sql-member-b", FirstName = "عضو", LastName = "B", IsActive = true,
            };
            var foreign = new Domain.User("sql-foreign@example.test")
            {
                Username = "sql-foreign", FirstName = "خارجی", LastName = "شرکت", IsActive = true,
            };

            database.AddRange(company, otherCompany, managerRole, memberRole, foreignRole,
                manager, memberA, memberB, foreign);
            database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
            {
                Role = managerRole,
                RoleId = managerRole.Id,
                Permission = permission,
                PermissionId = permission.Id,
            });
            database.UserCompanyMemberships.AddRange(
                NewMembership(manager, company, managerRole),
                NewMembership(memberA, company, memberRole),
                NewMembership(memberB, company, memberRole),
                NewMembership(foreign, otherCompany, foreignRole));
            await database.SaveChangesAsync(cancellationToken);

            var access = new Infrastructure.Tenancy.CompanyAccessService(database);
            var first = await Server.Api.CompanyUserDirectoryEndpoints.GetUsersAsync(
                company.Id, new DefaultHttpContext(), Principal(manager.Id), database, access,
                search: "sql-member", page: 1, pageSize: 1, cancellationToken: cancellationToken);
            var firstOk = Assert.IsType<Ok<Server.Api.CompanyUserDirectoryResponse>>(first);
            Assert.Equal(2, firstOk.Value!.TotalCount);
            var firstItem = Assert.Single(firstOk.Value.Items);
            Assert.Equal("sql-member-a", firstItem.Username);
            Assert.Equal(DateTimeKind.Utc, firstItem.MembershipCreatedAtUtc.Kind);
            Assert.EndsWith("Z", JsonSerializer.Serialize(firstItem.MembershipCreatedAtUtc));

            var second = await Server.Api.CompanyUserDirectoryEndpoints.GetUsersAsync(
                company.Id, new DefaultHttpContext(), Principal(manager.Id), database, access,
                search: "sql-member", page: 2, pageSize: 1, cancellationToken: cancellationToken);
            var secondOk = Assert.IsType<Ok<Server.Api.CompanyUserDirectoryResponse>>(second);
            Assert.Equal("sql-member-b", Assert.Single(secondOk.Value!.Items).Username);

            var json = JsonSerializer.Serialize(firstOk.Value);
            Assert.DoesNotContain("@example.test", json);
            Assert.DoesNotContain("Password", json);
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    private static Domain.UserCompanyMembership NewMembership(
        Domain.User user, Domain.Company company, Domain.CompanyRole role) => new()
    {
        UserId = user.Id,
        TenantId = company.Id,
        Company = company,
        RoleId = role.Id,
        Role = role,
        IsActive = true,
        CreatedAtUtc = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc),
    };
}
