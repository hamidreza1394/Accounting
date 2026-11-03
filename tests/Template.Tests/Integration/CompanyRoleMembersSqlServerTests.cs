using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Proves on SQL Server (not only the in-memory provider) that the role member list translates,
/// pages, and that a transfer is atomic and respects the (RoleId, TenantId) composite key.
/// </summary>
public sealed class CompanyRoleMembersSqlServerTests
{
    private const string PreviousRowVersionMigration = "20261103141500_SeedCompanyUserListPermission";

    private static bool SqlServerAvailable()
    {
        if (IntegrationDatabase.UsesSqlServer)
            return true;
        if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
            throw new InvalidOperationException("FARANOU_REQUIRE_SQLSERVER=1 but FARANOU_TEST_SQLSERVER is not set.");
        return false;
    }

    private static Data.DatabaseContext CreateContext(
        string databaseName,
        SaveChangesInterceptor? interceptor = null)
    {
        var builder = new SqlConnectionStringBuilder(IntegrationDatabase.SqlServerConnectionString!)
        {
            InitialCatalog = databaseName,
        };
        var options = new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseSqlServer(builder.ConnectionString, sql =>
                sql.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName));
        if (interceptor is not null)
            options.AddInterceptors(interceptor);
        return new Data.DatabaseContext(options.Options);
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

    [Fact]
    public async Task RoleAndMembershipWritersReturnConflictWhenAnotherWriterWinsTheRowVersion()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_RV_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            Guid companyId;
            Guid adminId;
            Guid roleToEditId;
            Guid assignmentUserId;
            Guid assignmentTargetRoleId;
            Guid assignmentWinnerRoleId;
            Guid assignmentMembershipId;
            Guid transferUserId;
            Guid transferSourceRoleId;
            Guid transferTargetRoleId;
            Guid transferWinnerRoleId;
            Guid transferMembershipId;

            await using (var seed = CreateContext(name))
            {
                await seed.Database.MigrateAsync(cancellationToken);
                var company = new Domain.Company { Name = "شرکت هم‌زمانی", IsActive = true };
                var adminRole = new Domain.Role(Constants.Role.Admin) { IsActive = true };
                var admin = new Domain.User("rowversion-admin@example.test")
                {
                    Username = "rowversion-admin",
                    IsActive = true,
                    Role = adminRole,
                };
                var roleToEdit = new Domain.CompanyRole
                {
                    Company = company, TenantId = company.Id, Code = "EDIT", Name = "قبل از ویرایش", IsActive = true,
                };
                var assignmentCurrentRole = new Domain.CompanyRole
                {
                    Company = company, TenantId = company.Id, Code = "ASSIGN_CURRENT", Name = "نقش فعلی", IsActive = true,
                };
                var assignmentTargetRole = new Domain.CompanyRole
                {
                    Company = company, TenantId = company.Id, Code = "ASSIGN_TARGET", Name = "مقصد", IsActive = true,
                };
                var assignmentWinnerRole = new Domain.CompanyRole
                {
                    Company = company, TenantId = company.Id, Code = "ASSIGN_WINNER", Name = "برنده هم‌زمان", IsActive = true,
                };
                var transferSourceRole = new Domain.CompanyRole
                {
                    Company = company, TenantId = company.Id, Code = "TRANSFER_SOURCE", Name = "مبدأ انتقال", IsActive = true,
                };
                var transferTargetRole = new Domain.CompanyRole
                {
                    Company = company, TenantId = company.Id, Code = "TRANSFER_TARGET", Name = "مقصد انتقال", IsActive = true,
                };
                var transferWinnerRole = new Domain.CompanyRole
                {
                    Company = company, TenantId = company.Id, Code = "TRANSFER_WINNER", Name = "برنده انتقال", IsActive = true,
                };
                var assignmentUser = new Domain.User("rv-assign@example.test")
                {
                    Username = "rv-assign", IsActive = true,
                };
                var transferUser = new Domain.User("rv-transfer@example.test")
                {
                    Username = "rv-transfer", IsActive = true,
                };
                seed.AddRange(
                    company, adminRole, admin, roleToEdit,
                    assignmentCurrentRole, assignmentTargetRole, assignmentWinnerRole,
                    transferSourceRole, transferTargetRole, transferWinnerRole,
                    assignmentUser, transferUser);
                seed.UserCompanyMemberships.AddRange(
                    new Domain.UserCompanyMembership
                    {
                        UserId = assignmentUser.Id, TenantId = company.Id, Role = assignmentCurrentRole,
                        IsActive = true, CreatedAtUtc = DateTime.UtcNow,
                    },
                    new Domain.UserCompanyMembership
                    {
                        UserId = transferUser.Id, TenantId = company.Id, Role = transferSourceRole,
                        IsActive = true, CreatedAtUtc = DateTime.UtcNow,
                    });
                await seed.SaveChangesAsync(cancellationToken);

                companyId = company.Id;
                adminId = admin.Id;
                roleToEditId = roleToEdit.Id;
                assignmentUserId = assignmentUser.Id;
                assignmentTargetRoleId = assignmentTargetRole.Id;
                assignmentWinnerRoleId = assignmentWinnerRole.Id;
                assignmentMembershipId = await seed.UserCompanyMemberships
                    .Where(item => item.UserId == assignmentUserId && item.TenantId == companyId)
                    .Select(item => item.Id).SingleAsync(cancellationToken);
                transferUserId = transferUser.Id;
                transferSourceRoleId = transferSourceRole.Id;
                transferTargetRoleId = transferTargetRole.Id;
                transferWinnerRoleId = transferWinnerRole.Id;
                transferMembershipId = await seed.UserCompanyMemberships
                    .Where(item => item.UserId == transferUserId && item.TenantId == companyId)
                    .Select(item => item.Id).SingleAsync(cancellationToken);

                // Exercise the additive migration against populated tables, not just an empty schema.
                await seed.Database.MigrateAsync(PreviousRowVersionMigration, cancellationToken);
                await seed.Database.MigrateAsync(cancellationToken);
                var roleVersions = await seed.CompanyRoles.AsNoTracking()
                    .Select(item => item.RowVersion).ToArrayAsync(cancellationToken);
                var membershipVersions = await seed.UserCompanyMemberships.AsNoTracking()
                    .Select(item => item.RowVersion).ToArrayAsync(cancellationToken);
                Assert.All(roleVersions, version => Assert.Equal(8, version.Length));
                Assert.All(membershipVersions, version => Assert.Equal(8, version.Length));
                Assert.Equal(roleVersions.Length,
                    roleVersions.Select(version => Convert.ToHexString(version)).Distinct(StringComparer.Ordinal).Count());
                Assert.Equal(membershipVersions.Length,
                    membershipVersions.Select(version => Convert.ToHexString(version)).Distinct(StringComparer.Ordinal).Count());
            }

            var roleInterceptor = new ConcurrentUpdateInterceptor(async token =>
            {
                await using var competing = CreateContext(name);
                var role = await competing.CompanyRoles.SingleAsync(item => item.Id == roleToEditId, token);
                role.Name = "برنده هم‌زمان";
                await competing.SaveChangesAsync(token);
            });
            await using (var requestContext = CreateContext(name, roleInterceptor))
            {
                var result = await Server.Api.CompanyRoleAdministrationEndpoints.UpdateRoleAsync(
                    companyId, roleToEditId, new DefaultHttpContext(), Principal(adminId),
                    new Server.Api.UpdateCompanyRoleRequest("بازنده", true, Array.Empty<string>()),
                    new AllowAntiforgery(), requestContext, TimeProvider.System, cancellationToken);
                Assert.Equal(StatusCodes.Status409Conflict, (result as IStatusCodeHttpResult)?.StatusCode);
            }

            await using (var verifyRole = CreateContext(name))
            {
                Assert.Equal("برنده هم‌زمان",
                    (await verifyRole.CompanyRoles.SingleAsync(item => item.Id == roleToEditId, cancellationToken)).Name);
                Assert.DoesNotContain(await verifyRole.SecurityAuditEvents.ToListAsync(cancellationToken),
                    item => item.EventType == "CompanyRoleUpdated" && item.EntityId == roleToEditId.ToString());
            }

            var assignmentInterceptor = new ConcurrentUpdateInterceptor(async token =>
            {
                await using var competing = CreateContext(name);
                var membership = await competing.UserCompanyMemberships.SingleAsync(item => item.Id == assignmentMembershipId, token);
                membership.RoleId = assignmentWinnerRoleId;
                await competing.SaveChangesAsync(token);
            });
            await using (var requestContext = CreateContext(name, assignmentInterceptor))
            {
                var result = await Server.Api.CompanyRoleAdministrationEndpoints.AssignMembershipAsync(
                    companyId, assignmentTargetRoleId, assignmentUserId, new DefaultHttpContext(),
                    Principal(adminId), new AllowAntiforgery(), requestContext, TimeProvider.System, cancellationToken);
                Assert.Equal(StatusCodes.Status409Conflict, (result as IStatusCodeHttpResult)?.StatusCode);
            }

            await using (var verifyAssignment = CreateContext(name))
            {
                Assert.Equal(assignmentWinnerRoleId,
                    (await verifyAssignment.UserCompanyMemberships.SingleAsync(item => item.Id == assignmentMembershipId, cancellationToken)).RoleId);
                Assert.DoesNotContain(await verifyAssignment.SecurityAuditEvents.ToListAsync(cancellationToken),
                    item => item.EventType == "UserCompanyRoleAssigned" && item.EntityId == $"{assignmentUserId}:{companyId}");
            }

            var transferInterceptor = new ConcurrentUpdateInterceptor(async token =>
            {
                await using var competing = CreateContext(name);
                var membership = await competing.UserCompanyMemberships.SingleAsync(item => item.Id == transferMembershipId, token);
                membership.RoleId = transferWinnerRoleId;
                await competing.SaveChangesAsync(token);
            });
            await using (var requestContext = CreateContext(name, transferInterceptor))
            {
                var result = await Server.Api.CompanyRoleAdministrationEndpoints.TransferMembersAsync(
                    companyId, transferSourceRoleId, new DefaultHttpContext(), Principal(adminId),
                    new Server.Api.TransferCompanyRoleMembersRequest(transferTargetRoleId),
                    new AllowAntiforgery(), requestContext, TimeProvider.System, cancellationToken);
                Assert.Equal(StatusCodes.Status409Conflict, (result as IStatusCodeHttpResult)?.StatusCode);
            }

            await using (var verifyTransfer = CreateContext(name))
            {
                Assert.Equal(transferWinnerRoleId,
                    (await verifyTransfer.UserCompanyMemberships.SingleAsync(item => item.Id == transferMembershipId, cancellationToken)).RoleId);
                Assert.DoesNotContain(await verifyTransfer.SecurityAuditEvents.ToListAsync(cancellationToken),
                    item => item.EventType == "UserCompanyRoleAssigned" && item.EntityId == $"{transferUserId}:{companyId}");
            }
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    private sealed class ConcurrentUpdateInterceptor(
        Func<CancellationToken, Task> concurrentUpdate) : SaveChangesInterceptor
    {
        private int _triggered;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _triggered, 1) == 0)
                await concurrentUpdate(cancellationToken);
            return result;
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
