using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>Verifies membership discovery and approval read queries against SQL Server.</summary>
public sealed class CompanyMembershipRequestReadSqlServerTests
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

    private static ClaimsPrincipal Principal(Guid userId) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim("AccountKind", "User"),
    ], "test"));

    [Fact]
    public async Task MembershipDiscoveryAssignableRolesApproversAndHistoryTranslateOnSqlServer()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_MR_Read_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            var boundary = Guid.NewGuid();
            var foreignBoundary = Guid.NewGuid();
            var source = new Domain.Company
            {
                Name = "شرکت مبدأ SQL",
                IsActive = true,
                CustomerBoundaryId = boundary,
            };
            var destination = new Domain.Company
            {
                Name = "شرکت مقصد SQL",
                IsActive = true,
                CustomerBoundaryId = boundary,
            };
            var foreign = new Domain.Company
            {
                Name = destination.Name,
                IsActive = true,
                CustomerBoundaryId = foreignBoundary,
            };
            var pendingDestination = new Domain.Company
            {
                Name = "شرکت دارای درخواست SQL",
                IsActive = true,
                CustomerBoundaryId = boundary,
            };
            var requester = new Domain.User("membership-read-requester@example.test")
            {
                Username = "mr-requester",
                FirstName = "درخواست‌کننده",
                LastName = "آزمون",
                IsActive = true,
            };
            var approver = new Domain.User("membership-read-approver@example.test")
            {
                Username = "mr-approver",
                FirstName = "مدیر",
                LastName = "مقصد",
                IsActive = true,
            };
            var sourceRole = new Domain.CompanyRole
            {
                TenantId = source.Id, Company = source, Code = "SOURCE", Name = "مبدأ", IsActive = true,
            };
            var approverRole = new Domain.CompanyRole
            {
                TenantId = destination.Id, Company = destination, Code = "APPROVER", Name = "تأییدکننده", IsActive = true,
            };
            var assignableRole = new Domain.CompanyRole
            {
                TenantId = destination.Id, Company = destination, Code = "MEMBER", Name = "عضو", IsActive = true,
            };
            var restrictedRole = new Domain.CompanyRole
            {
                TenantId = destination.Id, Company = destination, Code = "RESTRICTED", Name = "خارج از سقف", IsActive = true,
            };
            var approvalPermission = await database.PermissionDefinitions
                .SingleAsync(item => item.Code == Server.Api.CompanyMembershipRequestEndpoints.ApprovalPermissionCode,
                    cancellationToken);
            var extraPermission = new Domain.PermissionDefinition
            {
                Code = "test.membership.read.extra",
                Name = "مجوز خارج از سقف",
            };

            database.AddRange(source, destination, foreign, pendingDestination, requester, approver,
                sourceRole, approverRole, assignableRole, restrictedRole, extraPermission);
            database.CompanyRolePermissions.AddRange(
                new Domain.CompanyRolePermission
                {
                    Role = approverRole, RoleId = approverRole.Id,
                    Permission = approvalPermission, PermissionId = approvalPermission.Id,
                },
                new Domain.CompanyRolePermission
                {
                    Role = restrictedRole, RoleId = restrictedRole.Id,
                    Permission = extraPermission, PermissionId = extraPermission.Id,
                });
            database.UserCompanyMemberships.AddRange(
                new Domain.UserCompanyMembership
                {
                    UserId = requester.Id, TenantId = source.Id, Company = source,
                    Role = sourceRole, RoleId = sourceRole.Id, IsActive = true,
                },
                new Domain.UserCompanyMembership
                {
                    UserId = approver.Id, TenantId = destination.Id, Company = destination,
                    Role = approverRole, RoleId = approverRole.Id, IsActive = true,
                });
            var pending = new Domain.CompanyMembershipRequest
            {
                UserId = requester.Id,
                DestinationTenantId = pendingDestination.Id,
                DestinationCompany = pendingDestination,
                Status = Domain.CompanyMembershipRequestStatuses.Pending,
                RequestedAtUtc = new DateTime(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc),
            };
            var approved = new Domain.CompanyMembershipRequest
            {
                UserId = requester.Id,
                DestinationTenantId = destination.Id,
                DestinationCompany = destination,
                Status = Domain.CompanyMembershipRequestStatuses.Approved,
                RequestedAtUtc = new DateTime(2026, 11, 3, 8, 0, 0, DateTimeKind.Utc),
                DecidedAtUtc = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc),
                DecidedByUserId = approver.Id,
                ApprovedRoleId = assignableRole.Id,
                ApprovedRole = assignableRole,
                DecisionReason = "تأیید SQL",
            };
            database.CompanyMembershipRequests.AddRange(pending, approved);
            await database.SaveChangesAsync(cancellationToken);

            var access = new Infrastructure.Tenancy.CompanyAccessService(database);
            var destinations = await Server.Api.CompanyMembershipRequestEndpoints.GetDestinationsAsync(
                new DefaultHttpContext(), Principal(requester.Id), database, access, destination.Name,
                page: 1, pageSize: 25, cancellationToken);
            var destinationResponse = Assert.IsType<Ok<Server.Api.MembershipDestinationListResponse>>(destinations).Value!;
            var discovered = Assert.Single(destinationResponse.Items);
            Assert.Equal(destination.Id, discovered.CompanyId);
            Assert.DoesNotContain(destinationResponse.Items, item => item.CompanyId == foreign.Id);

            var assignable = await Server.Api.CompanyMembershipRequestEndpoints.GetAssignableRolesAsync(
                destination.Id, new DefaultHttpContext(), Principal(approver.Id), database, access,
                page: 1, pageSize: 100, cancellationToken);
            var roleResponse = Assert.IsType<Ok<Server.Api.AssignableCompanyRoleListResponse>>(assignable).Value!;
            Assert.Contains(roleResponse.Items, item => item.Id == assignableRole.Id);
            Assert.DoesNotContain(roleResponse.Items, item => item.Id == restrictedRole.Id);
            Assert.DoesNotContain(roleResponse.Items, item => item.Id == approverRole.Id);

            var approvers = await Server.Api.CompanyMembershipRequestEndpoints.GetApproversAsync(
                destination.Id, new DefaultHttpContext(), Principal(approver.Id), database, access,
                page: 1, pageSize: 25, cancellationToken);
            var approverResponse = Assert.IsType<Ok<Server.Api.MembershipApproverListResponse>>(approvers).Value!;
            Assert.Equal(1, approverResponse.TotalCount);
            Assert.Equal(approver.Id, Assert.Single(approverResponse.Items).UserId);

            var history = await Server.Api.CompanyMembershipRequestEndpoints.GetRequestHistoryAsync(
                destination.Id, new DefaultHttpContext(), Principal(approver.Id), database, access,
                status: "Approved", page: 1, pageSize: 25, cancellationToken);
            var historyResponse = Assert.IsType<Ok<Server.Api.CompanyMembershipRequestHistoryListResponse>>(history).Value!;
            var historyItem = Assert.Single(historyResponse.Items);
            Assert.Equal(approved.Id, historyItem.Id);
            Assert.Equal(DateTimeKind.Utc, historyItem.RequestedAtUtc.Kind);
            Assert.Equal(DateTimeKind.Utc, historyItem.DecidedAtUtc!.Value.Kind);
            Assert.EndsWith("Z", JsonSerializer.Serialize(historyItem.DecidedAtUtc));
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }
}
