using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Template.Tests;

public sealed class MePermissionsEndpointTests
{
    [Fact]
    public async Task ReturnsOnlyPermissionsForTheValidatedCompanyInTheCurrentSession()
    {
        await using var fixture = new Fixture();
        var user = fixture.AddUser("member");
        var companyA = fixture.AddCompany("شرکت الف");
        var companyB = fixture.AddCompany("شرکت ب");
        var roleA = fixture.AddCompanyRole(companyA, "ACCOUNTANT");
        var roleB = fixture.AddCompanyRole(companyB, "TRADE");
        fixture.AddPermission(roleA, "users.list");
        fixture.AddPermission(roleB, "trade.import");
        fixture.AddMembership(user, companyA, roleA);
        fixture.AddMembership(user, companyB, roleB);
        var periodA = fixture.AddFiscalPeriod(companyA);
        await fixture.Database.SaveChangesAsync();

        var sessionId = Guid.NewGuid();
        var httpContext = fixture.CreateContextWithSelection(user.Id, sessionId, companyA.Id, periodA.Id);
        // A forged query parameter is deliberately ignored: this endpoint has no companyId input.
        httpContext.Request.QueryString = new QueryString($"?companyId={companyB.Id}");

        var result = await Server.Api.MePermissionsEndpoints.GetPermissionsAsync(
            httpContext,
            Principal(user.Id, sessionId),
            fixture.Database,
            fixture.CompanyAccess,
            fixture.WorkContext);

        var response = Assert.IsType<Ok<Server.Api.MePermissionsResponse>>(result).Value!;
        Assert.False(response.IsInstallationAdministrator);
        Assert.Equal(companyA.Id, response.Company!.Id);
        Assert.Equal(new[] { "users.list" }, response.Company.PermissionCodes);
        Assert.DoesNotContain("trade.import", response.Company.PermissionCodes);
        Assert.Equal("no-store", httpContext.Response.Headers.CacheControl.ToString());
        Assert.Equal("no-cache", httpContext.Response.Headers.Pragma.ToString());
    }

    [Fact]
    public async Task ReturnsNullCompanyWhenThereIsNoSelectedOrAccessibleCompany()
    {
        await using var fixture = new Fixture();
        var user = fixture.AddUser("without-company");
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.MePermissionsEndpoints.GetPermissionsAsync(
            fixture.CreateHttpContext(),
            Principal(user.Id, Guid.NewGuid()),
            fixture.Database,
            fixture.CompanyAccess,
            fixture.WorkContext);

        var response = Assert.IsType<Ok<Server.Api.MePermissionsResponse>>(result).Value!;
        Assert.False(response.IsInstallationAdministrator);
        Assert.Null(response.Company);
    }

    [Theory]
    [InlineData("membership")]
    [InlineData("role")]
    public async Task InvalidatedMembershipOrRoleImmediatelyRemovesCompanyPermissions(string invalidation)
    {
        await using var fixture = new Fixture();
        var user = fixture.AddUser("member");
        var company = fixture.AddCompany("شرکت الف");
        var role = fixture.AddCompanyRole(company, "ACCOUNTANT");
        fixture.AddPermission(role, "users.list");
        var membership = fixture.AddMembership(user, company, role);
        var period = fixture.AddFiscalPeriod(company);
        await fixture.Database.SaveChangesAsync();

        var sessionId = Guid.NewGuid();
        var httpContext = fixture.CreateContextWithSelection(user.Id, sessionId, company.Id, period.Id);
        if (invalidation == "membership")
            membership.IsActive = false;
        else
            role.IsActive = false;
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.MePermissionsEndpoints.GetPermissionsAsync(
            httpContext,
            Principal(user.Id, sessionId),
            fixture.Database,
            fixture.CompanyAccess,
            fixture.WorkContext);

        var response = Assert.IsType<Ok<Server.Api.MePermissionsResponse>>(result).Value!;
        Assert.Null(response.Company);
    }

    [Fact]
    public async Task InstallationAdministratorFlagComesFromTheActiveDatabaseRole()
    {
        await using var fixture = new Fixture();
        var adminRole = new Domain.Role(Constants.Role.Admin) { IsActive = true };
        var user = fixture.AddUser("installer", role: adminRole);
        await fixture.Database.SaveChangesAsync();

        var activeResult = await Server.Api.MePermissionsEndpoints.GetPermissionsAsync(
            fixture.CreateHttpContext(),
            Principal(user.Id, Guid.NewGuid()),
            fixture.Database,
            fixture.CompanyAccess,
            fixture.WorkContext);
        Assert.True(Assert.IsType<Ok<Server.Api.MePermissionsResponse>>(activeResult).Value!.IsInstallationAdministrator);

        adminRole.IsActive = false;
        await fixture.Database.SaveChangesAsync();
        var inactiveResult = await Server.Api.MePermissionsEndpoints.GetPermissionsAsync(
            fixture.CreateHttpContext(),
            Principal(user.Id, Guid.NewGuid()),
            fixture.Database,
            fixture.CompanyAccess,
            fixture.WorkContext);
        Assert.False(Assert.IsType<Ok<Server.Api.MePermissionsResponse>>(inactiveResult).Value!.IsInstallationAdministrator);
    }

    [Fact]
    public async Task RejectsPrincipalWithoutAValidCompanyUserIdentity()
    {
        await using var fixture = new Fixture();
        var result = await Server.Api.MePermissionsEndpoints.GetPermissionsAsync(
            fixture.CreateHttpContext(),
            new ClaimsPrincipal(new ClaimsIdentity()),
            fixture.Database,
            fixture.CompanyAccess,
            fixture.WorkContext);

        Assert.Equal(StatusCodes.Status401Unauthorized, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    private static ClaimsPrincipal Principal(Guid userId, Guid sessionId) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim(Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
            Infrastructure.Tenancy.CompanyAccessService.UserAccountKind),
        new Claim(Infrastructure.Tenancy.WorkContextCookie.SessionIdClaimType, sessionId.ToString()),
    ], "test"));

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        private readonly Infrastructure.Tenancy.WorkContextCookie _cookie;

        public Fixture()
        {
            Database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
            CompanyAccess = new Infrastructure.Tenancy.CompanyAccessService(Database);
            _services = new ServiceCollection()
                .AddSingleton<IHostEnvironment>(new TestHostEnvironment())
                .BuildServiceProvider();
            _cookie = new Infrastructure.Tenancy.WorkContextCookie(
                new EphemeralDataProtectionProvider(), TimeProvider.System);
            WorkContext = new Infrastructure.Tenancy.WorkContextService(
                Database, CompanyAccess, _cookie, NullLogger<Infrastructure.Tenancy.WorkContextService>.Instance);
        }

        public Data.DatabaseContext Database { get; }
        public Infrastructure.Tenancy.CompanyAccessService CompanyAccess { get; }
        public Infrastructure.Tenancy.WorkContextService WorkContext { get; }

        public Domain.User AddUser(string username, Domain.Role? role = null)
        {
            var user = new Domain.User($"{username}@example.test")
            {
                Username = username,
                IsActive = true,
                Role = role,
            };
            Database.Users.Add(user);
            return user;
        }

        public Domain.Company AddCompany(string name)
        {
            var company = new Domain.Company { Name = name, IsActive = true };
            Database.Companies.Add(company);
            return company;
        }

        public Domain.CompanyRole AddCompanyRole(Domain.Company company, string code)
        {
            var role = new Domain.CompanyRole
            {
                TenantId = company.Id,
                Company = company,
                Code = code,
                Name = code,
                IsActive = true,
            };
            Database.CompanyRoles.Add(role);
            return role;
        }

        public void AddPermission(Domain.CompanyRole role, string code)
        {
            var permission = new Domain.PermissionDefinition { Code = code, Name = code };
            Database.PermissionDefinitions.Add(permission);
            Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
            {
                Role = role,
                RoleId = role.Id,
                Permission = permission,
                PermissionId = permission.Id,
            });
        }

        public Domain.UserCompanyMembership AddMembership(
            Domain.User user, Domain.Company company, Domain.CompanyRole role)
        {
            var membership = new Domain.UserCompanyMembership
            {
                UserId = user.Id,
                TenantId = company.Id,
                Company = company,
                RoleId = role.Id,
                Role = role,
                IsActive = true,
            };
            Database.UserCompanyMemberships.Add(membership);
            return membership;
        }

        public Domain.FiscalPeriod AddFiscalPeriod(Domain.Company company)
        {
            var period = new Domain.FiscalPeriod
            {
                TenantId = company.Id,
                Company = company,
                Title = "سال مالی آزمون",
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31),
                Status = Domain.FiscalPeriodStatus.Open,
            };
            Database.FiscalPeriods.Add(period);
            return period;
        }

        public DefaultHttpContext CreateHttpContext()
        {
            return new DefaultHttpContext { RequestServices = _services };
        }

        public DefaultHttpContext CreateContextWithSelection(
            Guid userId, Guid sessionId, Guid companyId, Guid fiscalPeriodId)
        {
            var issuingContext = CreateHttpContext();
            _cookie.Write(issuingContext, userId, sessionId, companyId, fiscalPeriodId);
            var cookiePair = issuingContext.Response.Headers.SetCookie.ToString().Split(';')[0];
            var requestContext = CreateHttpContext();
            requestContext.Request.Headers.Cookie = cookiePair;
            return requestContext;
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await _services.DisposeAsync();
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Template.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
