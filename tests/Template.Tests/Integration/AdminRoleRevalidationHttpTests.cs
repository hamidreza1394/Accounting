using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// PR-E1a (R-16): the installation-administrator role of a signed-in user is checked against the
/// database on every request, not read once from the role claim of the authentication cookie.
/// The HTTP tests use the real sign-in, the real cookie and the real <c>[Authorize(Roles = Admin)]</c> pages.
/// </summary>
public sealed class AdminRoleRevalidationHttpTests : IClassFixture<FaranouApplicationFactory>
{
	private const string AdminPage = "/Admin/Roles/Index";

	public AdminRoleRevalidationHttpTests(FaranouApplicationFactory factory)
	{
		Factory = factory;
	}

	private FaranouApplicationFactory Factory { get; }

	private async Task<SeededUser> AddUserWithRoleAsync(string roleName, bool roleActive = true)
	{
		return await Factory.WithDatabaseAsync(async database =>
		{
			var user = await TestData.AddUserAsync(database);
			var existing = await database.Roles.FirstOrDefaultAsync(item => item.Name == roleName);
			if (existing is null)
			{
				existing = new Domain.Role(roleName) { IsActive = roleActive };
				database.Roles.Add(existing);
				await database.SaveChangesAsync();
			}

			existing.IsActive = roleActive;
			var stored = await database.Users.SingleAsync(item => item.Id == user.Id);
			stored.RoleId = existing.Id;
			await database.SaveChangesAsync();
			return user;
		});
	}

	private Task SetUserRoleAsync(Guid userId, string? roleName) => Factory.WithDatabaseAsync(async database =>
	{
		var user = await database.Users.SingleAsync(item => item.Id == userId);
		if (roleName is null)
		{
			user.RoleId = null;
		}
		else
		{
			var role = await database.Roles.FirstOrDefaultAsync(item => item.Name == roleName);
			if (role is null)
			{
				role = new Domain.Role(roleName) { IsActive = true };
				database.Roles.Add(role);
				await database.SaveChangesAsync();
			}

			user.RoleId = role.Id;
		}

		await database.SaveChangesAsync();
	});

	private static async Task<bool> IsAdministratorOfSessionAsync(HttpClient client)
	{
		var session = await client.GetFromJsonAsync<JsonElement>("/api/v1/session");
		return session.GetProperty("isAdministrator").GetBoolean();
	}

	[Fact]
	public async Task AnAdministratorWithAValidSession_ReachesTheAdministrationPages()
	{
		var user = await AddUserWithRoleAsync(Constants.Role.Admin);
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var page = await client.GetAsync(AdminPage);

		Assert.Equal(HttpStatusCode.OK, page.StatusCode);
		Assert.True(await IsAdministratorOfSessionAsync(client));
	}

	[Fact]
	public async Task WhenTheAdministratorRoleIsRevoked_TheOldCookieLosesTheAdministrationAccessOnTheNextRequest()
	{
		var user = await AddUserWithRoleAsync(Constants.Role.Admin);
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);
		Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(AdminPage)).StatusCode);

		await SetUserRoleAsync(user.Id, null); // revoked in the database; the cookie is unchanged

		var page = await client.GetAsync(AdminPage);

		Assert.NotEqual(HttpStatusCode.OK, page.StatusCode);
		Assert.Contains("AccessDenied", page.Headers.Location?.OriginalString ?? page.StatusCode.ToString(), StringComparison.OrdinalIgnoreCase);
		// API and Razor Pages agree: one answer to "is this user an administrator?"
		Assert.False(await IsAdministratorOfSessionAsync(client));
	}

	[Fact]
	public async Task WhenTheAdministratorRoleIsDeactivatedOrReplaced_TheOldCookieLosesTheAdministrationAccess()
	{
		var user = await AddUserWithRoleAsync(Constants.Role.Admin);
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		await SetUserRoleAsync(user.Id, TestData.Unique("Other-"));
		Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync(AdminPage)).StatusCode);
		Assert.False(await IsAdministratorOfSessionAsync(client));

		// back to Admin, then the Admin role itself is switched off
		await SetUserRoleAsync(user.Id, Constants.Role.Admin);
		await Factory.WithDatabaseAsync(async database =>
		{
			var role = await database.Roles.SingleAsync(item => item.Name == Constants.Role.Admin);
			role.IsActive = false;
			await database.SaveChangesAsync();
		});
		try
		{
			Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync(AdminPage)).StatusCode);
			Assert.False(await IsAdministratorOfSessionAsync(client));
		}
		finally
		{
			await Factory.WithDatabaseAsync(async database =>
			{
				var role = await database.Roles.SingleAsync(item => item.Name == Constants.Role.Admin);
				role.IsActive = true;
				await database.SaveChangesAsync();
			});
		}
	}

	[Fact]
	public async Task ADeactivatedAdministrator_HasNoAccessAtAll()
	{
		var user = await AddUserWithRoleAsync(Constants.Role.Admin);
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		await Factory.WithDatabaseAsync(async database =>
		{
			var stored = await database.Users.SingleAsync(item => item.Id == user.Id);
			stored.IsActive = false;
			await database.SaveChangesAsync();
		});

		var page = await client.GetAsync(AdminPage);
		var session = await client.GetAsync("/api/v1/session");

		Assert.NotEqual(HttpStatusCode.OK, page.StatusCode);
		Assert.Contains("Login", page.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
		Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
	}

	[Fact]
	public async Task RevokingTheAdministratorRole_DoesNotBreakTheOrdinarySession()
	{
		var user = await AddUserWithRoleAsync(Constants.Role.Admin);
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		await SetUserRoleAsync(user.Id, null);

		// still signed in: the ordinary company API of this user keeps working, repeatedly
		for (var attempt = 0; attempt < 2; attempt++)
		{
			var companies = await client.GetAsync("/api/v1/companies");
			Assert.Equal(HttpStatusCode.OK, companies.StatusCode);
			var session = await client.GetAsync("/api/v1/session");
			Assert.Equal(HttpStatusCode.OK, session.StatusCode);
		}
	}

	[Fact]
	public async Task AUserWhoWasNeverAnAdministrator_GainsNothingFromAnAdministratorClaim()
	{
		// A forged or stale role claim can not be produced over HTTP (the cookie is encrypted), so the
		// validation step that runs for every cookie is driven directly with such a principal.
		var user = await AddUserWithRoleAsync(TestData.Unique("Plain-"));
		var (context, _) = await ValidateAsync(user, claimedRole: Constants.Role.Admin);

		Assert.NotNull(context.Principal);
		Assert.False(context.Principal!.IsInRole(Constants.Role.Admin));
		Assert.Equal(user.Username, context.Principal.Identity!.Name);
	}

	[Fact]
	public async Task ARevokedAdministratorsPrincipal_KeepsItsIdentityAndSession_ButLosesTheAdministratorRole()
	{
		var user = await AddUserWithRoleAsync(Constants.Role.Admin);
		await SetUserRoleAsync(user.Id, null);

		var (context, rejected) = await ValidateAsync(user, claimedRole: Constants.Role.Admin);

		Assert.False(rejected);
		Assert.NotNull(context.Principal);
		Assert.False(context.Principal!.IsInRole(Constants.Role.Admin));
		Assert.NotNull(CompanyAccessGuid(context.Principal));
	}

	[Fact]
	public async Task WhenTheRoleCanNotBeConfirmedBecauseTheDatabaseFails_NoAdministratorRoleIsGranted()
	{
		var principal = new ClaimsPrincipal(new ClaimsIdentity(
		[
			new Claim(ClaimTypes.Name, "x"),
			new Claim(ClaimTypes.Role, Constants.Role.Admin),
			new Claim("AccountKind", "User"),
			new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
			new Claim(Infrastructure.Security.SessionValidator.CredentialStampClaimType, "stamp"),
			new Claim(Infrastructure.Tenancy.WorkContextCookie.SessionIdClaimType, Guid.NewGuid().ToString()),
		], CookieAuthenticationDefaults.AuthenticationScheme));

		// a database that can not be reached: connecting fails quickly on a closed local port
		var services = new ServiceCollection()
			.AddDbContext<Data.DatabaseContext>(options => options.UseSqlServer(
				"Server=127.0.0.1,1;Database=x;User Id=u;Password=p;Connect Timeout=1;TrustServerCertificate=True"))
			.AddLogging()
			.BuildServiceProvider();

		var context = NewContext(principal, services);
		var outcome = await Record.ExceptionAsync(() => Infrastructure.Security.SessionValidator.ValidatePrincipalAsync(context));

		// fail closed: either the request fails or the principal is rejected; the administrator role is never kept
		var kept = outcome is null && context.Principal?.IsInRole(Constants.Role.Admin) == true;
		Assert.False(kept);
	}

	private static Guid? CompanyAccessGuid(ClaimsPrincipal principal) => Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);

	private async Task<(CookieValidatePrincipalContext Context, bool Rejected)> ValidateAsync(SeededUser user, string claimedRole)
	{
		var (sessionId, stamp) = await Factory.WithDatabaseAsync(async database =>
		{
			var stored = await database.Users.SingleAsync(item => item.Id == user.Id);
			var session = new Domain.UserSession { UserId = user.Id, CreatedAtUtc = DateTime.UtcNow, LastSeenAtUtc = DateTime.UtcNow };
			database.UserSessions.Add(session);
			await database.SaveChangesAsync();
			return (session.Id, Infrastructure.Security.SessionValidator.ComputeCredentialStamp(stored.Password));
		});

		var principal = new ClaimsPrincipal(new ClaimsIdentity(
		[
			new Claim(ClaimTypes.Name, user.Username),
			new Claim("FullName", user.Username),
			new Claim(ClaimTypes.Role, claimedRole),
			new Claim("AccountKind", "User"),
			new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
			new Claim(Infrastructure.Security.SessionValidator.CredentialStampClaimType, stamp),
			new Claim(Infrastructure.Tenancy.WorkContextCookie.SessionIdClaimType, sessionId.ToString()),
		], CookieAuthenticationDefaults.AuthenticationScheme));

		using var scope = Factory.Services.CreateScope();
		var context = NewContext(principal, scope.ServiceProvider);
		await Infrastructure.Security.SessionValidator.ValidatePrincipalAsync(context);
		return (context, context.Principal is null);
	}

	private static CookieValidatePrincipalContext NewContext(ClaimsPrincipal principal, IServiceProvider services)
	{
		var http = new DefaultHttpContext { RequestServices = services };
		var scheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
		var ticket = new AuthenticationTicket(principal, CookieAuthenticationDefaults.AuthenticationScheme);
		return new CookieValidatePrincipalContext(http, scheme, new CookieAuthenticationOptions(), ticket);
	}
}
