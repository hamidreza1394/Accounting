using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

/// <summary>
/// D-078 × D-077/D-066 compatibility: a membership created without a role stays a valid,
/// active membership that the separate <c>roles.assign</c> flow can complete at any time;
/// membership and role assignment never imply each other, and no fiscal access of any kind
/// is created implicitly by either side.
/// </summary>
public sealed class CompanyRoleAssignmentMembershipCompatTests
{
    [Fact]
    public async Task AssignRole_CompletesARolelessMember_AndGrantsAccessThroughTheRole()
    {
        await using var fixture = new Fixture();
        var (company, manager, rolelessMember) = await fixture.SeedCompanyWithManagerAndRolelessMemberAsync();
        var targetRole = fixture.AddRole("ACCOUNTANT", "حسابدار");
        var listPermission = fixture.AddPermissionToRole(targetRole, "users.list");
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyRoleAdministrationEndpoints.AssignMembershipAsync(
            company.Id, targetRole.Id, rolelessMember.Id, new DefaultHttpContext(),
            fixture.Principal(manager.Id), new AllowAntiforgery(), fixture.Database,
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database),
            new Infrastructure.Tenancy.DelegationCeilingService(fixture.Database),
            TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status204NoContent, (result as IStatusCodeHttpResult)?.StatusCode);

        var membership = await fixture.Database.UserCompanyMemberships
            .Include(item => item.Role)
            .SingleAsync(item => item.UserId == rolelessMember.Id && item.TenantId == company.Id);
        Assert.True(membership.IsActive);
        Assert.Equal(targetRole.Id, membership.RoleId);
        Assert.Equal(1, await fixture.Database.UserCompanyMemberships
            .CountAsync(item => item.UserId == rolelessMember.Id));
        Assert.False(await fixture.Database.CompanyMembershipRequests.AnyAsync());

        var access = new Infrastructure.Tenancy.CompanyAccessService(fixture.Database);
        Assert.True(await access.HasPermissionAsync(rolelessMember.Id, company.Id, "users.list"));
        Assert.Equal(["users.list"],
            (await access.GetGrantedPermissionCodesAsync(rolelessMember.Id, company.Id)).OrderBy(code => code));

        // The assignment is audited with the previous (null) role visible.
        var audit = await fixture.Database.SecurityAuditEvents.SingleAsync(item =>
            item.EventType == "UserCompanyRoleAssigned" && item.EntityId == $"{rolelessMember.Id}:{company.Id}");
        Assert.Contains("previousRoleId\":null", audit.DetailsJson);
        Assert.Contains($"roleId\":\"{targetRole.Id}\"", audit.DetailsJson);
    }

    [Fact]
    public async Task AssignRole_NeverCreatesAMembershipOrARequestForAnOutsider()
    {
        await using var fixture = new Fixture();
        var (company, manager, _) = await fixture.SeedCompanyWithManagerAndRolelessMemberAsync();
        var outsider = fixture.NewUser("outsider", active: true);
        fixture.Database.Add(outsider);
        var targetRole = fixture.AddRole("ACCOUNTANT", "حسابدار");
        await fixture.Database.SaveChangesAsync();
        var membershipCount = await fixture.Database.UserCompanyMemberships.CountAsync();

        var result = await Server.Api.CompanyRoleAdministrationEndpoints.AssignMembershipAsync(
            company.Id, targetRole.Id, outsider.Id, new DefaultHttpContext(),
            fixture.Principal(manager.Id), new AllowAntiforgery(), fixture.Database,
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database),
            new Infrastructure.Tenancy.DelegationCeilingService(fixture.Database),
            TimeProvider.System, default);

        // A user without a membership in this company is not assignable: the approval
        // workflow (or D-078 provisioning) must create the membership first.
        Assert.Equal(StatusCodes.Status409Conflict, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(membershipCount, await fixture.Database.UserCompanyMemberships.CountAsync());
        Assert.False(await fixture.Database.CompanyMembershipRequests.AnyAsync());
    }

    [Fact]
    public async Task Provisioning_CreatesNoRolePermissionOrFiscalAccessImplicitly()
    {
        await using var fixture = new Fixture();
        var (company, manager) = await fixture.SeedCompanyManagerOnlyAsync();

        // One fiscal year exists for the company; provisioning must not touch it.
        var fiscalYear = new Domain.FiscalPeriod
        {
            TenantId = company.Id,
            Company = company,
            Title = "سال ۱۴۰۵",
            StartDate = new DateOnly(2027, 3, 21),
            EndDate = new DateOnly(2028, 3, 19),
            Status = Domain.FiscalPeriodStatus.Open,
        };
        fixture.Database.Add(fiscalYear);
        await fixture.Database.SaveChangesAsync();
        var fiscalCount = await fixture.Database.FiscalPeriods.CountAsync();
        var membershipCount = await fixture.Database.UserCompanyMemberships.CountAsync();

        var result = await Server.Api.CompanyUserProvisioningEndpoints.CreateUserForCompanyAsync(
            company.Id, new DefaultHttpContext(), fixture.Principal(manager.Id), fixture.NewCreateRequest("plain.member"),
            new AllowAntiforgery(), fixture.Database, new Infrastructure.Security.PasswordHashService(),
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status201Created, (result as IStatusCodeHttpResult)?.StatusCode);
        var memberId = (result as Created<Server.Api.CompanyUserProvisionedResponse>)!.Value!.UserId;

        // No new membership beyond the one created, no fiscal row, no request, no role
        // grant of any kind (D-078 §4/D-077 §2: membership is not fiscal access).
        Assert.Equal(membershipCount + 1, await fixture.Database.UserCompanyMemberships.CountAsync());
        Assert.Equal(fiscalCount, await fixture.Database.FiscalPeriods.CountAsync());
        Assert.False(await fixture.Database.CompanyMembershipRequests.AnyAsync());

        var access = new Infrastructure.Tenancy.CompanyAccessService(fixture.Database);
        Assert.Empty(await access.GetGrantedPermissionCodesAsync(memberId, company.Id));
        Assert.Empty(await access.GetGrantedPermissionsWithSourcesAsync(memberId, company.Id));
    }

    [Fact]
    public async Task ExistingMembership_IsSufficientForLaterRoleAssignment_NoNewMembershipNeeded()
    {
        await using var fixture = new Fixture();
        var (company, manager, rolelessMember) = await fixture.SeedCompanyWithManagerAndRolelessMemberAsync();
        var firstRole = fixture.AddRole("ACCOUNTANT", "حسابدار");
        fixture.AddPermissionToRole(firstRole, "users.list");
        var secondRole = fixture.AddRole("TREASURER", "خزانه‌دار");
        fixture.AddPermissionToRole(secondRole, "roles.view");
        await fixture.Database.SaveChangesAsync();

        var access = new Infrastructure.Tenancy.CompanyAccessService(fixture.Database);

        // Year N: the member gets their role.
        var first = await Server.Api.CompanyRoleAdministrationEndpoints.AssignMembershipAsync(
            company.Id, firstRole.Id, rolelessMember.Id, new DefaultHttpContext(),
            fixture.Principal(manager.Id), new AllowAntiforgery(), fixture.Database, access,
            new Infrastructure.Tenancy.DelegationCeilingService(fixture.Database), TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status204NoContent, (first as IStatusCodeHttpResult)?.StatusCode);
        var membershipsAfterFirst = await fixture.Database.UserCompanyMemberships.CountAsync();

        // A later assignment (e.g. the role of the next fiscal year, D-077) reuses the SAME
        // membership; no new membership and no request is created for it.
        var second = await Server.Api.CompanyRoleAdministrationEndpoints.AssignMembershipAsync(
            company.Id, secondRole.Id, rolelessMember.Id, new DefaultHttpContext(),
            fixture.Principal(manager.Id), new AllowAntiforgery(), fixture.Database, access,
            new Infrastructure.Tenancy.DelegationCeilingService(fixture.Database), TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status204NoContent, (second as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(membershipsAfterFirst, await fixture.Database.UserCompanyMemberships.CountAsync());
        Assert.Equal(1, await fixture.Database.UserCompanyMemberships
            .CountAsync(item => item.UserId == rolelessMember.Id && item.TenantId == company.Id));
        Assert.False(await fixture.Database.CompanyMembershipRequests.AnyAsync());
        Assert.Equal(2, await fixture.Database.SecurityAuditEvents
            .CountAsync(item => item.EventType == "UserCompanyRoleAssigned"));
    }

    [Fact]
    public async Task RolelessMembers_AreNotMembersOfAnyRole_AndTransferLeavesThemAlone()
    {
        await using var fixture = new Fixture();
        var (company, manager, rolelessMember) = await fixture.SeedCompanyWithManagerAndRolelessMemberAsync();
        var sourceRole = fixture.AddRole("ACCOUNTANT", "حسابدار");
        var member = fixture.NewUser("roled.member", active: true);
        fixture.Database.Add(member);
        fixture.Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = member.Id, TenantId = company.Id, Company = company,
            RoleId = sourceRole.Id, Role = sourceRole, IsActive = true,
        });
        var targetRole = fixture.AddRole("TREASURER", "خزانه‌دار");
        await fixture.Database.SaveChangesAsync();

        // The roleless member is invisible to every role-membership query…
        Assert.Equal(0, await fixture.Database.UserCompanyMemberships
            .CountAsync(item => item.TenantId == company.Id && item.RoleId == sourceRole.Id && item.UserId == rolelessMember.Id));

        // …and a group transfer moves only real members of the source role.
        var result = await Server.Api.CompanyRoleAdministrationEndpoints.TransferMembersAsync(
            company.Id, sourceRole.Id, new DefaultHttpContext(), fixture.Principal(manager.Id),
            new Server.Api.TransferCompanyRoleMembersRequest(targetRole.Id), new AllowAntiforgery(),
            fixture.Database, new Infrastructure.Tenancy.CompanyAccessService(fixture.Database),
            new Infrastructure.Tenancy.DelegationCeilingService(fixture.Database), TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status200OK, (result as IStatusCodeHttpResult)?.StatusCode);

        var membership = await fixture.Database.UserCompanyMemberships
            .SingleAsync(item => item.UserId == rolelessMember.Id && item.TenantId == company.Id);
        Assert.Null(membership.RoleId);
        Assert.True(membership.IsActive);
        var transferred = await fixture.Database.UserCompanyMemberships
            .SingleAsync(item => item.UserId == member.Id && item.TenantId == company.Id);
        Assert.Equal(targetRole.Id, transferred.RoleId);
    }

    private sealed class AllowAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        }

        public Data.DatabaseContext Database { get; }

        private Domain.Company? Company { get; set; }

        private Domain.User? ManagerUser { get; set; }

        private Domain.CompanyRole? ManagerRole { get; set; }

        public Domain.User NewUser(string username, bool active) => new($"{username}@example.test")
        {
            Username = username,
            IsActive = active,
            Password = "private-password",
        };

        public ClaimsPrincipal Principal(Guid userId) => new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("AccountKind", "User"),
        ], "test"));

        public Server.Api.CreateUserRequest NewCreateRequest(string username) => new(
            username, "کاربر", "شرکت", $"{username}@example.test", null,
            "A-Long-Unique-Phrase-1406!", "A-Long-Unique-Phrase-1406!", true);

        public Domain.CompanyRole AddRole(string code, string name)
        {
            var role = new Domain.CompanyRole
            { Company = Company!, TenantId = Company!.Id, Code = code, Name = name, IsActive = true };
            Database.Add(role);
            return role;
        }

        public string AddPermissionToRole(Domain.CompanyRole role, string code)
        {
            // Permission codes are unique: reuse the existing definition when one is already
            // seeded (the delegation ceiling matches by permission id, not by code).
            var permission = Database.PermissionDefinitions.Local
                .FirstOrDefault(candidate => candidate.Code == code)
                ?? Database.PermissionDefinitions.FirstOrDefault(candidate => candidate.Code == code)
                ?? new Domain.PermissionDefinition { Code = code, Name = code };
            if (Database.Entry(permission).State == EntityState.Detached && permission.Id == default)
                Database.Add(permission);
            Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
            { Role = role, RoleId = role.Id, Permission = permission, PermissionId = permission.Id });
            return permission.Id.ToString();
        }

        public async Task<(Domain.Company Company, Domain.User Manager)> SeedCompanyManagerOnlyAsync()
        {
            Company = new Domain.Company { Name = "شرکت آزمون", IsActive = true };
            var createPermission = new Domain.PermissionDefinition
            { Code = Server.Api.CompanyUserProvisioningEndpoints.CreateUserPermissionCode, Name = "ایجاد کاربر برای شرکت" };
            ManagerRole = new Domain.CompanyRole
            { Company = Company, TenantId = Company.Id, Code = "MANAGER", Name = "مدیر شرکت", IsActive = true };
            ManagerUser = NewUser("manager", active: true);
            Database.AddRange(Company, createPermission, ManagerRole, ManagerUser);
            Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
            { Role = ManagerRole, RoleId = ManagerRole.Id, Permission = createPermission, PermissionId = createPermission.Id });
            // The manager also holds the assignment grant and the two codes the tests
            // delegate, so the D-071 ceiling permits assigning roles built from them.
            foreach (var code in new[] { "roles.assign", "users.list", "roles.view" })
            {
                var permission = new Domain.PermissionDefinition { Code = code, Name = code };
                Database.Add(permission);
                Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
                { Role = ManagerRole, RoleId = ManagerRole.Id, Permission = permission, PermissionId = permission.Id });
            }
            Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
            {
                UserId = ManagerUser.Id, TenantId = Company.Id, Company = Company,
                RoleId = ManagerRole.Id, Role = ManagerRole, IsActive = true,
            });
            await Database.SaveChangesAsync();
            return (Company, ManagerUser);
        }

        public async Task<(Domain.Company Company, Domain.User Manager, Domain.User RolelessMember)>
            SeedCompanyWithManagerAndRolelessMemberAsync()
        {
            var (company, manager) = await SeedCompanyManagerOnlyAsync();
            var rolelessMember = NewUser("roleless.member", active: true);
            Database.Add(rolelessMember);
            Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
            {
                UserId = rolelessMember.Id, TenantId = company.Id, Company = company,
                RoleId = null, Role = null, IsActive = true,
            });
            await Database.SaveChangesAsync();
            return (company, manager, rolelessMember);
        }

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}
