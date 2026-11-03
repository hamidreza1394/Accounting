using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

public sealed class CompanyUserDirectoryEndpointTests
{
    [Fact]
    public async Task Directory_IsLimitedToCompanyMembershipsAndExcludesPendingRequests()
    {
        await using var fixture = new Fixture();
        var companyA = new Domain.Company { Name = "شرکت الف", IsActive = true };
        var companyB = new Domain.Company { Name = "شرکت ب", IsActive = true };
        var permission = new Domain.PermissionDefinition { Code = "users.list", Name = "مشاهده کاربران شرکت" };
        var managerRole = NewRole(companyA, "MANAGER", "مدیر");
        var memberRoleA = NewRole(companyA, "ACCOUNTANT", "حسابدار");
        var memberRoleB = NewRole(companyB, "ACCOUNTANT", "حسابدار شرکت ب");

        var manager = NewUser("manager", active: true, firstName: "مدیر", lastName: "شرکت");
        var alice = NewUser("alice", active: true, firstName: "علی", lastName: "الف");
        var inactive = NewUser("inactive", active: false, firstName: "کاربر", lastName: "غیرفعال");
        var bob = NewUser("bob", active: true, firstName: "باب", lastName: "شرکت ب");
        var pending = NewUser("pending", active: true, firstName: "در انتظار", lastName: "عضویت");

        fixture.Database.AddRange(companyA, companyB, permission, managerRole, memberRoleA, memberRoleB,
            manager, alice, inactive, bob, pending);
        fixture.Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
        {
            Role = managerRole,
            RoleId = managerRole.Id,
            Permission = permission,
            PermissionId = permission.Id,
        });
        fixture.Database.UserCompanyMemberships.AddRange(
            NewMembership(manager, companyA, managerRole, active: true),
            NewMembership(alice, companyA, memberRoleA, active: true),
            NewMembership(inactive, companyA, memberRoleA, active: false),
            NewMembership(bob, companyB, memberRoleB, active: true));
        fixture.Database.CompanyMembershipRequests.Add(new Domain.CompanyMembershipRequest
        {
            UserId = pending.Id,
            DestinationTenantId = companyA.Id,
            DestinationCompany = companyA,
            Status = Domain.CompanyMembershipRequestStatuses.Pending,
            RequestedAtUtc = DateTime.UtcNow,
        });
        await fixture.Database.SaveChangesAsync();

        var responseContext = new DefaultHttpContext();
        var result = await Server.Api.CompanyUserDirectoryEndpoints.GetUsersAsync(
            companyA.Id,
            responseContext,
            Principal(manager.Id),
            fixture.Database,
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database),
            page: 1,
            pageSize: 20);

        var ok = Assert.IsType<Ok<Server.Api.CompanyUserDirectoryResponse>>(result);
        Assert.Equal(companyA.Id, ok.Value!.CompanyId);
        Assert.Equal("شرکت الف", ok.Value.CompanyName);
        Assert.Equal(3, ok.Value.TotalCount);
        Assert.Equal("no-store", responseContext.Response.Headers.CacheControl.ToString());
        Assert.Equal(
            new[] { manager.Id, alice.Id, inactive.Id }.Order(),
            ok.Value.Items.Select(item => item.UserId).Order());
        Assert.DoesNotContain(ok.Value.Items, item => item.UserId == bob.Id);
        Assert.DoesNotContain(ok.Value.Items, item => item.UserId == pending.Id);

        var inactiveItem = Assert.Single(ok.Value.Items, item => item.UserId == inactive.Id);
        Assert.False(inactiveItem.UserIsActive);
        Assert.False(inactiveItem.MembershipIsActive);
        Assert.Equal("حسابدار", inactiveItem.RoleName);

        var filtered = await Server.Api.CompanyUserDirectoryEndpoints.GetUsersAsync(
            companyA.Id,
            new DefaultHttpContext(),
            Principal(manager.Id),
            fixture.Database,
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database),
            search: "alice",
            membershipActive: true,
            page: 1,
            pageSize: 1);
        var filteredOk = Assert.IsType<Ok<Server.Api.CompanyUserDirectoryResponse>>(filtered);
        Assert.Equal(1, filteredOk.Value!.TotalCount);
        Assert.Equal(alice.Id, Assert.Single(filteredOk.Value.Items).UserId);

        var invalidSearch = await Server.Api.CompanyUserDirectoryEndpoints.GetUsersAsync(
            companyA.Id, new DefaultHttpContext(), Principal(manager.Id), fixture.Database,
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database), search: new string('x', 121));
        Assert.Equal(StatusCodes.Status400BadRequest, (invalidSearch as IStatusCodeHttpResult)?.StatusCode);

        var inactivePage = await Server.Api.CompanyUserDirectoryEndpoints.GetUsersAsync(
            companyA.Id,
            new DefaultHttpContext(),
            Principal(manager.Id),
            fixture.Database,
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database),
            membershipActive: false);
        var inactiveOk = Assert.IsType<Ok<Server.Api.CompanyUserDirectoryResponse>>(inactivePage);
        Assert.Equal(inactive.Id, Assert.Single(inactiveOk.Value!.Items).UserId);

        var properties = typeof(Server.Api.CompanyUserDirectoryItem).GetProperties()
            .Select(property => property.Name).ToArray();
        Assert.DoesNotContain(nameof(Domain.User.EmailAddress), properties);
        Assert.DoesNotContain(nameof(Domain.User.CellPhoneNumber), properties);
        Assert.DoesNotContain(nameof(Domain.User.Password), properties);
        Assert.DoesNotContain("PasswordHash", properties);
        Assert.DoesNotContain("PermissionCodes", properties);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.DoesNotContain("@example.test", json);
        Assert.DoesNotContain("private-password", json);
    }

    [Fact]
    public async Task Directory_RequiresUsersListAndHidesForeignCompanies()
    {
        await using var fixture = new Fixture();
        var companyA = new Domain.Company { Name = "شرکت الف", IsActive = true };
        var companyB = new Domain.Company { Name = "شرکت ب", IsActive = true };
        var noPermissionRole = NewRole(companyA, "READER", "خواننده");
        var manager = NewUser("reader", active: true);
        fixture.Database.AddRange(companyA, companyB, noPermissionRole, manager);
        fixture.Database.UserCompanyMemberships.Add(NewMembership(manager, companyA, noPermissionRole, active: true));
        await fixture.Database.SaveChangesAsync();

        var access = new Infrastructure.Tenancy.CompanyAccessService(fixture.Database);
        var denied = await Server.Api.CompanyUserDirectoryEndpoints.GetUsersAsync(
            companyA.Id, new DefaultHttpContext(), Principal(manager.Id), fixture.Database, access);
        Assert.Equal(StatusCodes.Status403Forbidden, (denied as IStatusCodeHttpResult)?.StatusCode);

        var foreign = await Server.Api.CompanyUserDirectoryEndpoints.GetUsersAsync(
            companyB.Id, new DefaultHttpContext(), Principal(manager.Id), fixture.Database, access);
        Assert.Equal(StatusCodes.Status404NotFound, (foreign as IStatusCodeHttpResult)?.StatusCode);

    }

    [Fact]
    public async Task Directory_RejectsAnonymousAndInfrastructurePrincipals()
    {
        await using var fixture = new Fixture();
        var result = await Server.Api.CompanyUserDirectoryEndpoints.GetUsersAsync(
            Guid.NewGuid(), new DefaultHttpContext(), new ClaimsPrincipal(), fixture.Database,
            new Infrastructure.Tenancy.CompanyAccessService(fixture.Database));
        Assert.Equal(StatusCodes.Status401Unauthorized, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    private static Domain.CompanyRole NewRole(Domain.Company company, string code, string name) => new()
    {
        Company = company,
        TenantId = company.Id,
        Code = code,
        Name = name,
        IsActive = true,
    };

    private static Domain.User NewUser(string username, bool active, string? firstName = null, string? lastName = null) => new($"{username}@example.test")
    {
        Username = username,
        FirstName = firstName,
        LastName = lastName,
        IsActive = active,
        Password = "private-password",
    };

    private static Domain.UserCompanyMembership NewMembership(
        Domain.User user, Domain.Company company, Domain.CompanyRole role, bool active) => new()
    {
        UserId = user.Id,
        TenantId = company.Id,
        Company = company,
        RoleId = role.Id,
        Role = role,
        IsActive = active,
        CreatedAtUtc = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc),
    };

    private static ClaimsPrincipal Principal(Guid userId) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim("AccountKind", "User"),
    ], "test"));

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        }

        public Data.DatabaseContext Database { get; }

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}
