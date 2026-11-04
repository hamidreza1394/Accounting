using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

/// <summary>
/// The secure bootstrap path (D-071): an installation administrator can decide membership
/// requests of any destination company, so the first company manager of an otherwise empty
/// company can be created without a database hand-edit. Every predicate is re-read from the
/// database, self-approval stays forbidden and system roles stay unassignable.
/// </summary>
public sealed class BootstrapMembershipApprovalTests
{
    [Fact]
    public async Task Approve_InstallationAdministrator_BootstrapsTheFirstMembershipOfAnEmptyCompany()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        var company = fixture.AddCompany();
        fixture.AddPermission("users.list");
        var role = fixture.AddRoleWithPermissions(company.Id, "MANAGER", "users.list");
        var requester = fixture.AddUser("founder");
        var request = fixture.AddPendingRequest(requester, company);
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.Approve(admin.Id, company.Id, request.Id, role.Id);

        Assert.Equal(StatusCodes.Status204NoContent, (result as IStatusCodeHttpResult)?.StatusCode);
        await using var reader = fixture.NewContext();
        var membership = await reader.UserCompanyMemberships.SingleAsync();
        Assert.True(membership.IsActive);
        Assert.Equal(role.Id, membership.RoleId);
        Assert.Equal(company.Id, membership.TenantId);
        var decided = await reader.CompanyMembershipRequests.SingleAsync(item => item.Id == request.Id);
        Assert.Equal(Domain.CompanyMembershipRequestStatuses.Approved, decided.Status);
        Assert.Equal(admin.Id, decided.DecidedByUserId);
        Assert.Contains(await reader.SecurityAuditEvents.ToListAsync(),
            audit => audit.EventType == "CompanyMembershipApproved" && audit.TenantId == company.Id);
    }

    [Fact]
    public async Task Approve_InstallationAdministrator_CannotApproveTheirOwnRequest()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        var company = fixture.AddCompany();
        fixture.AddPermission("users.list");
        var role = fixture.AddRoleWithPermissions(company.Id, "MANAGER", "users.list");
        var request = fixture.AddPendingRequest(admin, company);
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.Approve(admin.Id, company.Id, request.Id, role.Id);

        Assert.Equal(StatusCodes.Status403Forbidden, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(Domain.CompanyMembershipRequestStatuses.Pending,
            (await fixture.Database.CompanyMembershipRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task Approve_InstallationAdministrator_HasNoDelegationCeilingButSystemRolesStayRefused()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        var company = fixture.AddCompany();
        fixture.AddPermission("corporate-groups.manage");
        var systemRole = fixture.AddRoleWithPermissions(company.Id, "company-admin", "corporate-groups.manage");
        systemRole.IsSystemRole = true;
        var normalRole = fixture.AddRoleWithPermissions(company.Id, "MANAGER", "corporate-groups.manage");
        var requester = fixture.AddUser("founder");
        var request = fixture.AddPendingRequest(requester, company);
        await fixture.Database.SaveChangesAsync();

        // A system role is refused for every actor kind, including the installation admin.
        var system = await fixture.Approve(admin.Id, company.Id, request.Id, systemRole.Id);
        Assert.Equal(StatusCodes.Status403Forbidden, (system as IStatusCodeHttpResult)?.StatusCode);

        // The admin holds no membership at all — a company approver with a ceiling would be
        // refused; the installation administrator may delegate any non-system role.
        var normal = await fixture.Approve(admin.Id, company.Id, request.Id, normalRole.Id);
        Assert.Equal(StatusCodes.Status204NoContent, (normal as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(Domain.CompanyMembershipRequestStatuses.Approved,
            (await fixture.Database.CompanyMembershipRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task Approve_WithoutAnyDestinationRelationship_Returns404()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        var company = fixture.AddCompany();
        fixture.AddPermission("users.list");
        var role = fixture.AddRoleWithPermissions(company.Id, "MANAGER", "users.list");
        var stranger = fixture.AddUser("stranger");
        var requester = fixture.AddUser("founder");
        var request = fixture.AddPendingRequest(requester, company);
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.Approve(stranger.Id, company.Id, request.Id, role.Id);

        Assert.Equal(StatusCodes.Status404NotFound, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync());
    }

    [Fact]
    public async Task PendingQueue_InstallationAdministrator_SeesThePendingRequestsOfAnyCompany()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        var company = fixture.AddCompany();
        var requester = fixture.AddUser("founder");
        var request = fixture.AddPendingRequest(requester, company);
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.GetPendingRequests(admin.Id, company.Id);
        var ok = Assert.IsType<Ok<Server.Api.CompanyMembershipPendingListResponse>>(result);
        Assert.Equal(1, ok.Value!.TotalCount);
        Assert.Equal(requester.Id, ok.Value.Items.Single().UserId);
    }

    [Fact]
    public async Task AssignableRoles_InstallationAdministrator_SeesEveryActiveNonSystemRoleForBootstrap()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        var company = fixture.AddCompany();
        fixture.AddPermission("users.list");
        fixture.AddPermission("users.membership.approve");
        var managerRole = fixture.AddRoleWithPermissions(company.Id, "MANAGER", "users.membership.approve", "users.list");
        var plainRole = fixture.AddRoleWithPermissions(company.Id, "VIEWER", "users.list");
        await fixture.Database.SaveChangesAsync();

        // The bootstrap needs a role that carries the approval permission itself; a company
        // approver must never see such a role, the installation administrator must.
        var result = await fixture.GetAssignableRoles(admin.Id, company.Id);
        var ok = Assert.IsType<Ok<Server.Api.AssignableCompanyRoleListResponse>>(result);
        var codes = ok.Value!.Items.Select(item => item.Code).Order().ToArray();
        Assert.Equal(["MANAGER", "VIEWER"], codes);
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

        public Infrastructure.Tenancy.CompanyAccessService CompanyAccess => new(Database);

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

        public Domain.User AddAdministrator()
        {
            var role = new Domain.Role(Constants.Role.Admin) { IsActive = true };
            Database.Roles.Add(role);
            var user = AddUser("admin");
            user.Role = role;
            return user;
        }

        public Domain.Company AddCompany()
        {
            var company = new Domain.Company { Name = "شرکت نوپا", IsActive = true };
            Database.Companies.Add(company);
            return company;
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

        public Domain.CompanyRole AddRoleWithPermissions(Guid tenantId, string code, params string[] permissionCodes)
        {
            var role = new Domain.CompanyRole
            {
                TenantId = tenantId,
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

        public Domain.CompanyMembershipRequest AddPendingRequest(Domain.User requester, Domain.Company destination)
        {
            var request = new Domain.CompanyMembershipRequest
            {
                UserId = requester.Id,
                DestinationTenantId = destination.Id,
                Status = Domain.CompanyMembershipRequestStatuses.Pending,
                RequestedAtUtc = DateTime.UtcNow,
            };
            Database.CompanyMembershipRequests.Add(request);
            return request;
        }

        public Task<IResult> Approve(Guid approverId, Guid companyId, Guid requestId, Guid roleId) =>
            Server.Api.CompanyMembershipRequestEndpoints.ApproveAsync(
                companyId, requestId, new DefaultHttpContext(), Principal(approverId),
                new Server.Api.ApproveCompanyMembershipRequest(roleId),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);

        public Task<IResult> GetPendingRequests(Guid approverId, Guid companyId) =>
            Server.Api.CompanyMembershipRequestEndpoints.GetPendingRequestsAsync(
                companyId, new DefaultHttpContext(), Principal(approverId), Database, CompanyAccess,
                cancellationToken: default);

        public Task<IResult> GetAssignableRoles(Guid approverId, Guid companyId) =>
            Server.Api.CompanyMembershipRequestEndpoints.GetAssignableRolesAsync(
                companyId, new DefaultHttpContext(), Principal(approverId), Database, CompanyAccess,
                cancellationToken: default);

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
