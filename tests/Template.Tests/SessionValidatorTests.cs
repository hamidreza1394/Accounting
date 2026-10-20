using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Template.Tests;

/// <summary>
/// The per-request re-validation of the authentication cookie.
/// </summary>
public sealed class SessionValidatorTests
{
	private static ClaimsPrincipal Principal(string accountKind, string name, Guid? userId, string? stamp)
	{
		var claims = new List<Claim>
		{
			new(ClaimTypes.Name, name),
			new("AccountKind", accountKind),
		};

		if (userId.HasValue)
		{
			claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
		}

		if (stamp is not null)
		{
			claims.Add(new Claim(Infrastructure.Security.SessionValidator.CredentialStampClaimType, stamp));
		}

		return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
	}

	private static (ServiceProvider Services, Data.DatabaseContext Database) CreateServices(
		Infrastructure.Settings.ApplicationSettings? settings = null)
	{
		var databaseName = Guid.NewGuid().ToString();

		var services = new ServiceCollection()
			.AddSingleton(settings ?? new Infrastructure.Settings.ApplicationSettings())
			.AddDbContext<Data.DatabaseContext>(options => options.UseInMemoryDatabase(databaseName))
			.BuildServiceProvider();

		return (services, services.CreateScope().ServiceProvider.GetRequiredService<Data.DatabaseContext>());
	}

	private static Domain.User AddUser(Data.DatabaseContext database, string passwordHash, bool isActive = true)
	{
		var user = new Domain.User(emailAddress: $"{Guid.NewGuid():N}@example.test")
		{
			Username = "session-user",
			Password = passwordHash,
			IsActive = isActive,
		};

		database.Users.Add(user);
		database.SaveChanges();
		return user;
	}

	[Fact]
	public async Task AnUnchangedActiveAccount_KeepsItsSession()
	{
		var (services, database) = CreateServices();
		var user = AddUser(database, "hash-1");

		var principal = Principal("User", "session-user", user.Id,
			Infrastructure.Security.SessionValidator.ComputeCredentialStamp("hash-1"));

		Assert.True(await Infrastructure.Security.SessionValidator.IsSessionValidAsync(principal, services, default));
	}

	[Fact]
	public async Task AChangedCredential_ADeactivatedOrDeletedAccount_EndTheSession()
	{
		var (services, database) = CreateServices();
		var user = AddUser(database, "hash-1");
		var stampOfOldHash = Infrastructure.Security.SessionValidator.ComputeCredentialStamp("hash-1");

		user.Password = "hash-2";
		database.SaveChanges();
		Assert.False(await Infrastructure.Security.SessionValidator.IsSessionValidAsync(
			Principal("User", "session-user", user.Id, stampOfOldHash), services, default));

		var inactive = AddUser(database, "hash-3", isActive: false);
		Assert.False(await Infrastructure.Security.SessionValidator.IsSessionValidAsync(
			Principal("User", "session-user", inactive.Id, Infrastructure.Security.SessionValidator.ComputeCredentialStamp("hash-3")),
			services, default));

		Assert.False(await Infrastructure.Security.SessionValidator.IsSessionValidAsync(
			Principal("User", "session-user", Guid.NewGuid(), stampOfOldHash), services, default));
	}

	[Fact]
	public async Task ASessionOfAnEarlierRelease_WithoutAStamp_MustSignInAgain()
	{
		var (services, database) = CreateServices();
		var user = AddUser(database, "hash-1");

		Assert.False(await Infrastructure.Security.SessionValidator.IsSessionValidAsync(
			Principal("User", "session-user", user.Id, stamp: null), services, default));
	}

	[Fact]
	public async Task TheInfrastructureAccount_IsBoundToItsConfiguredCredential()
	{
		var settings = new Infrastructure.Settings.ApplicationSettings
		{
			MasterUsername = "master",
			MasterPassword = "configured-hash",
		};
		var (services, _) = CreateServices(settings);
		var stamp = Infrastructure.Security.SessionValidator.ComputeCredentialStamp("configured-hash");

		Assert.True(await Infrastructure.Security.SessionValidator.IsSessionValidAsync(
			Principal("Infrastructure", "master", null, stamp), services, default));

		settings.MasterPassword = "rotated-hash";
		Assert.False(await Infrastructure.Security.SessionValidator.IsSessionValidAsync(
			Principal("Infrastructure", "master", null, stamp), services, default));
	}

	[Fact]
	public void TheStamp_DoesNotContainTheStoredCredential()
	{
		var stamp = Infrastructure.Security.SessionValidator.ComputeCredentialStamp("AQAAAAIAAYagAAAAEStoredHash");

		Assert.DoesNotContain("StoredHash", stamp, StringComparison.Ordinal);
		Assert.Equal(44, stamp.Length);
	}
}

/// <summary>
/// The security headers per area of the application.
/// </summary>
public sealed class SecurityHeadersTests
{
	[Theory]
	[InlineData("/app/", Infrastructure.Security.SecurityHeadersMiddleware.ClientPolicy)]
	[InlineData("/app/companies/1", Infrastructure.Security.SecurityHeadersMiddleware.ClientPolicy)]
	[InlineData("/api/v1/session", Infrastructure.Security.SecurityHeadersMiddleware.ApiPolicy)]
	[InlineData("/Account/Login", Infrastructure.Security.SecurityHeadersMiddleware.RazorBaselinePolicy)]
	public void EachAreaGetsItsPolicy(string path, string expectedPolicy)
	{
		IHeaderDictionary headers = new HeaderDictionary();

		Infrastructure.Security.SecurityHeadersMiddleware.ApplyHeaders(new PathString(path), headers, new Infrastructure.Security.SecurityHeadersSettings());

		Assert.Equal(expectedPolicy, headers.ContentSecurityPolicy.ToString());
		Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
	}

	[Fact]
	public void TheRazorStrictPolicy_IsReportOnly_UntilItIsEnforced()
	{
		IHeaderDictionary reportOnly = new HeaderDictionary();
		Infrastructure.Security.SecurityHeadersMiddleware.ApplyHeaders(new PathString("/"), reportOnly, new Infrastructure.Security.SecurityHeadersSettings());
		Assert.Equal(Infrastructure.Security.SecurityHeadersMiddleware.RazorStrictPolicy, reportOnly["Content-Security-Policy-Report-Only"].ToString());

		IHeaderDictionary enforced = new HeaderDictionary();
		Infrastructure.Security.SecurityHeadersMiddleware.ApplyHeaders(new PathString("/"), enforced, new Infrastructure.Security.SecurityHeadersSettings { EnforceRazorContentSecurityPolicy = true });
		Assert.Equal(Infrastructure.Security.SecurityHeadersMiddleware.RazorStrictPolicy, enforced.ContentSecurityPolicy.ToString());
		Assert.False(enforced.ContainsKey("Content-Security-Policy-Report-Only"));
	}

	[Fact]
	public void ReportOnlyPrefixes_KeepTheStrictPolicyReportOnly_WhileItIsEnforcedElsewhere()
	{
		var settings = new Infrastructure.Security.SecurityHeadersSettings
		{
			EnforceRazorContentSecurityPolicy = true,
			RazorReportOnlyPathPrefixes = ["/Admin/PageManager"],
		};

		IHeaderDictionary editor = new HeaderDictionary();
		Infrastructure.Security.SecurityHeadersMiddleware.ApplyHeaders(new PathString("/admin/pagemanager/create"), editor, settings);
		Assert.Equal(Infrastructure.Security.SecurityHeadersMiddleware.RazorBaselinePolicy, editor.ContentSecurityPolicy.ToString());
		Assert.Equal(Infrastructure.Security.SecurityHeadersMiddleware.RazorStrictPolicy, editor["Content-Security-Policy-Report-Only"].ToString());

		IHeaderDictionary other = new HeaderDictionary();
		Infrastructure.Security.SecurityHeadersMiddleware.ApplyHeaders(new PathString("/Admin/Users"), other, settings);
		Assert.Equal(Infrastructure.Security.SecurityHeadersMiddleware.RazorStrictPolicy, other.ContentSecurityPolicy.ToString());
	}

	[Fact]
	public void Reporting_AddsTheReportUri_ToDocumentPolicies_Only()
	{
		var settings = new Infrastructure.Security.SecurityHeadersSettings
		{
			EnforceRazorContentSecurityPolicy = true,
			CspReportingEnabled = true,
		};

		IHeaderDictionary page = new HeaderDictionary();
		Infrastructure.Security.SecurityHeadersMiddleware.ApplyHeaders(new PathString("/Account/Login"), page, settings);
		Assert.EndsWith("; report-uri /csp-report", page.ContentSecurityPolicy.ToString(), StringComparison.Ordinal);

		IHeaderDictionary api = new HeaderDictionary();
		Infrastructure.Security.SecurityHeadersMiddleware.ApplyHeaders(new PathString("/api/v1/session"), api, settings);
		Assert.Equal(Infrastructure.Security.SecurityHeadersMiddleware.ApiPolicy, api.ContentSecurityPolicy.ToString());
	}

	[Fact]
	public void NoPolicyAllowsUnsafeEval()
	{
		Assert.DoesNotContain("unsafe-eval", Infrastructure.Security.SecurityHeadersMiddleware.ClientPolicy, StringComparison.Ordinal);
		Assert.DoesNotContain("unsafe-eval", Infrastructure.Security.SecurityHeadersMiddleware.RazorStrictPolicy, StringComparison.Ordinal);
		Assert.DoesNotContain("unsafe-inline", Infrastructure.Security.SecurityHeadersMiddleware.ClientPolicy, StringComparison.Ordinal);
	}
}
