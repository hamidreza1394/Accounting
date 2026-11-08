using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

/// <summary>
/// Company-scoped role administration under the delegation ceiling (D-071): a company
/// manager acts only inside their own company, only with the roles.* permission codes, and
/// only with permission sets that stay inside the ceiling they themselves hold.
/// </summary>
public sealed class CompanyRoleDelegationCeilingEndpointTests
{
    private const string ViewPermission = Server.Api.CompanyRoleAdministrationEndpoints.ViewRolesPermission;
    private const string CreatePermission = Server.Api.CompanyRoleAdministrationEndpoints.CreateRolesPermission;
    private const string UpdatePermission = Server.Api.CompanyRoleAdministrationEndpoints.UpdateRolesPermission;
    private const string AssignPermission = Server.Api.CompanyRoleAdministrationEndpoints.AssignRolesPermission;

    [Fact]
    public async Task CreateRole_CompanyManagerInsideCeiling_SucceedsAndWritesAudit()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission(CreatePermission);
        fixture.AddPermission("users.list");
        var manager = fixture.AddCompanyManager(CreatePermission, "users.list");
        await fixture.Database.SaveChangesAsync();

        var request = new Server.Api.CreateCompanyRoleRequest(
            "VIEWER", "نمایشگر", null, ["users.list"]);
        var result = await fixture.CreateRole(manager.Id, request);

        Assert.Equal(StatusCodes.Status201Created, (result as IStatusCodeHttpResult)?.StatusCode);
        var role = await fixture.Database.CompanyRoles
            .Include(item => item.RolePermissions)
                .ThenInclude(rolePermission => rolePermission.Permission)
            .SingleAsync(item => item.TenantId == fixture.CompanyId && item.Code == "VIEWER");
        Assert.Equal(["users.list"], role.RolePermissions
            .Select(rolePermission => rolePermission.Permission.Code).OrderBy(code => code).ToArray());
        Assert.Contains(await fixture.Database.SecurityAuditEvents.ToListAsync(),
            audit => audit.EventType == "CompanyRoleCreated" && audit.UserId == manager.Id);
    }

    [Fact]
    public async Task CreateRole_CompanyManagerBeyondCeiling_IsRefusedAndCreatesNothing()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission(CreatePermission);
        fixture.AddPermission("users.list");
        fixture.AddPermission("trade.form.party.view");
        fixture.AddPermission("trade.form.party.create");
        var manager = fixture.AddCompanyManager(CreatePermission, "users.list");
        await fixture.Database.SaveChangesAsync();

        var request = new Server.Api.CreateCompanyRoleRequest(
            "TRADE_CLERK", "کارشناس بازرگانی", null, ["trade.form.party.view", "trade.form.party.create"]);
        var result = await fixture.CreateRole(manager.Id, request);

        Assert.Equal(StatusCodes.Status403Forbidden, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.False(await fixture.Database.CompanyRoles.AnyAsync(role => role.Code == "TRADE_CLERK"));
        Assert.False(await fixture.Database.SecurityAuditEvents.AnyAsync());
    }

    [Fact]
    public async Task CreateRole_WithoutTheCreatePermission_IsRefusedForTheCompanyMember()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission("users.list");
        var member = fixture.AddCompanyManager("users.list");
        await fixture.Database.SaveChangesAsync();

        var request = new Server.Api.CreateCompanyRoleRequest(
            "VIEWER", "نمایشگر", null, ["users.list"]);
        var result = await fixture.CreateRole(member.Id, request);

        Assert.Equal(StatusCodes.Status403Forbidden, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.False(await fixture.Database.CompanyRoles.AnyAsync(role => role.Code == "VIEWER"));
    }

    [Fact]
    public async Task CreateRole_WithoutAMembership_ReturnsTheSame404AsAnUnknownCompany()
    {
        await using var fixture = new Fixture();
        var outsider = fixture.AddUser("outsider");
        fixture.AddCompany();
        await fixture.Database.SaveChangesAsync();

        var request = new Server.Api.CreateCompanyRoleRequest(
            "VIEWER", "نمایشگر", null, Array.Empty<string>());
        var result = await fixture.CreateRole(outsider.Id, request);

        Assert.Equal(StatusCodes.Status404NotFound, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_CompanyManagerCannotAddPermissionsBeyondTheCeiling()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission(UpdatePermission);
        fixture.AddPermission("users.list");
        fixture.AddPermission("trade.form.party.view");
        var manager = fixture.AddCompanyManager(UpdatePermission, "users.list");
        var role = fixture.AddRoleWithPermissions("VIEWER", "users.list");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.UpdateRole(manager.Id, role.Id,
            new Server.Api.UpdateCompanyRoleRequest(role.Name, true, ["users.list", "trade.form.party.view"]));

        Assert.Equal(StatusCodes.Status403Forbidden, (result as IStatusCodeHttpResult)?.StatusCode);
        await using var reader = fixture.NewContext();
        var unchanged = await reader.CompanyRoles
            .Include(item => item.RolePermissions)
                .ThenInclude(rolePermission => rolePermission.Permission)
            .SingleAsync(item => item.Id == role.Id);
        Assert.Equal(["users.list"], unchanged.RolePermissions
            .Select(rolePermission => rolePermission.Permission.Code).ToArray());
    }

    [Fact]
    public async Task UpdateRole_CompanyManagerInsideCeiling_Succeeds()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission(UpdatePermission);
        fixture.AddPermission("users.list");
        fixture.AddPermission(ViewPermission);
        var manager = fixture.AddCompanyManager(UpdatePermission, "users.list", ViewPermission);
        var role = fixture.AddRoleWithPermissions("VIEWER", "users.list");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.UpdateRole(manager.Id, role.Id,
            new Server.Api.UpdateCompanyRoleRequest("نمایشگر ارشد", true, ["users.list", ViewPermission]));

        Assert.Equal(StatusCodes.Status204NoContent, (result as IStatusCodeHttpResult)?.StatusCode);
        await using var reader = fixture.NewContext();
        var updated = await reader.CompanyRoles
            .Include(item => item.RolePermissions)
            .SingleAsync(item => item.Id == role.Id);
        Assert.Equal(2, updated.RolePermissions.Count);
        Assert.Equal("نمایشگر ارشد", updated.Name);
    }

    [Fact]
    public async Task UpdateRole_SystemRoleRemainsImmutableForACompanyManager()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission(UpdatePermission);
        fixture.AddPermission("users.list");
        var manager = fixture.AddCompanyManager(UpdatePermission, "users.list");
        var system = fixture.AddRoleWithPermissions("company-admin", "users.list");
        system.IsSystemRole = true;
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.UpdateRole(manager.Id, system.Id,
            new Server.Api.UpdateCompanyRoleRequest("هک‌شده", true, ["users.list"]));

        Assert.Equal(StatusCodes.Status409Conflict, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.True((await fixture.Database.CompanyRoles.SingleAsync(item => item.Id == system.Id)).IsSystemRole);
    }

    [Fact]
    public async Task AssignMembership_CompanyManagerWithinCeiling_SucceedsAndWritesAudit()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission(AssignPermission);
        fixture.AddPermission("users.list");
        var manager = fixture.AddCompanyManager(AssignPermission, "users.list");
        var role = fixture.AddRoleWithPermissions("VIEWER", "users.list");
        var member = fixture.AddMember(fixture.AddRole("OLD"), "employee");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.AssignMembership(manager.Id, role.Id, member.Id);

        Assert.Equal(StatusCodes.Status204NoContent, (result as IStatusCodeHttpResult)?.StatusCode);
        await using var reader = fixture.NewContext();
        Assert.Equal(role.Id, (await reader.UserCompanyMemberships
            .SingleAsync(membership => membership.UserId == member.Id)).RoleId);
        Assert.Contains(await reader.SecurityAuditEvents.ToListAsync(),
            audit => audit.EventType == "UserCompanyRoleAssigned" && audit.UserId == manager.Id);
    }

    [Fact]
    public async Task AssignMembership_CompanyManagerCannotAssignARoleBeyondTheirCeiling()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission(AssignPermission);
        fixture.AddPermission("users.list");
        fixture.AddPermission("trade.form.party.view");
        var manager = fixture.AddCompanyManager(AssignPermission, "users.list");
        var powerfulRole = fixture.AddRoleWithPermissions("TRADE", "trade.form.party.view");
        var member = fixture.AddMember(fixture.AddRole("OLD"), "employee");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.AssignMembership(manager.Id, powerfulRole.Id, member.Id);

        Assert.Equal(StatusCodes.Status403Forbidden, (result as IStatusCodeHttpResult)?.StatusCode);
        await using var reader = fixture.NewContext();
        Assert.Equal("OLD", (await reader.UserCompanyMemberships
            .Include(membership => membership.Role)
            .SingleAsync(membership => membership.UserId == member.Id)).Role.Code);
        Assert.False(await reader.SecurityAuditEvents.AnyAsync());
    }

    [Fact]
    public async Task AssignMembership_CompanyManagerHoldingTheWholeCatalog_StillCannotAssignASystemRole()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission(AssignPermission);
        fixture.AddPermission("users.list");
        fixture.AddPermission("trade.form.party.view");
        // The manager's ceiling deliberately covers every permission the system role holds:
        // the system-role refusal must not depend on the ceiling subset check (D-071).
        var systemRole = fixture.AddRoleWithPermissions("company-admin", "users.list", "trade.form.party.view");
        systemRole.IsSystemRole = true;
        var manager = fixture.AddCompanyManager(AssignPermission, "users.list", "trade.form.party.view");
        var member = fixture.AddMember(fixture.AddRole("OLD"), "employee");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.AssignMembership(manager.Id, systemRole.Id, member.Id);

        Assert.Equal(StatusCodes.Status409Conflict, (result as IStatusCodeHttpResult)?.StatusCode);
        await using var reader = fixture.NewContext();
        Assert.Equal("OLD", (await reader.UserCompanyMemberships
            .Include(membership => membership.Role)
            .SingleAsync(membership => membership.UserId == member.Id)).Role.Code);
        Assert.False(await reader.SecurityAuditEvents.AnyAsync());
    }

    [Fact]
    public async Task AssignMembership_CompanyManagerCannotChangeTheirOwnMembership()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission(AssignPermission);
        fixture.AddPermission("users.list");
        var manager = fixture.AddCompanyManager(AssignPermission, "users.list");
        var target = fixture.AddRoleWithPermissions("VIEWER", "users.list");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.AssignMembership(manager.Id, target.Id, manager.Id);

        Assert.Equal(StatusCodes.Status403Forbidden, (result as IStatusCodeHttpResult)?.StatusCode);
        await using var reader = fixture.NewContext();
        Assert.Equal("MANAGER", (await reader.UserCompanyMemberships
            .Include(membership => membership.Role).SingleAsync()).Role.Code);
    }

    [Fact]
    public async Task AssignMembership_CompanyManagerCannotReactivateAnInactiveMembership()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission(AssignPermission);
        fixture.AddPermission("users.list");
        var manager = fixture.AddCompanyManager(AssignPermission, "users.list");
        var role = fixture.AddRoleWithPermissions("VIEWER", "users.list");
        var former = fixture.AddMember(fixture.AddRole("OLD"), "former", membershipActive: false);
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.AssignMembership(manager.Id, role.Id, former.Id);

        Assert.Equal(StatusCodes.Status409Conflict, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.False((await fixture.Database.UserCompanyMemberships
            .SingleAsync(membership => membership.UserId == former.Id)).IsActive);
    }

    [Fact]
    public async Task TransferMembers_CompanyManagerCannotMoveSelfOrBeyondTheCeiling()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission(AssignPermission);
        fixture.AddPermission("users.list");
        fixture.AddPermission("trade.form.party.view");
        var manager = fixture.AddCompanyManager(AssignPermission, "users.list");
        var managerRole = fixture.Database.CompanyRoles.Local
            .Single(role => role.Code == "MANAGER" && role.TenantId == fixture.CompanyId);
        var source = fixture.AddRole("OLD");
        var beyond = fixture.AddRoleWithPermissions("TRADE", "trade.form.party.view");
        var clerk = fixture.AddMember(source, "clerk");
        await fixture.Database.SaveChangesAsync();

        var beyondResult = await fixture.TransferMembers(manager.Id, source.Id, beyond.Id);
        Assert.Equal(StatusCodes.Status403Forbidden, (beyondResult as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(source.Id, (await fixture.Database.UserCompanyMemberships
            .SingleAsync(membership => membership.UserId == clerk.Id)).RoleId);

        // A legitimate target, but the source role contains the manager's own membership:
        // the batch is refused before anything is written.
        var inside = fixture.AddRoleWithPermissions("VIEWER", "users.list");
        await fixture.Database.SaveChangesAsync();
        var selfTransfer = await fixture.TransferMembers(manager.Id, managerRole.Id, inside.Id);
        Assert.Equal(StatusCodes.Status403Forbidden, (selfTransfer as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(managerRole.Id, (await fixture.Database.UserCompanyMemberships
            .SingleAsync(membership => membership.UserId == manager.Id)).RoleId);
        Assert.False(await fixture.Database.SecurityAuditEvents.AnyAsync());
    }

    [Fact]
    public async Task GetRoles_CompanyManagerSeesOnlyTheRolesOfTheirOwnCompany()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission(ViewPermission);
        var manager = fixture.AddCompanyManager(ViewPermission);
        var ownRole = fixture.AddRoleWithPermissions("VIEWER", ViewPermission);
        var otherCompanyRole = fixture.AddRoleInOtherCompany("FOREIGN");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.GetRoles(manager.Id);
        var ok = Assert.IsType<Ok<Server.Api.CompanyRoleResponse[]>>(result);
        Assert.Equal(2, ok.Value!.Length); // the manager's own MANAGER role plus VIEWER
        Assert.Contains(ok.Value!, role => role.Id == ownRole.Id);
        Assert.DoesNotContain(ok.Value!, role => role.Code == "FOREIGN");

        var foreignRoleMembers = await fixture.GetRoleMembers(manager.Id, otherCompanyRole.Id);
        Assert.Equal(StatusCodes.Status404NotFound, (foreignRoleMembers as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task GetPermissionCatalog_CompanyActorSeesOnlyTheirCeilingCodes()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission(ViewPermission);
        fixture.AddPermission("users.list");
        fixture.AddPermission("trade.reports.multicompany.view");
        fixture.AddPermission("corporate-groups.manage");
        var manager = fixture.AddCompanyManager(ViewPermission, "users.list");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.GetPermissionCatalog(manager.Id);
        var ok = Assert.IsType<Ok<Server.Api.PermissionCatalogItemResponse[]>>(result);
        var codes = ok.Value!.Select(item => item.Code).ToArray();

        Assert.Equal(["roles.view", "users.list"], codes.Order().ToArray());
        Assert.DoesNotContain("trade.reports.multicompany.view", codes);
        Assert.DoesNotContain("corporate-groups.manage", codes);
    }

    [Fact]
    public async Task GetPermissionCatalog_UserWithoutAnyMembership_Returns404()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission("users.list");
        var outsider = fixture.AddUser("outsider");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.GetPermissionCatalog(outsider.Id);

        Assert.Equal(StatusCodes.Status404NotFound, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task GetTemplates_CompanyActorOnlySeesTemplatesInsideTheirCeiling()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission("trade.form.party.view");
        fixture.AddPermission("trade.form.party.create");
        var manager = fixture.AddCompanyManager("trade.form.party.view");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.GetTemplates(manager.Id);
        var ok = Assert.IsType<Ok<Server.Api.CompanyRoleTemplateResponse[]>>(result);
        var templates = ok.Value!;
        var viewer = Assert.Single(templates, template => template.Code == "trade.viewer");
        Assert.Equal(["trade.form.party.view"], viewer.PermissionCodes);
        Assert.All(templates, template =>
            Assert.All(template.PermissionCodes, code => Assert.Equal("trade.form.party.view", code)));
    }

    [Fact]
    public async Task RoleEndpoints_Return401ForAnAnonymousPrincipal()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        await fixture.Database.SaveChangesAsync();

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        var roles = await fixture.GetRoles(anonymous);
        Assert.Equal(StatusCodes.Status401Unauthorized, (roles as IStatusCodeHttpResult)?.StatusCode);

        var create = await fixture.CreateRole(anonymous,
            new Server.Api.CreateCompanyRoleRequest("X", "X", null, Array.Empty<string>()));
        Assert.Equal(StatusCodes.Status401Unauthorized, (create as IStatusCodeHttpResult)?.StatusCode);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Database = NewContext();
        }

        private readonly string _databaseName = Guid.NewGuid().ToString();

        public Data.DatabaseContext NewContext() => new(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseInMemoryDatabase(_databaseName).Options);

        public Data.DatabaseContext Database { get; }
        public Guid CompanyId { get; private set; }

        public Infrastructure.Tenancy.CompanyAccessService CompanyAccess => new(Database);
        public Infrastructure.Tenancy.DelegationCeilingService CeilingService => new(Database);

        public Domain.User AddUser(string username)
        {
            var user = new Domain.User($"{username}@example.test")
            {
                Username = username,
                IsActive = true,
                Password = "x",
            };
            Database.Users.Add(user);
            return user;
        }

        public void AddCompany()
        {
            var company = new Domain.Company { Name = "شرکت نمونه", IsActive = true };
            Database.Companies.Add(company);
            CompanyId = company.Id;
        }

        private readonly Dictionary<string, Guid> _permissionIds = new();

        private Guid PermissionId(string code) => _permissionIds[code];

        public void AddPermission(string code)
        {
            var permission = new Domain.PermissionDefinition
            {
                Code = code,
                Name = code,
                Description = "test permission",
            };
            Database.PermissionDefinitions.Add(permission);
            _permissionIds[code] = permission.Id;
        }

        /// <summary>A company manager: active user, own role with the given codes, active membership.
        /// Every permission code must already be registered through <see cref="AddPermission" />.</summary>
        public Domain.User AddCompanyManager(params string[] permissionCodes)
        {
            var managerRole = new Domain.CompanyRole
            {
                TenantId = CompanyId,
                Code = "MANAGER",
                Name = "مدیر شرکت",
                IsActive = true,
            };
            Database.CompanyRoles.Add(managerRole);
            foreach (var code in permissionCodes)
            {
                Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
                {
                    Role = managerRole,
                    PermissionId = PermissionId(code),
                });
            }

            var user = AddUser("manager");
            Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
            {
                UserId = user.Id,
                TenantId = CompanyId,
                Role = managerRole,
                RoleId = managerRole.Id,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
            });
            return user;
        }

        public Domain.CompanyRole AddRoleWithPermissions(string code, params string[] permissionCodes)
        {
            var role = new Domain.CompanyRole
            {
                TenantId = CompanyId,
                Code = code,
                Name = code,
                IsActive = true,
            };
            Database.CompanyRoles.Add(role);
            foreach (var permissionCode in permissionCodes)
            {
                Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
                {
                    Role = role,
                    PermissionId = PermissionId(permissionCode),
                });
            }

            return role;
        }

        public Domain.CompanyRole AddRole(string code, bool active = true)
        {
            var role = new Domain.CompanyRole { TenantId = CompanyId, Code = code, Name = code, IsActive = active };
            Database.CompanyRoles.Add(role);
            return role;
        }

        public Domain.CompanyRole AddRoleInOtherCompany(string code)
        {
            var other = new Domain.Company { Name = "شرکت دیگر", IsActive = true };
            Database.Companies.Add(other);
            var role = new Domain.CompanyRole { TenantId = other.Id, Company = other, Code = code, Name = code, IsActive = true };
            Database.CompanyRoles.Add(role);
            return role;
        }

        public Domain.User AddMember(Domain.CompanyRole role, string username, bool membershipActive = true)
        {
            var user = new Domain.User($"{username}@example.test") { Username = username, IsActive = true, Password = "x" };
            Database.Users.Add(user);
            Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
            {
                UserId = user.Id,
                TenantId = role.TenantId,
                Role = role,
                RoleId = role.Id,
                IsActive = membershipActive,
                CreatedAtUtc = DateTime.UtcNow,
            });
            return user;
        }

        public Task<IResult> GetRoles(ClaimsPrincipal principal) =>
            Server.Api.CompanyRoleAdministrationEndpoints.GetRolesAsync(
                CompanyId, new DefaultHttpContext(), principal, Database, CompanyAccess, default);

        public Task<IResult> GetRoles(Guid userId) => GetRoles(Principal(userId));

        public Task<IResult> GetRoleMembers(Guid userId, Guid roleId) =>
            Server.Api.CompanyRoleAdministrationEndpoints.GetRoleMembersAsync(
                CompanyId, roleId, null, null, null, new DefaultHttpContext(), Principal(userId),
                Database, CompanyAccess, default);

        public Task<IResult> CreateRole(ClaimsPrincipal principal, Server.Api.CreateCompanyRoleRequest request) =>
            Server.Api.CompanyRoleAdministrationEndpoints.CreateRoleAsync(
                CompanyId, new DefaultHttpContext(), principal, request,
                new AllowAntiforgery(), Database, CompanyAccess, CeilingService, TimeProvider.System, default);

        public Task<IResult> CreateRole(Guid userId, Server.Api.CreateCompanyRoleRequest request) =>
            CreateRole(Principal(userId), request);

        public Task<IResult> UpdateRole(Guid userId, Guid roleId, Server.Api.UpdateCompanyRoleRequest request) =>
            Server.Api.CompanyRoleAdministrationEndpoints.UpdateRoleAsync(
                CompanyId, roleId, new DefaultHttpContext(), Principal(userId), request,
                new AllowAntiforgery(), Database, CompanyAccess, CeilingService, TimeProvider.System, default);

        public Task<IResult> AssignMembership(Guid userId, Guid roleId, Guid targetUserId) =>
            Server.Api.CompanyRoleAdministrationEndpoints.AssignMembershipAsync(
                CompanyId, roleId, targetUserId, new DefaultHttpContext(), Principal(userId),
                new AllowAntiforgery(), Database, CompanyAccess, CeilingService, TimeProvider.System, default);

        public Task<IResult> TransferMembers(Guid userId, Guid sourceRoleId, Guid targetRoleId) =>
            Server.Api.CompanyRoleAdministrationEndpoints.TransferMembersAsync(
                CompanyId, sourceRoleId, new DefaultHttpContext(), Principal(userId),
                new Server.Api.TransferCompanyRoleMembersRequest(targetRoleId),
                new AllowAntiforgery(), Database, CompanyAccess, CeilingService, TimeProvider.System, default);

        public Task<IResult> GetPermissionCatalog(Guid userId) =>
            Server.Api.CompanyRoleAdministrationEndpoints.GetPermissionCatalogAsync(
                new DefaultHttpContext(), Principal(userId), Database, CeilingService, default);

        public Task<IResult> GetTemplates(Guid userId) =>
            Server.Api.CompanyRoleAdministrationEndpoints.GetTemplatesAsync(
                new DefaultHttpContext(), Principal(userId), Database, CeilingService, default);

        public ClaimsPrincipal Principal(Guid id) => new(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim("AccountKind", "User"),
            ], "test"));

        public ValueTask DisposeAsync() => Database.DisposeAsync();
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
