using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

/// <summary>
/// تست‌های «پیش‌نمایش اثر تغییر مجوزهای نقش» (Stage 2، PR-D، Issue #444):
/// محاسبهٔ سمت سرور و فقط-خواندنی؛ هیچ تغییر ذخیره یا Audit نوشته نمی‌شود و
/// در زمان ذخیره، همهٔ کنترل‌های امنیتی دوباره اجرا می‌شوند (D-071).
/// </summary>
public sealed class RolePermissionPreviewEndpointTests
{
    private const string UpdatePermission = Server.Api.CompanyRoleAdministrationEndpoints.UpdateRolesPermission;

    [Fact]
    public async Task Preview_ReportsDiffAffectedMembersSensitiveCodesAndCeilingViolations()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission(UpdatePermission);
        fixture.AddPermission("users.list");
        fixture.AddPermission("trade.form.party.view");
        fixture.AddPermission("trade.form.party.delete");

        // The manager's ceiling holds only the two first codes: the delete code is beyond it.
        var manager = fixture.AddCompanyManager(UpdatePermission, "users.list");
        var role = fixture.AddRoleWithPermissions("VIEWER", "users.list");
        fixture.AddMember(fixture.AddRole("OLD"), "member-1");
        // Two active members sit on the previewed role itself.
        fixture.AddMember(role, "member-2");
        fixture.AddMember(role, "member-3");
        await fixture.Database.SaveChangesAsync();

        var result = await CallAsync(fixture, manager.Id, role.Id, ["users.list", "trade.form.party.delete"]);

        Assert.Equal(StatusCodes.Status200OK, Status(result));
        var ok = Assert.IsType<Ok<Server.Api.RolePermissionPreviewResponse>>(result).Value!;
        Assert.Equal(role.Id, ok.RoleId);
        Assert.Equal(2, ok.ActiveMemberCount);
        Assert.Equal(["users.list"], ok.CurrentPermissionCodes);
        Assert.Equal(["trade.form.party.delete"], ok.AddedPermissionCodes);
        Assert.Empty(ok.RemovedPermissionCodes);
        // Destructive operations are sensitive (PERMISSION_CATALOG §2 taxonomy).
        Assert.Equal(["trade.form.party.delete"], ok.SensitiveChangedPermissionCodes);
        // The candidate code outside the manager's ceiling is reported, not silently trimmed:
        // the preview never blocks the save path, the save itself re-enforces D-071.
        Assert.Equal(["trade.form.party.delete"], ok.CeilingViolatedPermissionCodes);
    }

    [Fact]
    public async Task Preview_ForTheInstallationAdministratorHasNoCeilingAndReportsRemovals()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission(UpdatePermission);
        fixture.AddPermission("users.list");
        fixture.AddPermission("trade.form.party.close");
        var admin = fixture.AddAdministrator();
        var role = fixture.AddRoleWithPermissions("VIEWER", "users.list", "trade.form.party.close");
        await fixture.Database.SaveChangesAsync();

        var result = await CallAsync(fixture, admin.Id, role.Id, ["users.list"]);

        var ok = Assert.IsType<Ok<Server.Api.RolePermissionPreviewResponse>>(result).Value!;
        Assert.Empty(ok.AddedPermissionCodes);
        Assert.Equal(["trade.form.party.close"], ok.RemovedPermissionCodes);
        Assert.Equal(["trade.form.party.close"], ok.SensitiveChangedPermissionCodes);
        Assert.Empty(ok.CeilingViolatedPermissionCodes);
    }

    [Fact]
    public async Task Preview_WritesNothingAndAuditsNothing()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission(UpdatePermission);
        fixture.AddPermission("users.list");
        fixture.AddPermission("trade.form.party.view");
        var admin = fixture.AddAdministrator();
        var role = fixture.AddRoleWithPermissions("VIEWER", "users.list");
        await fixture.Database.SaveChangesAsync();

        await CallAsync(fixture, admin.Id, role.Id, ["users.list", "trade.form.party.view"]);

        await using var reader = fixture.NewContext();
        var unchanged = await reader.CompanyRoles
            .Include(item => item.RolePermissions)
                .ThenInclude(rolePermission => rolePermission.Permission)
            .SingleAsync(item => item.Id == role.Id);
        Assert.Equal(["users.list"], unchanged.RolePermissions
            .Select(rolePermission => rolePermission.Permission.Code).OrderBy(code => code).ToArray());
        Assert.Empty(await reader.SecurityAuditEvents.ToListAsync());
    }

    [Fact]
    public async Task Preview_RejectsUnknownCodesForeignRolesSystemRolesAndUnauthorizedActors()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission(UpdatePermission);
        fixture.AddPermission("users.list");
        var admin = fixture.AddAdministrator();
        var viewer = fixture.AddMember(fixture.AddRole("PLAIN-VIEWER"), "plain-viewer");
        var role = fixture.AddRoleWithPermissions("VIEWER", "users.list");
        var systemRole = fixture.AddRoleWithPermissions("company-admin", "users.list");
        systemRole.IsSystemRole = true;
        await fixture.Database.SaveChangesAsync();

        // Unknown permission code → 400 like the save path.
        var unknown = await CallAsync(fixture, admin.Id, role.Id, ["not.registered.code"]);
        Assert.Equal(StatusCodes.Status400BadRequest, Status(unknown));

        // Foreign role → 404.
        var otherCompany = new Domain.Company { Name = "شرکت دیگر", IsActive = true };
        fixture.Database.Companies.Add(otherCompany);
        var foreignRole = new Domain.CompanyRole
        {
            TenantId = otherCompany.Id,
            Company = otherCompany,
            Code = "FOREIGN",
            Name = "نقش شرکت دیگر",
            IsActive = true,
        };
        fixture.Database.CompanyRoles.Add(foreignRole);
        await fixture.Database.SaveChangesAsync();
        var foreign = await CallAsync(fixture, admin.Id, foreignRole.Id, ["users.list"]);
        Assert.Equal(StatusCodes.Status404NotFound, Status(foreign));

        // System role → 409: the save path refuses it, so it has no preview either.
        var system = await CallAsync(fixture, admin.Id, systemRole.Id, ["users.list"]);
        Assert.Equal(StatusCodes.Status409Conflict, Status(system));

        // A member without roles.update → 403.
        var forbidden = await CallAsync(fixture, viewer.Id, role.Id, ["users.list"]);
        Assert.Equal(StatusCodes.Status403Forbidden, Status(forbidden));

        // A member of another company → the same 404 as an unknown company (no disclosure).
        var outsider = fixture.AddUser("outsider");
        await fixture.Database.SaveChangesAsync();
        var crossCompany = await CallAsync(fixture, outsider.Id, role.Id, ["users.list"]);
        Assert.Equal(StatusCodes.Status404NotFound, Status(crossCompany));
    }

    private static int Status(IResult result) =>
        (result as IStatusCodeHttpResult)?.StatusCode
        ?? throw new InvalidOperationException("result did not carry a status code");

    private static Task<IResult> CallAsync(Fixture fixture, Guid actorId, Guid roleId, IReadOnlyList<string> codes) =>
        Server.Api.CompanyRoleAdministrationEndpoints.PreviewRolePermissionsAsync(
            fixture.CompanyId, roleId, codes, new DefaultHttpContext(), fixture.Principal(actorId),
            fixture.Database, fixture.CompanyAccess, fixture.CeilingService, default);

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

        public Domain.User AddAdministrator()
        {
            var role = new Domain.Role(Constants.Role.Admin) { IsActive = true };
            Database.Roles.Add(role);
            var user = AddUser($"admin-{Guid.NewGuid():N}");
            user.Role = role;
            return user;
        }

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

        public void AddPermission(string code)
        {
            Database.PermissionDefinitions.Add(new Domain.PermissionDefinition
            {
                Code = code,
                Name = code,
                Description = "test permission",
            });
        }

        public Domain.CompanyRole AddRole(string code)
        {
            var role = new Domain.CompanyRole { TenantId = CompanyId, Code = code, Name = code, IsActive = true };
            Database.CompanyRoles.Add(role);
            return role;
        }

        public Domain.CompanyRole AddRoleWithPermissions(string code, params string[] permissionCodes)
        {
            var role = new Domain.CompanyRole { TenantId = CompanyId, Code = code, Name = code, IsActive = true };
            Database.CompanyRoles.Add(role);
            foreach (var permissionCode in permissionCodes)
            {
                Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
                {
                    Role = role,
                    RoleId = role.Id,
                    PermissionId = Database.PermissionDefinitions.Local.Single(x => x.Code == permissionCode).Id,
                });
            }
            return role;
        }

        public Domain.User AddMember(Domain.CompanyRole role, string username, bool membershipActive = true)
        {
            var user = new Domain.User($"{username}-{Guid.NewGuid():N}@example.test")
            {
                Username = $"{username}-{Guid.NewGuid():N}",
                IsActive = true,
                Password = "x",
            };
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

        /// <summary>An active member of the company whose role carries exactly the given codes.</summary>
        public Domain.User AddCompanyManager(params string[] permissionCodes)
        {
            var managerRole = AddRoleWithPermissions("MANAGER", permissionCodes);
            return AddMember(managerRole, "manager");
        }

        public ClaimsPrincipal Principal(Guid id) => new(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim("AccountKind", "User"),
            ], "test"));

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}
