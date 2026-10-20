using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

/// <summary>
/// Seeds two tenants and the edge cases of a membership: inactive membership, inactive
/// company, inactive role and inactive user. Every test gets its own in-memory database.
/// </summary>
internal sealed class TenantFixture : IDisposable
{
	public TenantFixture()
	{
		var options = new DbContextOptionsBuilder<Data.DatabaseContext>()
			.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
			.Options;

		Database = new Data.DatabaseContext(options);

		Alice = AddUser("alice", isActive: true);
		Bob = AddUser("bob", isActive: true);
		Inactive = AddUser("inactive", isActive: false);

		CompanyA = AddCompany("شرکت الف", isActive: true);
		CompanyB = AddCompany("شرکت ب", isActive: true);
		ClosedCompany = AddCompany("شرکت غیرفعال", isActive: false);
		CompanyWithInactiveRole = AddCompany("شرکت نقش غیرفعال", isActive: true);

		var roleA = AddRole(CompanyA, "accountant", "حسابدار", isActive: true);
		var roleB = AddRole(CompanyB, "manager", "مدیر", isActive: true);
		var roleClosed = AddRole(ClosedCompany, "manager", "مدیر", isActive: true);
		var inactiveRole = AddRole(CompanyWithInactiveRole, "viewer", "مشاهده‌گر", isActive: false);

		// Alice: active in A; inactive membership in B; member of an inactive company and
		// of a company whose role is inactive.
		AddMembership(Alice, CompanyA, roleA, isActive: true);
		AddMembership(Alice, CompanyB, roleB, isActive: false);
		AddMembership(Alice, ClosedCompany, roleClosed, isActive: true);
		AddMembership(Alice, CompanyWithInactiveRole, inactiveRole, isActive: true);

		// Bob: active in B only.
		AddMembership(Bob, CompanyB, roleB, isActive: true);

		// A deactivated account keeps an active membership in A.
		AddMembership(Inactive, CompanyA, roleA, isActive: true);

		AddFiscalPeriod(CompanyA, "سال مالی ۱۴۰۴", new DateOnly(2025, 3, 21), new DateOnly(2026, 3, 20));
		AddFiscalPeriod(CompanyA, "سال مالی ۱۴۰۵", new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20));
		AddFiscalPeriod(CompanyB, "دوره محرمانه شرکت ب", new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20));

		Database.SaveChanges();
		Database.ChangeTracker.Clear();

		Service = new Infrastructure.Tenancy.CompanyAccessService(Database);
	}

	public Data.DatabaseContext Database { get; }

	public Infrastructure.Tenancy.CompanyAccessService Service { get; }

	public Domain.User Alice { get; }

	public Domain.User Bob { get; }

	public Domain.User Inactive { get; }

	public Domain.Company CompanyA { get; }

	public Domain.Company CompanyB { get; }

	public Domain.Company ClosedCompany { get; }

	public Domain.Company CompanyWithInactiveRole { get; }

	public static ClaimsPrincipal CompanyUser(Guid? userId, string username = "alice")
	{
		var claims = new List<Claim>
		{
			new(ClaimTypes.Name, username),
			new("FullName", "آلیس"),
			new("AccountKind", "User"),
		};

		if (userId.HasValue)
		{
			claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
		}

		return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
	}

	public static ClaimsPrincipal InfrastructureAccount()
	{
		return new ClaimsPrincipal(new ClaimsIdentity(
			new[]
			{
				new Claim(ClaimTypes.Name, "master"),
				new Claim(ClaimTypes.Role, Constants.Role.Admin),
				new Claim("AccountKind", "Infrastructure"),
			},
			CookieAuthenticationDefaults.AuthenticationScheme));
	}

	private Domain.User AddUser(string username, bool isActive)
	{
		var user = new Domain.User(emailAddress: $"{username}@example.test")
		{
			Username = username,
			IsActive = isActive,
		};

		Database.Users.Add(user);
		return user;
	}

	private Domain.Company AddCompany(string name, bool isActive)
	{
		var company = new Domain.Company { Name = name, IsActive = isActive };
		Database.Companies.Add(company);
		return company;
	}

	private Domain.CompanyRole AddRole(Domain.Company company, string code, string name, bool isActive)
	{
		var role = new Domain.CompanyRole
		{
			TenantId = company.Id,
			Company = company,
			Code = code,
			Name = name,
			IsActive = isActive,
		};

		Database.CompanyRoles.Add(role);
		return role;
	}

	private void AddMembership(Domain.User user, Domain.Company company, Domain.CompanyRole role, bool isActive)
	{
		Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
		{
			UserId = user.Id,
			TenantId = company.Id,
			Company = company,
			RoleId = role.Id,
			Role = role,
			IsActive = isActive,
		});
	}

	private void AddFiscalPeriod(Domain.Company company, string title, DateOnly start, DateOnly end)
	{
		Database.FiscalPeriods.Add(new Domain.FiscalPeriod
		{
			TenantId = company.Id,
			Company = company,
			Title = title,
			StartDate = start,
			EndDate = end,
		});
	}

	public void Dispose()
	{
		Database.Dispose();
	}
}

/// <summary>
/// The server-side company (tenant) access decision.
/// </summary>
public sealed class CompanyAccessServiceTests
{
	[Fact]
	public void GetUserId_ReturnsTheIdentifier_OfASignedInCompanyUser()
	{
		var userId = Guid.NewGuid();

		Assert.Equal(userId, Infrastructure.Tenancy.CompanyAccessService.GetUserId(TenantFixture.CompanyUser(userId)));
	}

	[Fact]
	public void GetUserId_IsNull_ForAnonymousInfrastructureAndLegacySessions()
	{
		Assert.Null(Infrastructure.Tenancy.CompanyAccessService.GetUserId(new ClaimsPrincipal(new ClaimsIdentity())));
		Assert.Null(Infrastructure.Tenancy.CompanyAccessService.GetUserId(TenantFixture.InfrastructureAccount()));
		Assert.Null(Infrastructure.Tenancy.CompanyAccessService.GetUserId(TenantFixture.CompanyUser(userId: null)));
		Assert.Null(Infrastructure.Tenancy.CompanyAccessService.GetUserId(null));
	}

	[Fact]
	public void GetUserId_IgnoresAnIdentifierClaim_OnAnUnauthenticatedIdentity()
	{
		var forged = new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim("AccountKind", "User"),
			new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
		}));

		Assert.Null(Infrastructure.Tenancy.CompanyAccessService.GetUserId(forged));
	}

	[Fact]
	public async Task AccessibleCompanies_ContainOnlyActiveMembershipsInActiveCompaniesWithActiveRoles()
	{
		using var fixture = new TenantFixture();

		var companies = await fixture.Service.GetAccessibleCompaniesAsync(fixture.Alice.Id);

		var company = Assert.Single(companies);
		Assert.Equal(fixture.CompanyA.Id, company.CompanyId);
		Assert.Equal("حسابدار", company.RoleName);
	}

	[Fact]
	public async Task AUserCanNotAccessACompany_InWhichSheHasNoActiveMembership()
	{
		using var fixture = new TenantFixture();

		// Bob's company: Alice's membership there is inactive.
		Assert.Null(await fixture.Service.FindAccessibleCompanyAsync(fixture.Alice.Id, fixture.CompanyB.Id));
		Assert.Null(await fixture.Service.FindAccessibleCompanyAsync(fixture.Alice.Id, fixture.ClosedCompany.Id));
		Assert.Null(await fixture.Service.FindAccessibleCompanyAsync(fixture.Alice.Id, fixture.CompanyWithInactiveRole.Id));
		Assert.Null(await fixture.Service.FindAccessibleCompanyAsync(fixture.Alice.Id, Guid.NewGuid()));
		Assert.Null(await fixture.Service.FindAccessibleCompanyAsync(fixture.Alice.Id, Guid.Empty));

		// Bob can not reach Alice's company either.
		Assert.Null(await fixture.Service.FindAccessibleCompanyAsync(fixture.Bob.Id, fixture.CompanyA.Id));
	}

	[Fact]
	public async Task ADeactivatedAccount_LosesAccessImmediately_EvenWithAnActiveMembership()
	{
		using var fixture = new TenantFixture();

		Assert.Empty(await fixture.Service.GetAccessibleCompaniesAsync(fixture.Inactive.Id));
		Assert.Null(await fixture.Service.FindAccessibleCompanyAsync(fixture.Inactive.Id, fixture.CompanyA.Id));
	}

	[Fact]
	public async Task ARevokedMembership_TakesEffectOnTheNextRequest()
	{
		using var fixture = new TenantFixture();

		Assert.NotNull(await fixture.Service.FindAccessibleCompanyAsync(fixture.Bob.Id, fixture.CompanyB.Id));

		var membership = await fixture.Database.UserCompanyMemberships
			.SingleAsync(current => current.UserId == fixture.Bob.Id && current.TenantId == fixture.CompanyB.Id);
		membership.IsActive = false;
		await fixture.Database.SaveChangesAsync();

		Assert.Null(await fixture.Service.FindAccessibleCompanyAsync(fixture.Bob.Id, fixture.CompanyB.Id));
	}
}

/// <summary>
/// The JSON API endpoints, invoked directly with the same services the host injects.
/// </summary>
public sealed class ApiEndpointTests
{
	[Fact]
	public async Task Companies_ReturnsOnlyTheCompaniesOfTheSignedInUser()
	{
		using var fixture = new TenantFixture();

		var result = await Server.Api.ApiEndpoints.GetCompaniesAsync(
			TenantFixture.CompanyUser(fixture.Alice.Id), fixture.Service, default);

		var ok = Assert.IsType<Ok<IReadOnlyList<Server.Api.CompanySummaryResponse>>>(result.Result);
		var company = Assert.Single(ok.Value!);
		Assert.Equal(fixture.CompanyA.Id, company.Id);
	}

	[Fact]
	public async Task Companies_IsEmpty_ForTheInfrastructureAccount()
	{
		using var fixture = new TenantFixture();

		var result = await Server.Api.ApiEndpoints.GetCompaniesAsync(
			TenantFixture.InfrastructureAccount(), fixture.Service, default);

		var ok = Assert.IsType<Ok<IReadOnlyList<Server.Api.CompanySummaryResponse>>>(result.Result);
		Assert.Empty(ok.Value!);
	}

	[Fact]
	public async Task Companies_RequiresANewSignIn_ForASessionWithoutTheIdentifierClaim()
	{
		using var fixture = new TenantFixture();

		var result = await Server.Api.ApiEndpoints.GetCompaniesAsync(
			TenantFixture.CompanyUser(userId: null), fixture.Service, default);

		Assert.IsType<UnauthorizedHttpResult>(result.Result);
	}

	[Fact]
	public async Task Overview_ReturnsTheRealFiscalPeriodsOfTheCompany_AndNothingOfAnotherTenant()
	{
		using var fixture = new TenantFixture();

		var result = await Server.Api.ApiEndpoints.GetCompanyOverviewAsync(
			fixture.CompanyA.Id, TenantFixture.CompanyUser(fixture.Alice.Id), fixture.Service, fixture.Database, default);

		var ok = Assert.IsType<Ok<Server.Api.CompanyOverviewResponse>>(result.Result);
		Assert.Equal(fixture.CompanyA.Id, ok.Value!.Company.Id);
		Assert.Equal(2, ok.Value.FiscalPeriods.Count);
		Assert.DoesNotContain(ok.Value.FiscalPeriods, period => period.Title == "دوره محرمانه شرکت ب");

		// Newest first, dates as stored (Gregorian calendar dates).
		Assert.Equal(new DateOnly(2026, 3, 21), ok.Value.FiscalPeriods[0].StartDate);
		Assert.Equal("Open", ok.Value.FiscalPeriods[0].Status);
	}

	[Fact]
	public async Task Overview_OfAnotherTenantsCompany_IsNotFound_LikeAnUnknownCompany()
	{
		using var fixture = new TenantFixture();
		var alice = TenantFixture.CompanyUser(fixture.Alice.Id);

		var otherTenant = await Server.Api.ApiEndpoints.GetCompanyOverviewAsync(
			fixture.CompanyB.Id, alice, fixture.Service, fixture.Database, default);
		var unknown = await Server.Api.ApiEndpoints.GetCompanyOverviewAsync(
			Guid.NewGuid(), alice, fixture.Service, fixture.Database, default);

		Assert.IsType<NotFound>(otherTenant.Result);
		Assert.IsType<NotFound>(unknown.Result);
	}

	[Fact]
	public async Task Overview_IgnoresATenantClaimForgedIntoThePrincipal()
	{
		using var fixture = new TenantFixture();

		// A TenantId claim (as older cookies carried) is not an access decision.
		var principal = TenantFixture.CompanyUser(fixture.Alice.Id);
		((ClaimsIdentity)principal.Identity!).AddClaim(new Claim("TenantId", fixture.CompanyB.Id.ToString()));

		var result = await Server.Api.ApiEndpoints.GetCompanyOverviewAsync(
			fixture.CompanyB.Id, principal, fixture.Service, fixture.Database, default);

		Assert.IsType<NotFound>(result.Result);
	}

	[Fact]
	public void Session_DescribesTheVerifiedIdentity()
	{
		var result = Server.Api.ApiEndpoints.GetSession(TenantFixture.InfrastructureAccount());

		Assert.Equal("master", result.Value!.Username);
		Assert.Equal("master", result.Value.FullName);
		Assert.Equal("Infrastructure", result.Value.AccountKind);
		Assert.True(result.Value.IsAdministrator);
	}

	[Theory]
	[InlineData("/api/v1/companies", true)]
	[InlineData("/API/v1/session", true)]
	[InlineData("/api", true)]
	[InlineData("/apidocs", false)]
	[InlineData("/Account/Login", false)]
	[InlineData("/app/companies", false)]
	public void IsApiRequest_MatchesTheApiPrefixOnly(string path, bool expected)
	{
		Assert.Equal(expected, Server.Api.ApiEndpoints.IsApiRequest(new PathString(path)));
	}
}

/// <summary>
/// An unauthenticated API call gets 401/403; a browser page keeps the login redirect.
/// </summary>
public sealed class ApiCookieAuthenticationEventsTests
{
	private static RedirectContext<CookieAuthenticationOptions> CreateContext(string path, string redirectUri)
	{
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Path = path;

		return new RedirectContext<CookieAuthenticationOptions>(
			httpContext,
			new AuthenticationScheme(
				CookieAuthenticationDefaults.AuthenticationScheme,
				displayName: null,
				handlerType: typeof(CookieAuthenticationHandler)),
			new CookieAuthenticationOptions(),
			new AuthenticationProperties(),
			redirectUri);
	}

	[Fact]
	public async Task AnApiRequest_Receives401_InsteadOfTheLoginPage()
	{
		var context = CreateContext("/api/v1/companies", "https://localhost/Account/Login?ReturnUrl=%2Fapi%2Fv1%2Fcompanies");

		await Server.Api.ApiCookieAuthenticationEvents.RedirectToLoginAsync(context);

		Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
		Assert.False(context.Response.Headers.ContainsKey("Location"));
	}

	[Fact]
	public async Task AnApiRequest_Receives403_WhenAccessIsDenied()
	{
		var context = CreateContext("/api/v1/companies", "https://localhost/Account/AccessDenied");

		await Server.Api.ApiCookieAuthenticationEvents.RedirectToAccessDeniedAsync(context);

		Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
		Assert.False(context.Response.Headers.ContainsKey("Location"));
	}

	[Fact]
	public async Task ABrowserPage_IsStillRedirectedToTheLoginPage()
	{
		const string loginUri = "https://localhost/Account/Login?ReturnUrl=%2F";
		var context = CreateContext("/", loginUri);

		await Server.Api.ApiCookieAuthenticationEvents.RedirectToLoginAsync(context);

		Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
		Assert.Equal(loginUri, context.Response.Headers.Location.ToString());
	}
}

/// <summary>
/// Static guards of the session handling in the markup and the page models.
/// </summary>
public sealed class SessionHardeningTests
{
	private static string ReadRepositoryFile(string relativePath)
	{
		var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

		while (directory.Parent != null &&
			!File.Exists(Path.Combine(directory.FullName, "Template.sln")))
		{
			directory = directory.Parent;
		}

		return File.ReadAllText(Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)));
	}

	[Fact]
	public void Logout_SignsOutOnlyOnPost()
	{
		var type = typeof(Server.Pages.Account.LogoutModel);

		Assert.NotNull(type.GetMethod("OnPostAsync"));
		Assert.Equal(typeof(void), type.GetMethod("OnGet")!.ReturnType);

		var markup = ReadRepositoryFile("Server/Pages/Shared/PartialViews/_LoginStatus.cshtml");
		Assert.DoesNotContain("<a class=\"nav-link\" asp-page=\"/Account/Logout\">", markup);
		Assert.Contains("<form method=\"post\" asp-page=\"/Account/Logout\"", markup);
	}

	[Fact]
	public void Program_KeepsTheRazorLogin_AndAnswersApiRequestsWithStatusCodes()
	{
		var program = ReadRepositoryFile("Server/Program.cs");

		Assert.Contains("options.LoginPath = \"/Account/Login\";", program);
		Assert.Contains("ApiCookieAuthenticationEvents.RedirectToLoginAsync", program);
		Assert.Contains("ApiCookieAuthenticationEvents.RedirectToAccessDeniedAsync", program);
		Assert.Contains("AddScoped<Infrastructure.Tenancy.CompanyAccessService>", program);
		Assert.Contains("app.MapFaranouApi();", program);
	}
}
