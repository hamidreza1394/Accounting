using System.Security.Claims;
using Faranou.Modules.Trade.Application.Authorization;
using Infrastructure.Tenancy;
using Infrastructure.Trade;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Template.Tests;

public sealed class TradeAuthorizationAdapterTests : IDisposable
{
    private readonly Data.DatabaseContext database;
    private readonly Guid userId;
    private readonly WorkContextCookie cookie;
    private readonly WorkContextService workContextService;
    private readonly CompanyAccessService companyAccessService;
    private readonly HttpContextAccessor httpContextAccessor = new();

    public TradeAuthorizationAdapterTests()
    {
        database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var user = new Domain.User(emailAddress: "trade-user@example.test")
        {
            Username = "trade-user",
            IsActive = true,
        };
        database.Users.Add(user);
        database.SaveChanges();
        userId = user.Id;

        cookie = new WorkContextCookie(new EphemeralDataProtectionProvider(), TimeProvider.System);
        companyAccessService = new CompanyAccessService(database);
        workContextService = new WorkContextService(
            database,
            companyAccessService,
            cookie,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkContextService>.Instance);
    }

    [Fact]
    public async Task OpenPeriodAllowsWriteWhenCompanyRoleHasTradePermission()
    {
        var permission = TradePermissionCodes.FormUpdate("import-order");
        var company = AddCompany(Domain.FiscalPeriodStatus.Open, [permission]);
        var adapter = CreateAdapter(company.CompanyId, company.PeriodId);

        Assert.True(await HasAsync(adapter, userId, permission, TradeAccessIntent.Write));
    }

    [Fact]
    public async Task ClosedPeriodAllowsReadButDeniesWriteEvenWhenPermissionIsGranted()
    {
        var viewPermission = TradePermissionCodes.FormView("import-order");
        var writePermission = TradePermissionCodes.FormUpdate("import-order");
        var company = AddCompany(Domain.FiscalPeriodStatus.Closed, [viewPermission, writePermission]);
        var adapter = CreateAdapter(company.CompanyId, company.PeriodId);

        Assert.True(await HasAsync(adapter, userId, viewPermission, TradeAccessIntent.Read));
        Assert.False(await HasAsync(adapter, userId, writePermission, TradeAccessIntent.Write));
    }

    [Fact]
    public async Task ClosedPeriodWritePermissionCannotBeDowngradedToReadIntent()
    {
        var writePermission = TradePermissionCodes.FormUpdate("import-order");
        var company = AddCompany(Domain.FiscalPeriodStatus.Closed, [writePermission]);
        var adapter = CreateAdapter(company.CompanyId, company.PeriodId);

        Assert.False(await HasAsync(adapter, userId, writePermission, TradeAccessIntent.Read));
    }

    [Fact]
    public async Task ReadPermissionCannotAuthorizeAWriteIntent()
    {
        var viewPermission = TradePermissionCodes.FormView("import-order");
        var company = AddCompany(Domain.FiscalPeriodStatus.Open, [viewPermission]);
        var adapter = CreateAdapter(company.CompanyId, company.PeriodId);

        Assert.False(await HasAsync(adapter, userId, viewPermission, TradeAccessIntent.Write));
    }

    [Fact]
    public async Task PeriodFromAnotherCompanyIsRejected()
    {
        var permission = TradePermissionCodes.FormView("import-order");
        var companyA = AddCompany(Domain.FiscalPeriodStatus.Open, [permission]);
        var companyB = AddCompany(Domain.FiscalPeriodStatus.Open, [permission]);
        var adapter = CreateAdapter(companyA.CompanyId, companyB.PeriodId);

        Assert.False(await HasAsync(adapter, userId, permission, TradeAccessIntent.Read));
    }

    [Fact]
    public async Task MultiCompanyPermissionRequiresEveryCompanyPeriodPair()
    {
        var permission = TradePermissionCodes.MultiCompanyReportView;
        var companyA = AddCompany(Domain.FiscalPeriodStatus.Open, [permission]);
        var companyB = AddCompany(Domain.FiscalPeriodStatus.Open, []);
        var adapter = CreateAdapter(companyA.CompanyId, companyA.PeriodId);

        var scopes = new[]
        {
            new TradePeriodScope(companyA.CompanyId, companyA.PeriodId),
            new TradePeriodScope(companyB.CompanyId, companyB.PeriodId),
        };

        Assert.False(await adapter.HasPermissionForAllCompanyPeriodsAsync(
            userId,
            scopes,
            permission,
            TradeAccessIntent.Read,
            TestContext.Current.CancellationToken));

        AddPermission(companyB.RoleId, permission);
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(await adapter.HasPermissionForAllCompanyPeriodsAsync(
            userId,
            scopes,
            permission,
            TradeAccessIntent.Read,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MultiCompanyPermissionRequiresCurrentSignInSession()
    {
        var permission = TradePermissionCodes.MultiCompanyReportView;
        var company = AddCompany(Domain.FiscalPeriodStatus.Open, [permission]);
        var adapter = CreateAdapter(company.CompanyId, company.PeriodId, includeSessionId: false);

        Assert.False(await adapter.HasPermissionForAllCompanyPeriodsAsync(
            userId,
            [new TradePeriodScope(company.CompanyId, company.PeriodId)],
            permission,
            TradeAccessIntent.Read,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RequestedUserMustMatchAuthenticatedPrincipal()
    {
        var permission = TradePermissionCodes.FormView("import-order");
        var company = AddCompany(Domain.FiscalPeriodStatus.Open, [permission]);
        var adapter = CreateAdapter(company.CompanyId, company.PeriodId);

        Assert.False(await HasAsync(adapter, Guid.NewGuid(), permission, TradeAccessIntent.Read));
    }

    private static async Task<bool> HasAsync(
        TradeAuthorizationAdapter adapter, Guid requestedUserId, string permission, TradeAccessIntent intent)
    {
        var context = await adapter.ResolveCurrentContextAsync(requestedUserId, TestContext.Current.CancellationToken);
        return context is not null &&
            await adapter.HasPermissionAsync(context, permission, intent, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ForgedContextIsDenied()
    {
        var permission = TradePermissionCodes.FormView("import-order");
        var company = AddCompany(Domain.FiscalPeriodStatus.Open, [permission]);
        var adapter = CreateAdapter(company.CompanyId, company.PeriodId);
        var resolved = await adapter.ResolveCurrentContextAsync(userId, TestContext.Current.CancellationToken);
        Assert.NotNull(resolved);

        var forgedCopy = resolved with { };
        var forgedTenant = resolved with { TenantId = Guid.NewGuid() };
        var forgedWrite = resolved with { AllowsWrites = true };

        Assert.True(await adapter.HasPermissionAsync(resolved, permission, TradeAccessIntent.Read, TestContext.Current.CancellationToken));
        Assert.False(await adapter.HasPermissionAsync(forgedCopy, permission, TradeAccessIntent.Read, TestContext.Current.CancellationToken));
        Assert.False(await adapter.HasPermissionAsync(forgedTenant, permission, TradeAccessIntent.Read, TestContext.Current.CancellationToken));
        Assert.False(await adapter.HasPermissionAsync(forgedWrite, permission, TradeAccessIntent.Read, TestContext.Current.CancellationToken));
        Assert.Empty(await adapter.GetGrantedTradePermissionsAsync(forgedCopy, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UnresolvedContextIsDeniedEvenForTheRightValues()
    {
        var permission = TradePermissionCodes.FormView("import-order");
        var company = AddCompany(Domain.FiscalPeriodStatus.Open, [permission]);
        var adapter = CreateAdapter(company.CompanyId, company.PeriodId);

        var neverResolved = new TradeAccessContext(userId, company.CompanyId, company.PeriodId, true);

        Assert.False(await adapter.HasPermissionAsync(neverResolved, permission, TradeAccessIntent.Read, TestContext.Current.CancellationToken));
        Assert.Empty(await adapter.GetGrantedTradePermissionsAsync(neverResolved, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ContextResolvedInAnotherRequestIsDenied()
    {
        var permission = TradePermissionCodes.FormView("import-order");
        var company = AddCompany(Domain.FiscalPeriodStatus.Open, [permission]);
        var firstRequest = CreateAdapter(company.CompanyId, company.PeriodId);
        var resolved = await firstRequest.ResolveCurrentContextAsync(userId, TestContext.Current.CancellationToken);
        Assert.NotNull(resolved);

        var secondRequest = CreateAdapter(company.CompanyId, company.PeriodId);

        Assert.False(await secondRequest.HasPermissionAsync(resolved, permission, TradeAccessIntent.Read, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ContextIsNullWithoutSessionOrForAnotherUserOrEmptyUser()
    {
        var company = AddCompany(Domain.FiscalPeriodStatus.Open, []);
        var withoutSession = CreateAdapter(company.CompanyId, company.PeriodId, includeSessionId: false);
        Assert.Null(await withoutSession.ResolveCurrentContextAsync(userId, TestContext.Current.CancellationToken));

        var adapter = CreateAdapter(company.CompanyId, company.PeriodId);
        Assert.Null(await adapter.ResolveCurrentContextAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.Null(await adapter.ResolveCurrentContextAsync(Guid.Empty, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CookieForForeignCompanyOrPeriodResolvesNull()
    {
        var mine = AddCompany(Domain.FiscalPeriodStatus.Open, []);
        var foreign = AddForeignCompany();

        var foreignCompany = CreateAdapter(foreign.CompanyId, foreign.PeriodId);
        Assert.Null(await foreignCompany.ResolveCurrentContextAsync(userId, TestContext.Current.CancellationToken));

        var foreignPeriod = CreateAdapter(mine.CompanyId, foreign.PeriodId);
        Assert.Null(await foreignPeriod.ResolveCurrentContextAsync(userId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RevokedMembershipResolvesNull()
    {
        var company = AddCompany(Domain.FiscalPeriodStatus.Open, []);
        var adapter = CreateAdapter(company.CompanyId, company.PeriodId);
        Assert.NotNull(await adapter.ResolveCurrentContextAsync(userId, TestContext.Current.CancellationToken));

        var membership = database.UserCompanyMemberships.Single(item => item.UserId == userId && item.TenantId == company.CompanyId);
        membership.IsActive = false;
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        database.ChangeTracker.Clear();

        var next = CreateAdapter(company.CompanyId, company.PeriodId);
        Assert.Null(await next.ResolveCurrentContextAsync(userId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GrantedTradePermissionsListsOnlyTradeCodesOfTheContextCompany()
    {
        var view = TradePermissionCodes.FormView("import-order");
        var company = AddCompany(Domain.FiscalPeriodStatus.Open, [view, "accounting.vouchers.view"]);
        var other = AddCompany(Domain.FiscalPeriodStatus.Open, [TradePermissionCodes.FormView("other")]);
        var adapter = CreateAdapter(company.CompanyId, company.PeriodId);
        var context = await adapter.ResolveCurrentContextAsync(userId, TestContext.Current.CancellationToken);

        var granted = await adapter.GetGrantedTradePermissionsAsync(context!, TestContext.Current.CancellationToken);

        Assert.Equal([view], granted);
        Assert.NotEqual(other.CompanyId, context!.TenantId);
    }

    private (Guid CompanyId, Guid PeriodId) AddForeignCompany()
    {
        var company = new Domain.Company { Name = "Foreign " + Guid.NewGuid().ToString("N")[..8], IsActive = true };
        var period = new Domain.FiscalPeriod
        {
            TenantId = company.Id,
            Company = company,
            Title = "Foreign period",
            StartDate = new DateOnly(2026, 3, 21),
            EndDate = new DateOnly(2027, 3, 20),
            Status = Domain.FiscalPeriodStatus.Open,
        };
        database.Companies.Add(company);
        database.FiscalPeriods.Add(period);
        database.SaveChanges();
        database.ChangeTracker.Clear();
        return (company.Id, period.Id);
    }

    public void Dispose() => database.Dispose();

    private (Guid CompanyId, Guid PeriodId, Guid RoleId) AddCompany(
        Domain.FiscalPeriodStatus status,
        IReadOnlyCollection<string> permissionCodes)
    {
        var company = new Domain.Company { Name = "Trade company " + Guid.NewGuid().ToString("N")[..8], IsActive = true };
        var role = new Domain.CompanyRole
        {
            TenantId = company.Id,
            Company = company,
            Code = "trade-role",
            Name = "Trade role",
            IsActive = true,
        };
        var period = new Domain.FiscalPeriod
        {
            TenantId = company.Id,
            Company = company,
            Title = "Trade fiscal period",
            StartDate = new DateOnly(2026, 3, 21),
            EndDate = new DateOnly(2027, 3, 20),
            Status = status,
        };

        database.Companies.Add(company);
        database.CompanyRoles.Add(role);
        database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = userId,
            TenantId = company.Id,
            Company = company,
            RoleId = role.Id,
            Role = role,
            IsActive = true,
        });
        database.FiscalPeriods.Add(period);

        foreach (var code in permissionCodes)
        {
            AddPermission(role.Id, code);
        }

        database.SaveChanges();
        database.ChangeTracker.Clear();
        return (company.Id, period.Id, role.Id);
    }

    private void AddPermission(Guid roleId, string code)
    {
        var permission = database.PermissionDefinitions.SingleOrDefault(item => item.Code == code);
        if (permission is null)
        {
            permission = new Domain.PermissionDefinition { Code = code, Name = code };
            database.PermissionDefinitions.Add(permission);
        }

        if (!database.CompanyRolePermissions.Any(item => item.RoleId == roleId && item.PermissionId == permission.Id))
        {
            database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
            {
                RoleId = roleId,
                PermissionId = permission.Id,
            });
        }
    }

    private TradeAuthorizationAdapter CreateAdapter(Guid companyId, Guid periodId, bool includeSessionId = true)
    {
        var sessionId = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(CompanyAccessService.AccountKindClaimType, CompanyAccessService.UserAccountKind),
        };
        if (includeSessionId)
        {
            claims.Add(new Claim(WorkContextCookie.SessionIdClaimType, sessionId.ToString()));
        }

        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        cookie.Write(context, userId, sessionId, companyId, periodId);
        var setCookie = context.Response.Headers.SetCookie.ToString();
        context.Request.Headers.Cookie = setCookie.Split(';', 2)[0];
        httpContextAccessor.HttpContext = context;

        return new TradeAuthorizationAdapter(
            database,
            companyAccessService,
            workContextService,
            httpContextAccessor);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "TradeAuthorizationTests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
