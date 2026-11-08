using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

/// <summary>
/// تست‌های «دسترسی‌های مؤثر» (Stage 2-E بخش Backend، Issue #444):
/// مشاهدهٔ اجتماع کدهای مجوز نقش‌های فعال یک عضو شرکت؛ فقط با roles.view همان شرکت
/// یا مدیر نصب (D-071 دوحالته)؛ برای بدون‌عضو 404 بدون افشای وجود حساب (D-064).
/// </summary>
public sealed class EffectivePermissionsEndpointTests
{
    [Fact]
    public async Task EffectivePermissions_FollowsTheDualModeCompanyScopeRules()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission("users.list");
        var viewerRole = fixture.AddRole("VIEWER");
        fixture.Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
        {
            Role = viewerRole,
            RoleId = viewerRole.Id,
            PermissionId = fixture.Database.PermissionDefinitions.Local.Single(x => x.Code == "users.list").Id,
        });
        var admin = fixture.AddAdministrator();
        var viewer = fixture.AddMember(viewerRole, "viewer");
        var outsider = fixture.AddUser("outsider");
        var target = fixture.AddMember(viewerRole, "target");
        await fixture.Database.SaveChangesAsync();

        // کاربر بی‌نسبت به شرکت: 404 بدون افشا
        var outsiderResult = await CallAsync(fixture, outsider.Id, target.Id);
        Assert.Equal(StatusCodes.Status404NotFound, Status(outsiderResult));

        // عضو بدون roles.view: 403 صریح
        var memberResult = await CallAsync(fixture, viewer.Id, target.Id);
        Assert.Equal(StatusCodes.Status403Forbidden, Status(memberResult));

        // مدیر نصب: 200 (بازخوانی نقش از DB)
        var adminResult = await CallAsync(fixture, admin.Id, target.Id);
        Assert.Equal(StatusCodes.Status200OK, Status(adminResult));
    }

    [Fact]
    public async Task EffectivePermissions_ReturnsTheUnionOfActiveRoleCodes()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission("users.list");
        fixture.AddPermission("roles.view");
        fixture.AddPermission("hr.employees.list");

        var viewerRole = fixture.AddRole("VIEWER");
        var hrRole = fixture.AddRole("HR-OPERATOR");
        var inactiveRole = fixture.AddRole("OLD-ROLE", active: false);
        foreach (var (role, code) in new[]
                 {
                     (viewerRole, "users.list"),
                     (hrRole, "hr.employees.list"),
                     (inactiveRole, "roles.view"),
                 })
        {
            fixture.Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
            {
                Role = role,
                RoleId = role.Id,
                PermissionId = fixture.Database.PermissionDefinitions.Local.Single(x => x.Code == code).Id,
            });
        }

        var target = fixture.AddUser("target");
        fixture.AddMembership(target, viewerRole);
        fixture.AddMembership(target, hrRole);
        // عضویت سوم غیرفعال با همان نقش قدیمی — نباید در نتیجه باشد
        fixture.AddMembership(target, inactiveRole, membershipActive: false);
        await fixture.Database.SaveChangesAsync();

        var admin = fixture.AddAdministrator();
        await fixture.Database.SaveChangesAsync();

        var result = await CallAsync(fixture, admin.Id, target.Id);
        Assert.Equal(StatusCodes.Status200OK, Status(result));
        var ok = Assert.IsType<Ok<Server.Api.EffectivePermissionsResponse>>(result);
        Assert.Equivalent(new[] { "HR-OPERATOR", "VIEWER" }, ok.Value!.RoleNames);
        Assert.Equal(new[] { "hr.employees.list", "users.list" }, ok.Value.PermissionCodes);
    }

    [Fact]
    public async Task EffectivePermissions_AttributesEveryCodeToItsGrantingActiveRoles()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission("users.list");
        fixture.AddPermission("hr.employees.list");

        // Both roles grant users.list: its source must list both role names.
        var accountant = fixture.AddRole("حسابدار");
        var auditor = fixture.AddRole("رسیدگی‌کننده");
        var hrRole = fixture.AddRole("HR-OPERATOR");
        var inactiveRole = fixture.AddRole("OLD-ROLE", active: false);
        foreach (var (role, code) in new[]
                 {
                     (accountant, "users.list"),
                     (auditor, "users.list"),
                     (hrRole, "hr.employees.list"),
                     // The inactive role also carries users.list, but must never be a source.
                     (inactiveRole, "users.list"),
                 })
        {
            fixture.Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
            {
                Role = role,
                RoleId = role.Id,
                PermissionId = fixture.Database.PermissionDefinitions.Local.Single(x => x.Code == code).Id,
            });
        }

        var target = fixture.AddUser("target");
        fixture.AddMembership(target, accountant);
        fixture.AddMembership(target, auditor);
        fixture.AddMembership(target, hrRole);
        fixture.AddMembership(target, inactiveRole);
        await fixture.Database.SaveChangesAsync();

        var admin = fixture.AddAdministrator();
        await fixture.Database.SaveChangesAsync();

        var result = await CallAsync(fixture, admin.Id, target.Id);
        Assert.Equal(StatusCodes.Status200OK, Status(result));
        var ok = Assert.IsType<Ok<Server.Api.EffectivePermissionsResponse>>(result).Value!;

        // The flat union stays identical to CompanyAccessService by construction.
        Assert.Equal(new[] { "hr.employees.list", "users.list" }, ok.PermissionCodes);

        var userGrant = Assert.Single(ok.Permissions, permission => permission.Code == "users.list");
        // Source role names are ordered with the deterministic ordinal comparer (same as the codes).
        Assert.Equal(new[] { "حسابدار", "رسیدگی‌کننده" }, userGrant.SourceRoleNames);
        var hrGrant = Assert.Single(ok.Permissions, permission => permission.Code == "hr.employees.list");
        Assert.Equal(new[] { "HR-OPERATOR" }, hrGrant.SourceRoleNames);
        // Every attributed code is part of the flat union and vice versa.
        Assert.Equal(
            (IEnumerable<string>)ok.PermissionCodes,
            ok.Permissions.Select(permission => permission.Code).OrderBy(code => code, StringComparer.Ordinal));
    }

    [Fact]
    public async Task EffectivePermissions_Returns404WithoutDisclosingAccounts()
    {
        await using var fixture = new Fixture();
        fixture.AddCompany();
        fixture.AddPermission("users.list");
        fixture.AddPermission("roles.view");
        var viewerRole = fixture.AddRole("VIEWER");
        foreach (var code in new[] { "users.list", "roles.view" })
        {
            fixture.Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
            {
                Role = viewerRole,
                RoleId = viewerRole.Id,
                PermissionId = fixture.Database.PermissionDefinitions.Local.Single(x => x.Code == code).Id,
            });
        }

        var viewer = fixture.AddMember(viewerRole, "viewer");
        var noMembershipUser = fixture.AddUser("nomembership");
        var inactiveMember = fixture.AddUser("inactive");
        fixture.AddMembership(inactiveMember, viewerRole, membershipActive: false);
        await fixture.Database.SaveChangesAsync();

        var noMembership = await CallAsync(fixture, viewer.Id, noMembershipUser.Id);
        Assert.Equal(StatusCodes.Status404NotFound, Status(noMembership));

        var inactive = await CallAsync(fixture, viewer.Id, inactiveMember.Id);
        Assert.Equal(StatusCodes.Status404NotFound, Status(inactive));

        // حساب کاملاً ناموجود هم همان 404 است
        var missing = await CallAsync(fixture, viewer.Id, Guid.NewGuid());
        Assert.Equal(StatusCodes.Status404NotFound, Status(missing));
    }

    private static int Status(IResult result) =>
        (result as IStatusCodeHttpResult)?.StatusCode
        ?? throw new InvalidOperationException("result did not carry a status code");

    private static Task<IResult> CallAsync(Fixture fixture, Guid actorId, Guid targetUserId) =>
        Server.Api.CompanyRoleAdministrationEndpoints.GetEffectivePermissionsAsync(
            fixture.CompanyId, targetUserId, new DefaultHttpContext(), fixture.Principal(actorId),
            fixture.Database, fixture.CompanyAccess, default);

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        }

        public Data.DatabaseContext Database { get; }
        public Guid CompanyId { get; private set; }
        public Guid AdminId { get; private set; }

        public Infrastructure.Tenancy.CompanyAccessService CompanyAccess => new(Database);

        public Domain.User AddAdministrator()
        {
            var role = new Domain.Role(Constants.Role.Admin) { IsActive = true };
            Database.Roles.Add(role);
            var user = new Domain.User($"admin-{Guid.NewGuid():N}@example.test")
            {
                Username = $"admin-{Guid.NewGuid():N}",
                FirstName = "مدیر",
                LastName = "نصب",
                IsActive = true,
                Role = role,
                Password = "not-a-real-hash",
            };
            Database.Users.Add(user);
            AdminId = user.Id;
            return user;
        }

        public Domain.User AddUser(string username)
        {
            var user = new Domain.User($"{username}@example.test")
            {
                Username = username,
                FirstName = username,
                LastName = "کاربر",
                IsActive = true,
                Password = "not-a-real-hash",
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

        public Domain.CompanyRole AddRole(string code, bool active = true)
        {
            var role = new Domain.CompanyRole { TenantId = CompanyId, Code = code, Name = code, IsActive = active };
            Database.CompanyRoles.Add(role);
            return role;
        }

        public void AddMembership(Domain.User user, Domain.CompanyRole role, bool membershipActive = true)
        {
            Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
            {
                UserId = user.Id,
                TenantId = role.TenantId,
                Role = role,
                RoleId = role.Id,
                IsActive = membershipActive,
                CreatedAtUtc = DateTime.UtcNow,
            });
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

        public ClaimsPrincipal Principal(Guid id) => new(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim("AccountKind", "User"),
            ], "test"));

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}
