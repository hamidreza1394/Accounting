using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

public sealed class CompanyRoleAdministrationEndpointTests
{
    [Fact]
    public async Task CreateRole_UsesOnlyRegisteredPermissionCodesAndWritesAudit()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        fixture.AddCompany();
        fixture.AddPermission("trade.form.party.view");
        fixture.AddPermission("trade.form.party.create");
        await fixture.Database.SaveChangesAsync();

        var request = new Server.Api.CreateCompanyRoleRequest(
            "TRADE_OPERATOR", "کارشناس بازرگانی", "trade.operator", null);

        var result = await Server.Api.CompanyRoleAdministrationEndpoints.CreateRoleAsync(
            fixture.CompanyId, new DefaultHttpContext(), fixture.Principal(admin.Id), request,
            new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status201Created, (result as IStatusCodeHttpResult)?.StatusCode);
        var role = await fixture.Database.CompanyRoles.Include(x => x.RolePermissions)
            .SingleAsync(x => x.TenantId == fixture.CompanyId);
        Assert.Equal("TRADE_OPERATOR", role.Code);
        Assert.Equal(2, role.RolePermissions.Count);
        Assert.Contains(await fixture.Database.SecurityAuditEvents.ToListAsync(),
            audit => audit.EventType == "CompanyRoleCreated" && audit.TenantId == fixture.CompanyId);
    }

    [Fact]
    public async Task CreateRole_RejectsUnknownPermissionCodes()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        fixture.AddCompany();
        fixture.AddPermission("trade.form.party.view");
        await fixture.Database.SaveChangesAsync();

        var request = new Server.Api.CreateCompanyRoleRequest(
            "CUSTOM", "نقش سفارشی", null, ["accounting.document.approve"]);

        var result = await Server.Api.CompanyRoleAdministrationEndpoints.CreateRoleAsync(
            fixture.CompanyId, new DefaultHttpContext(), fixture.Principal(admin.Id), request,
            new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status400BadRequest, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.False(await fixture.Database.CompanyRoles.AnyAsync());
    }

    [Fact]
    public async Task AssignMembership_RejectsRoleFromAnotherCompany()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        var user = fixture.AddUser("employee");
        fixture.AddCompany();
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

        var result = await Server.Api.CompanyRoleAdministrationEndpoints.AssignMembershipAsync(
            fixture.CompanyId, foreignRole.Id, user.Id, new DefaultHttpContext(),
            fixture.Principal(admin.Id), new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status404NotFound, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync());
    }


    [Fact]
    public async Task AssignMembership_DoesNotCreateANewMembershipBeforeDestinationApproval()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        var user = fixture.AddUser("employee");
        fixture.AddCompany();
        var role = new Domain.CompanyRole { TenantId = fixture.CompanyId, Code = "ACCOUNTANT", Name = "حسابدار", IsActive = true };
        fixture.Database.CompanyRoles.Add(role);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyRoleAdministrationEndpoints.AssignMembershipAsync(
            fixture.CompanyId, role.Id, user.Id, new DefaultHttpContext(),
            fixture.Principal(admin.Id), new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status409Conflict, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.False(await fixture.Database.UserCompanyMemberships.AnyAsync());
    }

    [Fact]
    public async Task AssignMembership_DoesNotReactivateAnInactiveMembership()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddAdministrator();
        var user = fixture.AddUser("employee");
        fixture.AddCompany();
        var role = new Domain.CompanyRole { TenantId = fixture.CompanyId, Code = "ACCOUNTANT", Name = "حسابدار", IsActive = true };
        fixture.Database.CompanyRoles.Add(role);
        fixture.Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = user.Id, TenantId = fixture.CompanyId, Role = role, RoleId = role.Id,
            IsActive = false, CreatedAtUtc = DateTime.UtcNow,
        });
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.CompanyRoleAdministrationEndpoints.AssignMembershipAsync(
            fixture.CompanyId, role.Id, user.Id, new DefaultHttpContext(),
            fixture.Principal(admin.Id), new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status409Conflict, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.False((await fixture.Database.UserCompanyMemberships.SingleAsync()).IsActive);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        }

        public Data.DatabaseContext Database { get; }
        public Guid CompanyId { get; private set; }

        public Domain.User AddAdministrator()
        {
            var role = new Domain.Role(Constants.Role.Admin) { IsActive = true };
            Database.Roles.Add(role);
            var user = AddUser("admin");
            user.Role = role;
            return user;
        }

        public Domain.User AddUser(string username)
        {
            var user = new Domain.User($"{username}@example.test")
            {
                Username = username,
                IsActive = true,
                Password = new Infrastructure.Security.PasswordHashService().Hash("Admin-Password-Phrase-1406!"),
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
