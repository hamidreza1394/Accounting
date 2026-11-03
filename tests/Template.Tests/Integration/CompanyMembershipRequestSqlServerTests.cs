using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Verifies the additive membership-request migration and the single-winner approval
/// transition against SQL Server, including rowversion and the filtered pending index.
/// </summary>
public sealed class CompanyMembershipRequestSqlServerTests
{
    private const string PreviousMigration = "20261031140000_SeedTradePermissionCatalog";
    private const string MembershipMigration = "20261103111108_AddCompanyMembershipRequests";

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

    private static async Task WithDatabaseAsync(Func<string, Task> test)
    {
        var name = "FaranouTests_MR_" + Guid.NewGuid().ToString("N")[..12];
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
    public async Task MigrationPreservesExistingMembershipsAndConcurrentApprovalsHaveOneWinner()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        await WithDatabaseAsync(async name =>
        {
            Guid destinationId;
            Guid requesterId;
            Guid approverAId;
            Guid approverBId;
            Guid memberRoleId;
            Guid requestId;

            await using (var before = CreateContext(name))
            {
                await before.GetService<IMigrator>().MigrateAsync(
                    PreviousMigration, TestContext.Current.CancellationToken);

                var destination = new Domain.Company { Name = "شرکت مقصد", IsActive = true };
                var requester = new Domain.User("requester-membership@example.test")
                {
                    Username = "requester-membership",
                    IsActive = true,
                };
                var approverA = new Domain.User("approver-a@example.test")
                {
                    Username = "approver-a",
                    IsActive = true,
                };
                var approverB = new Domain.User("approver-b@example.test")
                {
                    Username = "approver-b",
                    IsActive = true,
                };
                var approverRoleA = new Domain.CompanyRole
                {
                    TenantId = destination.Id, Company = destination, Code = "APPROVER_A", Name = "تأییدکننده الف", IsActive = true,
                };
                var approverRoleB = new Domain.CompanyRole
                {
                    TenantId = destination.Id, Company = destination, Code = "APPROVER_B", Name = "تأییدکننده ب", IsActive = true,
                };
                var memberRole = new Domain.CompanyRole
                {
                    TenantId = destination.Id, Company = destination, Code = "MEMBER", Name = "عضو", IsActive = true,
                };

                before.Companies.Add(destination);
                before.Users.AddRange(requester, approverA, approverB);
                before.CompanyRoles.AddRange(approverRoleA, approverRoleB, memberRole);
                before.UserCompanyMemberships.AddRange(
                    new Domain.UserCompanyMembership
                    {
                        UserId = approverA.Id, TenantId = destination.Id, Role = approverRoleA, IsActive = true,
                        CreatedAtUtc = DateTime.UtcNow,
                    },
                    new Domain.UserCompanyMembership
                    {
                        UserId = approverB.Id, TenantId = destination.Id, Role = approverRoleB, IsActive = true,
                        CreatedAtUtc = DateTime.UtcNow,
                    });

                await before.SaveChangesAsync(TestContext.Current.CancellationToken);
                destinationId = destination.Id;
                requesterId = requester.Id;
                approverAId = approverA.Id;
                approverBId = approverB.Id;
                memberRoleId = memberRole.Id;

                // Upgrade an existing database after destination approver memberships already exist.
                await before.Database.MigrateAsync(TestContext.Current.CancellationToken);
                Assert.True(await before.UserCompanyMemberships.AnyAsync(
                    item => item.UserId == approverAId && item.TenantId == destinationId && item.IsActive,
                    TestContext.Current.CancellationToken));
                Assert.Contains(await before.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken),
                    item => item == MembershipMigration);

                var permission = await before.PermissionDefinitions.SingleAsync(
                    item => item.Code == Server.Api.CompanyMembershipRequestEndpoints.ApprovalPermissionCode,
                    TestContext.Current.CancellationToken);
                before.CompanyRolePermissions.AddRange(
                    new Domain.CompanyRolePermission { RoleId = approverRoleA.Id, Role = approverRoleA, PermissionId = permission.Id, Permission = permission },
                    new Domain.CompanyRolePermission { RoleId = approverRoleB.Id, Role = approverRoleB, PermissionId = permission.Id, Permission = permission });

                var membershipRequest = new Domain.CompanyMembershipRequest
                {
                    UserId = requesterId,
                    DestinationTenantId = destinationId,
                    Status = Domain.CompanyMembershipRequestStatuses.Pending,
                    RequestedAtUtc = DateTime.UtcNow,
                };
                before.CompanyMembershipRequests.Add(membershipRequest);
                await before.SaveChangesAsync(TestContext.Current.CancellationToken);
                requestId = membershipRequest.Id;
            }

            async Task<IResult> ApproveAsync(Guid approverId)
            {
                await using var database = CreateContext(name);
                return await Server.Api.CompanyMembershipRequestEndpoints.ApproveAsync(
                    destinationId,
                    requestId,
                    new DefaultHttpContext(),
                    Principal(approverId),
                    new Server.Api.ApproveCompanyMembershipRequest(memberRoleId),
                    new AllowAntiforgery(),
                    database,
                    new Infrastructure.Tenancy.CompanyAccessService(database),
                    TimeProvider.System,
                    TestContext.Current.CancellationToken);
            }

            var outcomes = await Task.WhenAll(ApproveAsync(approverAId), ApproveAsync(approverBId));
            Assert.Single(outcomes.Where(result =>
                (result as IStatusCodeHttpResult)?.StatusCode == StatusCodes.Status204NoContent));
            Assert.All(outcomes.Where(result =>
                    (result as IStatusCodeHttpResult)?.StatusCode != StatusCodes.Status204NoContent),
                result =>
                {
                    var status = (result as IStatusCodeHttpResult)?.StatusCode;
                    Assert.True(status == StatusCodes.Status404NotFound || status == StatusCodes.Status409Conflict);
                });

            await using (var check = CreateContext(name))
            {
                var savedRequest = await check.CompanyMembershipRequests.SingleAsync(
                    item => item.Id == requestId, TestContext.Current.CancellationToken);
                Assert.Equal(Domain.CompanyMembershipRequestStatuses.Approved, savedRequest.Status);
                Assert.NotNull(savedRequest.DecidedByUserId);
                Assert.Contains(savedRequest.DecidedByUserId.Value, new[] { approverAId, approverBId });
                Assert.Equal(memberRoleId, savedRequest.ApprovedRoleId);

                var membership = await check.UserCompanyMemberships.SingleAsync(
                    item => item.UserId == requesterId && item.TenantId == destinationId,
                    TestContext.Current.CancellationToken);
                Assert.True(membership.IsActive);
                Assert.Equal(memberRoleId, membership.RoleId);
                Assert.Equal(1, await check.SecurityAuditEvents.CountAsync(
                    item => item.EventType == "CompanyMembershipApproved" && item.EntityId == requestId.ToString(),
                    TestContext.Current.CancellationToken));

                await check.GetService<IMigrator>().MigrateAsync(
                    PreviousMigration, TestContext.Current.CancellationToken);
                var tableCount = await check.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS [Value] FROM sys.tables WHERE [name] = 'CompanyMembershipRequests'")
                    .SingleAsync(TestContext.Current.CancellationToken);
                Assert.Equal(0, tableCount);

                await check.Database.MigrateAsync(TestContext.Current.CancellationToken);
                tableCount = await check.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS [Value] FROM sys.tables WHERE [name] = 'CompanyMembershipRequests'")
                    .SingleAsync(TestContext.Current.CancellationToken);
                Assert.Equal(1, tableCount);
            }
        });
    }

    private static ClaimsPrincipal Principal(Guid userId) => new(
        new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind),
        ], "sql-server-test"));

    private sealed class AllowAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }
}
