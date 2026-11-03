using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Proves on SQL Server (not only the in-memory provider) that the role member list translates,
/// pages, and that a transfer is atomic and respects the (RoleId, TenantId) composite key.
/// </summary>
public sealed class CompanyRoleMembersSqlServerTests
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
    public async Task MemberListPagesAndTransferMovesMembersAtomicallyOnSqlServer()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_RM_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            var company = new Domain.Company { Name = "شرکت نمونه", IsActive = true };
            var otherCompany = new Domain.Company { Name = "شرکت دیگر", IsActive = true };
            var adminRole = new Domain.Role(Constants.Role.Admin) { IsActive = true };
            var admin = new Domain.User("rm-admin@example.test") { Username = "rm-admin", IsActive = true, Role = adminRole };
            var source = new Domain.CompanyRole { Company = company, TenantId = company.Id, Code = "OLD", Name = "قدیمی", IsActive = true };
            var target = new Domain.CompanyRole { Company = company, TenantId = company.Id, Code = "NEW", Name = "جدید", IsActive = true };
            var foreign = new Domain.CompanyRole { Company = otherCompany, TenantId = otherCompany.Id, Code = "FOREIGN", Name = "دیگر", IsActive = true };
            database.AddRange(company, otherCompany, adminRole, admin, source, target, foreign);

            // Distinct and NULL names, so the ORDER BY on nullable columns and OFFSET/FETCH are exercised.
            var members = new[]
            {
                new Domain.User("rm-member-0@example.test") { Username = "rm-member-0", IsActive = true, LastName = "الف", FirstName = "ب" },
                new Domain.User("rm-member-1@example.test") { Username = "rm-member-1", IsActive = true, LastName = "الف", FirstName = "الف" },
                new Domain.User("rm-member-2@example.test") { Username = "rm-member-2", IsActive = true },
                new Domain.User("rm-member-3@example.test") { Username = "rm-member-3", IsActive = true, LastName = "ت" },
                new Domain.User("rm-member-4@example.test") { Username = "rm-member-4", IsActive = true },
            };
            database.Users.AddRange(members);
            foreach (var member in members)
                database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
                {
                    UserId = member.Id, TenantId = company.Id, Role = source, RoleId = source.Id, IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                });
            await database.SaveChangesAsync(cancellationToken);

            var seen = new List<Guid>();
            foreach (var pageNumber in new[] { 1, 2, 3 })
            {
                var page = await Server.Api.CompanyRoleAdministrationEndpoints.GetRoleMembersAsync(
                    company.Id, source.Id, pageNumber, 2, new DefaultHttpContext(), Principal(admin.Id), database, cancellationToken);
                var list = Assert.IsType<Ok<Server.Api.CompanyRoleMemberListResponse>>(page).Value!;
                Assert.Equal(5, list.TotalCount);
                Assert.Equal(pageNumber < 3 ? 2 : 1, list.Items.Count);
                seen.AddRange(list.Items.Select(item => item.UserId));
            }
            // Pages are disjoint and together cover every member exactly once.
            Assert.Equal(members.Select(member => member.Id).Order(), seen.Order());
            Assert.Equal(seen.Count, seen.Distinct().Count());

            var crossCompany = await Server.Api.CompanyRoleAdministrationEndpoints.TransferMembersAsync(
                company.Id, source.Id, new DefaultHttpContext(), Principal(admin.Id),
                new Server.Api.TransferCompanyRoleMembersRequest(foreign.Id),
                new AllowAntiforgery(), database, TimeProvider.System, cancellationToken);
            Assert.Equal(StatusCodes.Status404NotFound, (crossCompany as IStatusCodeHttpResult)?.StatusCode);

            var moved = await Server.Api.CompanyRoleAdministrationEndpoints.TransferMembersAsync(
                company.Id, source.Id, new DefaultHttpContext(), Principal(admin.Id),
                new Server.Api.TransferCompanyRoleMembersRequest(target.Id),
                new AllowAntiforgery(), database, TimeProvider.System, cancellationToken);
            Assert.Equal(5, Assert.IsType<Ok<Server.Api.TransferCompanyRoleMembersResponse>>(moved).Value!.MovedCount);

            await using var verify = CreateContext(name);
            Assert.All(await verify.UserCompanyMemberships.ToListAsync(cancellationToken),
                membership => Assert.Equal(target.Id, membership.RoleId));
            Assert.Equal(5, await verify.SecurityAuditEvents
                .CountAsync(e => e.EventType == "UserCompanyRoleAssigned", cancellationToken));
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task TransferOfTheMaximumBatchSucceedsOnSqlServerAndAuditsEveryUser()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_RM_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            var company = new Domain.Company { Name = "شرکت نمونه", IsActive = true };
            var adminRole = new Domain.Role(Constants.Role.Admin) { IsActive = true };
            var admin = new Domain.User("rm-big-admin@example.test") { Username = "rm-big-admin", IsActive = true, Role = adminRole };
            var source = new Domain.CompanyRole { Company = company, TenantId = company.Id, Code = "OLD", Name = "قدیمی", IsActive = true };
            var target = new Domain.CompanyRole { Company = company, TenantId = company.Id, Code = "NEW", Name = "جدید", IsActive = true };
            database.AddRange(company, adminRole, admin, source, target);

            // Exactly the documented limit: a single audit row listing every id would not fit in
            // the 4000-character DetailsJson column, so this proves the per-user audit design.
            var count = Server.Api.CompanyRoleAdministrationEndpoints.MaxTransferBatchSize;
            for (var i = 0; i < count; i++)
            {
                var user = new Domain.User($"rm-big-{i}@example.test") { Username = $"rm-big-{i}", IsActive = true };
                database.Users.Add(user);
                database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
                {
                    UserId = user.Id, TenantId = company.Id, Role = source, RoleId = source.Id, IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                });
            }
            await database.SaveChangesAsync(cancellationToken);

            var moved = await Server.Api.CompanyRoleAdministrationEndpoints.TransferMembersAsync(
                company.Id, source.Id, new DefaultHttpContext(), Principal(admin.Id),
                new Server.Api.TransferCompanyRoleMembersRequest(target.Id),
                new AllowAntiforgery(), database, TimeProvider.System, cancellationToken);
            Assert.Equal(count, Assert.IsType<Ok<Server.Api.TransferCompanyRoleMembersResponse>>(moved).Value!.MovedCount);

            await using var verify = CreateContext(name);
            Assert.Equal(count, await verify.UserCompanyMemberships.CountAsync(m => m.RoleId == target.Id, cancellationToken));
            Assert.Equal(count, await verify.SecurityAuditEvents
                .CountAsync(e => e.EventType == "UserCompanyRoleAssigned", cancellationToken));
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    private sealed class AllowAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }
}
