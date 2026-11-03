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
            Guid restrictedRequesterId;
            Guid restrictedRoleId;
            Guid restrictedRequestId;

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
                await before.SaveChangesAsync(TestContext.Current.CancellationToken);

                // These rows must exist before the membership-request migration is applied.
                // RowVersion is introduced by a later migration, so the old-schema fixture
                // inserts them with SQL instead of using the current EF model.
                await before.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO CompanyRoles (Id, Ordering, InsertDateTime, TenantId, Code, Name, IsSystemRole, IsActive) VALUES ({approverRoleA.Id}, 0, SYSUTCDATETIME(), {destination.Id}, {"APPROVER_A"}, {"تأییدکننده الف"}, 0, 1)",
                    TestContext.Current.CancellationToken);
                await before.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO CompanyRoles (Id, Ordering, InsertDateTime, TenantId, Code, Name, IsSystemRole, IsActive) VALUES ({approverRoleB.Id}, 0, SYSUTCDATETIME(), {destination.Id}, {"APPROVER_B"}, {"تأییدکننده ب"}, 0, 1)",
                    TestContext.Current.CancellationToken);
                await before.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO CompanyRoles (Id, Ordering, InsertDateTime, TenantId, Code, Name, IsSystemRole, IsActive) VALUES ({memberRole.Id}, 0, SYSUTCDATETIME(), {destination.Id}, {"MEMBER"}, {"عضو"}, 0, 1)",
                    TestContext.Current.CancellationToken);
                await before.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO UserCompanyMemberships (Id, Ordering, InsertDateTime, UserId, TenantId, RoleId, IsActive, CreatedAtUtc) VALUES ({Guid.NewGuid()}, 0, SYSUTCDATETIME(), {approverA.Id}, {destination.Id}, {approverRoleA.Id}, 1, SYSUTCDATETIME())",
                    TestContext.Current.CancellationToken);
                await before.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO UserCompanyMemberships (Id, Ordering, InsertDateTime, UserId, TenantId, RoleId, IsActive, CreatedAtUtc) VALUES ({Guid.NewGuid()}, 0, SYSUTCDATETIME(), {approverB.Id}, {destination.Id}, {approverRoleB.Id}, 1, SYSUTCDATETIME())",
                    TestContext.Current.CancellationToken);

                destinationId = destination.Id;
                requesterId = requester.Id;
                approverAId = approverA.Id;
                approverBId = approverB.Id;
                memberRoleId = memberRole.Id;

                // Upgrade an existing database after destination approver memberships already exist.
                await before.Database.MigrateAsync(TestContext.Current.CancellationToken);
                approverRoleA = await before.CompanyRoles.SingleAsync(item => item.Id == approverRoleA.Id, TestContext.Current.CancellationToken);
                approverRoleB = await before.CompanyRoles.SingleAsync(item => item.Id == approverRoleB.Id, TestContext.Current.CancellationToken);
                memberRole = await before.CompanyRoles.SingleAsync(item => item.Id == memberRole.Id, TestContext.Current.CancellationToken);
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

                var restrictedRequester = new Domain.User("restricted-requester@example.test")
                {
                    Username = "restricted-requester",
                    IsActive = true,
                };
                var restrictedRole = new Domain.CompanyRole
                {
                    TenantId = destinationId,
                    Company = destination,
                    Code = "RESTRICTED",
                    Name = "نقش خارج از سقف واگذاری",
                    IsActive = true,
                };
                var extraPermission = new Domain.PermissionDefinition
                {
                    Code = "test.membership.delegation.extra",
                    Name = "مجوز آزمایشی خارج از سقف",
                };
                before.Users.Add(restrictedRequester);
                before.CompanyRoles.Add(restrictedRole);
                before.PermissionDefinitions.Add(extraPermission);
                before.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
                {
                    Role = restrictedRole,
                    Permission = extraPermission,
                });
                var restrictedRequest = new Domain.CompanyMembershipRequest
                {
                    UserId = restrictedRequester.Id,
                    DestinationTenantId = destinationId,
                    Status = Domain.CompanyMembershipRequestStatuses.Pending,
                    RequestedAtUtc = DateTime.UtcNow,
                };
                before.CompanyMembershipRequests.Add(restrictedRequest);
                await before.SaveChangesAsync(TestContext.Current.CancellationToken);
                restrictedRequesterId = restrictedRequester.Id;
                restrictedRoleId = restrictedRole.Id;
                restrictedRequestId = restrictedRequest.Id;

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

            async Task<IResult> ApproveAsync(Guid approverId, Guid targetRequestId, Guid targetRoleId)
            {
                await using var database = CreateContext(name);
                return await Server.Api.CompanyMembershipRequestEndpoints.ApproveAsync(
                    destinationId,
                    targetRequestId,
                    new DefaultHttpContext(),
                    Principal(approverId),
                    new Server.Api.ApproveCompanyMembershipRequest(targetRoleId),
                    new AllowAntiforgery(),
                    database,
                    new Infrastructure.Tenancy.CompanyAccessService(database),
                    TimeProvider.System,
                    TestContext.Current.CancellationToken);
            }

            var restrictedOutcome = await ApproveAsync(approverAId, restrictedRequestId, restrictedRoleId);
            Assert.Equal(StatusCodes.Status403Forbidden,
                (restrictedOutcome as IStatusCodeHttpResult)?.StatusCode);

            await using (var deniedCheck = CreateContext(name))
            {
                var deniedRequest = await deniedCheck.CompanyMembershipRequests.SingleAsync(
                    item => item.Id == restrictedRequestId, TestContext.Current.CancellationToken);
                Assert.Equal(Domain.CompanyMembershipRequestStatuses.Pending, deniedRequest.Status);
                Assert.False(await deniedCheck.UserCompanyMemberships.AnyAsync(
                    item => item.UserId == restrictedRequesterId && item.TenantId == destinationId,
                    TestContext.Current.CancellationToken));
                Assert.DoesNotContain(await deniedCheck.SecurityAuditEvents.ToListAsync(TestContext.Current.CancellationToken),
                    item => item.EventType == "CompanyMembershipApproved" && item.EntityId == restrictedRequestId.ToString());
            }

            var outcomes = await Task.WhenAll(
                ApproveAsync(approverAId, requestId, memberRoleId),
                ApproveAsync(approverBId, requestId, memberRoleId));
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

                // The migration deliberately refuses to discard membership-request history.
                await Assert.ThrowsAsync<SqlException>(async () =>
                    await check.GetService<IMigrator>().MigrateAsync(
                        PreviousMigration, TestContext.Current.CancellationToken));

                // This isolated test database has no customer data to retain; remove every test
                // membership-request row (approved + still-pending restricted) only after the
                // rollback guard has rejected the destructive downgrade.
                await check.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM [CompanyMembershipRequests] WHERE [Id] IN ({requestId}, {restrictedRequestId})",
                    TestContext.Current.CancellationToken);
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
