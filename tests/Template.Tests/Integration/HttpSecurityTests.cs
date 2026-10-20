using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// End-to-end HTTP behaviour of authentication, anti-forgery, session revalidation and company
/// isolation through the real pipeline of Program.cs. The database is SQL Server in CI
/// (FARANOU_TEST_SQLSERVER) and the in-memory provider otherwise; see
/// <see cref="IntegrationDatabase" />.
/// </summary>
public sealed class HttpSecurityTests : IClassFixture<FaranouApplicationFactory>
{
	private static readonly DateOnly Start1405 = new(2026, 3, 21);
	private static readonly DateOnly End1405 = new(2027, 3, 20);

	public HttpSecurityTests(FaranouApplicationFactory factory, ITestOutputHelper output)
	{
		Factory = factory;
		output.WriteLine($"Integration database provider: {IntegrationDatabase.ProviderDescription}");
	}

	private FaranouApplicationFactory Factory { get; }

	#region Anonymous requests
	[Fact]
	public async Task AnAnonymousApiRequest_Gets401_AndNotTheLoginPage()
	{
		using var client = Factory.CreateBrowser();

		var response = await client.GetAsync("/api/v1/companies");
		var body = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
		Assert.Null(response.Headers.Location);
		Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("__RequestVerificationToken", body, StringComparison.Ordinal);
	}

	[Fact]
	public async Task AnAnonymousPageRequest_IsStillRedirectedToTheLoginPage()
	{
		using var client = Factory.CreateBrowser();

		var response = await client.GetAsync("/");

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Equal("/Account/Login", response.Headers.Location!.AbsolutePath, ignoreCase: true);
		Assert.Contains("ReturnUrl=%2F", response.Headers.Location.Query, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task TheLoginPage_RendersItsFormWithAnAntiForgeryToken()
	{
		using var client = Factory.CreateBrowser();

		var token = await Browser.ReadFormTokenAsync(client, "/Account/Login");

		Assert.False(string.IsNullOrWhiteSpace(token));
	}
	#endregion

	#region Sign-in
	[Fact]
	public async Task ASuccessfulSignIn_IssuesASecureHttpOnlyCookie_ForTheRightUser()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		var company = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("شرکت-")));
		await Factory.WithDatabaseAsync(database => TestData.AddMembershipAsync(database, user, company));

		using var client = Factory.CreateBrowser();
		var response = await Browser.PostLoginAsync(client, user.Username, user.Password);

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Equal("/Account/LoginSuccess", response.Headers.Location!.OriginalString, ignoreCase: true);

		var cookie = response.Headers.GetValues("Set-Cookie")
			.Single(value => value.StartsWith(".Faranou.Authentication=", StringComparison.Ordinal));
		Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
		// Without "Remember me" the cookie is a session cookie.
		Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);

		var session = await client.GetFromJsonAsync<JsonElement>("/api/v1/session");
		Assert.Equal(user.Username, session.GetProperty("username").GetString());
		Assert.Equal("User", session.GetProperty("accountKind").GetString());

		// The identity behind the cookie is this user: exactly his company is visible.
		var companies = await client.GetFromJsonAsync<JsonElement>("/api/v1/companies");
		Assert.Equal(company.Id.ToString(), Assert.Single(companies.EnumerateArray()).GetProperty("id").GetString());
	}

	[Fact]
	public async Task RememberMe_IssuesAPersistentCookie()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();

		var response = await Browser.PostLoginAsync(client, user.Username, user.Password, rememberMe: true);

		var cookie = response.Headers.GetValues("Set-Cookie")
			.Single(value => value.StartsWith(".Faranou.Authentication=", StringComparison.Ordinal));
		Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task ALocalReturnUrl_IsFollowed_AndAnExternalOneIsNot()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));

		using (var client = Factory.CreateBrowser())
		{
			var local = await Browser.PostLoginAsync(client, user.Username, user.Password, returnUrl: "/app/?company=x");
			Assert.Equal("/app/?company=x", local.Headers.Location!.OriginalString);
		}

		using (var client = Factory.CreateBrowser())
		{
			var external = await Browser.PostLoginAsync(client, user.Username, user.Password, returnUrl: "https://evil.example/");
			Assert.Equal("/Account/LoginSuccess", external.Headers.Location!.OriginalString, ignoreCase: true);
		}
	}

	[Fact]
	public async Task AWrongPassword_IssuesNoCookie()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();

		var response = await Browser.PostLoginAsync(client, user.Username, "Wrong-Password-1");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies)
			&& cookies.Any(value => value.StartsWith(".Faranou.Authentication=", StringComparison.Ordinal)));
		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/session")).StatusCode);
	}

	[Fact]
	public async Task TheLoginForm_IsRejectedWithoutItsAntiForgeryToken()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();

		var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["ViewModel.Username"] = user.Username,
			["ViewModel.Password"] = user.Password,
		}));

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/session")).StatusCode);
	}
	#endregion

	#region Anti-forgery and sign-out
	[Fact]
	public async Task ApiSignOut_WithoutTheAntiForgeryHeader_IsRejected_AndTheSessionSurvives()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var response = await client.PostAsync("/api/v1/session/logout", content: null);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/session")).StatusCode);
	}

	[Fact]
	public async Task ApiSignOut_WithAForgedToken_IsRejected()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/session/logout");
		request.Headers.Add("X-XSRF-TOKEN", "forged-token");

		Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/session")).StatusCode);
	}

	[Fact]
	public async Task ApiSignOut_WithAValidToken_EndsTheSession()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/antiforgery/token");
		using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/session/logout");
		request.Headers.Add(token.GetProperty("headerName").GetString()!, token.GetProperty("requestToken").GetString());

		Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(request)).StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/session")).StatusCode);
	}

	[Fact]
	public async Task RazorSignOut_HappensOnlyOnAPostWithAValidToken()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		// A GET (a link or an image on another site) does not end the session.
		Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Logout")).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/session")).StatusCode);

		// A POST without the token is rejected.
		var forged = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>()));
		Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/session")).StatusCode);

		// The form of the page signs out.
		var token = await Browser.ReadFormTokenAsync(client, "/Account/Logout");
		var response = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token,
		}));

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/session")).StatusCode);
	}
	#endregion

	#region Session revalidation
	[Fact]
	public async Task DeactivatingTheAccount_EndsTheSession_OnTheNextRequest()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);
		Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/session")).StatusCode);

		await Factory.WithDatabaseAsync(async database =>
		{
			var entity = await database.Users.SingleAsync(current => current.Id == user.Id);
			entity.IsActive = false;
			await database.SaveChangesAsync();
		});

		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/session")).StatusCode);
		Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/")).StatusCode);
	}

	[Fact]
	public async Task ChangingThePassword_EndsExistingSessions()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		await Factory.WithDatabaseAsync(async database =>
		{
			var entity = await database.Users.SingleAsync(current => current.Id == user.Id);
			entity.Password = new Infrastructure.Security.PasswordHashService().Hash(password: "A-New-Password-1");
			await database.SaveChangesAsync();
		});

		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/session")).StatusCode);
	}
	#endregion

	#region Company isolation
	[Fact]
	public async Task AUserSeesOnlyActiveMembershipsInActiveCompaniesWithActiveRoles()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		var visible = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("visible-")));
		var revoked = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("revoked-")));
		var closed = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("closed-"), isActive: false));
		var inactiveRole = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("role-"), roleIsActive: false));

		await Factory.WithDatabaseAsync(async database =>
		{
			await TestData.AddMembershipAsync(database, user, visible);
			await TestData.AddMembershipAsync(database, user, revoked, isActive: false);
			await TestData.AddMembershipAsync(database, user, closed);
			await TestData.AddMembershipAsync(database, user, inactiveRole);
		});

		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var companies = await client.GetFromJsonAsync<JsonElement>("/api/v1/companies");
		var ids = companies.EnumerateArray().Select(company => company.GetProperty("id").GetString()).ToList();

		Assert.Equal(new[] { visible.Id.ToString() }, ids);
	}

	[Fact]
	public async Task RevokingAMembership_RemovesAccess_OnTheNextRequest()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		var company = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("c-")));
		await Factory.WithDatabaseAsync(database => TestData.AddMembershipAsync(database, user, company));

		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);
		Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/companies/{company.Id}/overview")).StatusCode);

		await Factory.WithDatabaseAsync(async database =>
		{
			var membership = await database.UserCompanyMemberships
				.SingleAsync(current => current.UserId == user.Id && current.TenantId == company.Id);
			membership.IsActive = false;
			await database.SaveChangesAsync();
		});

		Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/companies/{company.Id}/overview")).StatusCode);
		Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/v1/companies")).EnumerateArray());
	}

	[Fact]
	public async Task AnotherTenantsCompany_AndAnUnknownCompany_GetTheSame404()
	{
		var alice = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		var bob = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		var bobsCompany = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("bob-")));
		await Factory.WithDatabaseAsync(database => TestData.AddMembershipAsync(database, bob, bobsCompany));

		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, alice);

		var foreign = await client.GetAsync($"/api/v1/companies/{bobsCompany.Id}/overview");
		var unknown = await client.GetAsync($"/api/v1/companies/{Guid.NewGuid()}/overview");

		Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
		Assert.Equal(await unknown.Content.ReadAsStringAsync(), await foreign.Content.ReadAsStringAsync());
		Assert.Equal(unknown.Content.Headers.ContentType?.ToString(), foreign.Content.Headers.ContentType?.ToString());
	}

	[Fact]
	public async Task ACompanyIdThatIsNotAGuid_IsNotRouted()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var response = await client.GetAsync("/api/v1/companies/1%20OR%201=1/overview");

		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Fact]
	public async Task TheOverview_ContainsOnlyTheDataOfTheRequestedCompany()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		var mine = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("mine-")));
		var other = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("other-")));
		var secretTitle = TestData.Unique("secret-period-");

		await Factory.WithDatabaseAsync(async database =>
		{
			await TestData.AddMembershipAsync(database, user, mine);
			await TestData.AddMembershipAsync(database, user, other);
			await TestData.AddFiscalPeriodAsync(database, mine, "سال مالی ۱۴۰۵", Start1405, End1405);
			await TestData.AddFiscalPeriodAsync(database, other, secretTitle, Start1405, End1405);
		});

		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var body = await client.GetStringAsync($"/api/v1/companies/{mine.Id}/overview");
		var overview = JsonDocument.Parse(body).RootElement;

		Assert.Equal(mine.Id.ToString(), overview.GetProperty("company").GetProperty("id").GetString());
		var period = Assert.Single(overview.GetProperty("fiscalPeriods").EnumerateArray());
		Assert.Equal("2026-03-21", period.GetProperty("startDate").GetString());
		Assert.DoesNotContain(secretTitle, body, StringComparison.Ordinal);
		Assert.DoesNotContain(other.Id.ToString(), body, StringComparison.OrdinalIgnoreCase);
	}
	#endregion

	#region Headers
	[Fact]
	public async Task ApiResponses_AreNotCached_AndCarryAStrictPolicy()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var response = await client.GetAsync("/api/v1/session");

		Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
		Assert.Equal(
			Infrastructure.Security.SecurityHeadersMiddleware.ApiPolicy,
			response.Headers.GetValues("Content-Security-Policy").Single());
		Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
	}

	[Fact]
	public async Task TheLoginPage_GetsTheBaselinePolicy_AndTheStrictPolicyAsReportOnly()
	{
		using var client = Factory.CreateBrowser();

		var response = await client.GetAsync("/Account/Login");

		Assert.Equal(
			Infrastructure.Security.SecurityHeadersMiddleware.RazorBaselinePolicy,
			response.Headers.GetValues("Content-Security-Policy").Single());
		Assert.Equal(
			Infrastructure.Security.SecurityHeadersMiddleware.RazorStrictPolicy,
			response.Headers.GetValues("Content-Security-Policy-Report-Only").Single());
	}

	[Fact]
	public async Task TheClientPath_GetsTheEnforcedStrictPolicy()
	{
		using var client = Factory.CreateBrowser();

		// The policy is set whether or not the client has been built into wwwroot/app.
		var response = await client.GetAsync("/app/");

		var policy = response.Headers.GetValues("Content-Security-Policy").Single();
		Assert.Equal(Infrastructure.Security.SecurityHeadersMiddleware.ClientPolicy, policy);
		Assert.DoesNotContain("unsafe-inline", policy, StringComparison.Ordinal);
		Assert.DoesNotContain("unsafe-eval", policy, StringComparison.Ordinal);
	}
	#endregion
}

/// <summary>
/// The infrastructure (master) account through the real pipeline.
/// </summary>
public sealed class InfrastructureAccountTests : IClassFixture<InfrastructureAccountTests.Factory>
{
	public const string MasterUsername = "it-master";
	public const string MasterPassword = "Master-Password-1";

	public sealed class Factory : FaranouApplicationFactory
	{
		protected override IDictionary<string, string?> Settings
		{
			get
			{
				var settings = base.Settings;
				settings["ApplicationSettings:MasterUsername"] = MasterUsername;
				settings["ApplicationSettings:MasterPassword"] =
					new Infrastructure.Security.PasswordHashService().Hash(password: MasterPassword);
				return settings;
			}
		}
	}

	public InfrastructureAccountTests(Factory factory)
	{
		ApplicationFactory = factory;
	}

	private Factory ApplicationFactory { get; }

	[Fact]
	public async Task TheInfrastructureAccount_SignsIn_AndHasNoCompany()
	{
		using var client = ApplicationFactory.CreateBrowser();

		var response = await Browser.PostLoginAsync(client, MasterUsername, MasterPassword);
		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

		var session = await client.GetFromJsonAsync<JsonElement>("/api/v1/session");
		Assert.Equal("Infrastructure", session.GetProperty("accountKind").GetString());
		Assert.True(session.GetProperty("isAdministrator").GetBoolean());
		Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/v1/companies")).EnumerateArray());
	}
}

/// <summary>
/// An unexpected server error is logged, never shown: no exception text, connection string
/// or stack trace reaches the client.
/// </summary>
public sealed class ErrorDisclosureTests : IClassFixture<ErrorDisclosureTests.Factory>
{
	public const string SecretInException = "Server=db.internal;Password=Sup3r-Secret-Value";

	public sealed class Factory : FaranouApplicationFactory
	{
		protected override void ConfigureServices(IServiceCollection services)
		{
			// The company service fails while it is created, before any endpoint filter runs.
			services.AddScoped<Infrastructure.Tenancy.CompanyAccessService>(
				_ => throw new InvalidOperationException($"Simulated failure. {SecretInException}"));
		}
	}

	public ErrorDisclosureTests(Factory factory)
	{
		ApplicationFactory = factory;
	}

	private Factory ApplicationFactory { get; }

	[Fact]
	public async Task AnApiFailure_ReturnsAGenericProblem_WithoutDetails()
	{
		var user = await ApplicationFactory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = ApplicationFactory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var response = await client.GetAsync("/api/v1/companies");
		var body = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
		Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
		Assert.DoesNotContain("Sup3r-Secret-Value", body, StringComparison.Ordinal);
		Assert.DoesNotContain("db.internal", body, StringComparison.Ordinal);
		Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
		Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);
	}
}

/// <summary>
/// The configured rate limits are applied by the real middleware.
/// </summary>
public sealed class RateLimitingTests : IClassFixture<RateLimitingTests.Factory>
{
	public sealed class Factory : FaranouApplicationFactory
	{
		protected override IDictionary<string, string?> Settings
		{
			get
			{
				var settings = base.Settings;
				settings["RateLimiting:Api:PermitLimit"] = "3";
				settings["RateLimiting:Api:WindowSeconds"] = "3600";
				settings["RateLimiting:Login:PermitLimit"] = "2";
				settings["RateLimiting:Login:WindowSeconds"] = "3600";
				return settings;
			}
		}
	}

	public RateLimitingTests(Factory factory)
	{
		ApplicationFactory = factory;
	}

	private Factory ApplicationFactory { get; }

	[Fact]
	public async Task TheApi_AnswersWith429_AfterTheConfiguredLimit()
	{
		using var client = ApplicationFactory.CreateBrowser();
		var statuses = new List<HttpStatusCode>();

		for (var attempt = 0; attempt < 4; attempt++)
		{
			statuses.Add((await client.GetAsync("/api/v1/session")).StatusCode);
		}

		Assert.Equal(
			new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, (HttpStatusCode)429 },
			statuses);

		var rejected = await client.GetAsync("/api/v1/session");
		Assert.Equal((HttpStatusCode)429, rejected.StatusCode);
		Assert.True(rejected.Headers.Contains("Retry-After"));
		Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
	}

	[Fact]
	public async Task SignInSubmissions_AreLimited_ButShowingTheFormIsNot()
	{
		using var client = ApplicationFactory.CreateBrowser();

		// Each PostLoginAsync first GETs the form (never limited), then POSTs it.
		var first = await Browser.PostLoginAsync(client, "nobody-1", "Wrong-Password-1");
		var second = await Browser.PostLoginAsync(client, "nobody-2", "Wrong-Password-1");

		Assert.Equal(HttpStatusCode.OK, first.StatusCode);
		Assert.Equal(HttpStatusCode.OK, second.StatusCode);

		var token = await Browser.ReadFormTokenAsync(client, "/Account/Login");
		var third = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["ViewModel.Username"] = "nobody-3",
			["ViewModel.Password"] = "Wrong-Password-1",
			["__RequestVerificationToken"] = token,
		}));

		Assert.Equal((HttpStatusCode)429, third.StatusCode);
	}
}
