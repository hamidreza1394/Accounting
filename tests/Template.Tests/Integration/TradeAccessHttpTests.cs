using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Faranou.Modules.Trade.Application.Authorization;
using Faranou.Modules.Trade.Application.Licensing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Server.Api.Trade;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>Production DI (deny-all entitlement) plus test-only probe endpoints under the gated group.</summary>
public class TradeProbeFactory : FaranouApplicationFactory
{
	protected virtual void ConfigureEntitlement(IServiceCollection services)
	{
	}

	protected override void ConfigureServices(IServiceCollection services)
	{
		ConfigureEntitlement(services);
		services.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter, ProbeStartupFilter>();
	}

	// Probes exist only in this test factory; production code maps only /capabilities.
	private sealed class ProbeStartupFilter : Microsoft.AspNetCore.Hosting.IStartupFilter
	{
		public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
		{
			next(app);
			app.UseEndpoints(endpoints =>
			{
				var group = endpoints.MapGroup("/api/v1/trade")
					.RequireAuthorization()
					.AddEndpointFilter<TradeAccessFilter>()
					.WithMetadata(new TradeGateMarker());

				group.MapGet("/_probe/read", () => TypedResults.Ok(new { ok = true }))
					.RequireTradePermission(TradePermissionCodes.FormView("probe"), TradeAccessIntent.Read);
				group.MapPost("/_probe/write", () => TypedResults.Ok(new { ok = true }))
					.RequireTradePermission(TradePermissionCodes.FormUpdate("probe"), TradeAccessIntent.Write);
				group.MapGet("/_probe/unannotated", () => TypedResults.Ok(new { ok = true }));
				group.MapPost("/_probe/post-declared-read", () => TypedResults.Ok(new { ok = true }))
					.RequireTradePermission(TradePermissionCodes.FormView("probe"), TradeAccessIntent.Read);
			});
		};
	}
}

public sealed class GrantingTradeProbeFactory : TradeProbeFactory
{
	// Test-only granting fake; production registers only the deny-all implementation.
	protected override void ConfigureEntitlement(IServiceCollection services)
	{
		services.RemoveAll<ITradeModuleEntitlement>();
		services.AddScoped<ITradeModuleEntitlement, GrantingEntitlement>();
	}

	private sealed class GrantingEntitlement : ITradeModuleEntitlement
	{
		public Task<bool> IsEnabledForCompanyAsync(Guid tenantId, CancellationToken cancellationToken = default)
			=> Task.FromResult(true);
	}
}

/// <summary>Keeps cookies itself so a test can inspect and tamper with them.</summary>
internal sealed class CookieJar : DelegatingHandler
{
	public Dictionary<string, string> Cookies { get; } = new(StringComparer.Ordinal);

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		if (Cookies.Count > 0)
		{
			request.Headers.Remove("Cookie");
			request.Headers.Add("Cookie", string.Join("; ", Cookies.Select(pair => $"{pair.Key}={pair.Value}")));
		}

		var response = await base.SendAsync(request, cancellationToken);
		if (response.Headers.TryGetValues("Set-Cookie", out var values))
		{
			foreach (var header in values)
			{
				var pair = header.Split(';', 2)[0];
				var index = pair.IndexOf('=');
				if (index <= 0)
				{
					continue;
				}

				var name = pair[..index];
				var value = pair[(index + 1)..];
				if (value.Length == 0)
				{
					Cookies.Remove(name);
				}
				else
				{
					Cookies[name] = value;
				}
			}
		}

		return response;
	}
}

internal static class TradeHttp
{
	public static readonly DateOnly Start = new(2026, 3, 21);
	public static readonly DateOnly End = new(2027, 3, 20);

	public static HttpClient Create(WebApplicationFactory<Program> factory, out CookieJar jar)
	{
		jar = new CookieJar { InnerHandler = factory.Server.CreateHandler() };
		return new HttpClient(jar) { BaseAddress = new Uri("https://localhost") };
	}

	public static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, object? body = null)
	{
		var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/antiforgery/token", TestContext.Current.CancellationToken);
		using var request = new HttpRequestMessage(method, path);
		request.Headers.Add(token.GetProperty("headerName").GetString()!, token.GetProperty("requestToken").GetString());
		if (body is not null)
		{
			request.Content = JsonContent.Create(body);
		}

		return await client.SendAsync(request, TestContext.Current.CancellationToken);
	}

	public static async Task<(SeededUser User, SeededCompany Company, Guid PeriodId)> SeedAsync(
		FaranouApplicationFactory factory,
		Domain.FiscalPeriodStatus status = Domain.FiscalPeriodStatus.Open,
		params string[] permissionCodes)
	{
		var user = await factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		var company = await factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("trade-")));
		await factory.WithDatabaseAsync(database => TestData.AddMembershipAsync(database, user, company));
		var periodId = await factory.WithDatabaseAsync(database =>
			TestData.AddFiscalPeriodAsync(database, company, "trade", Start, End, status));

		await factory.WithDatabaseAsync(async database =>
		{
			foreach (var code in permissionCodes)
			{
				var permission = await database.PermissionDefinitions.SingleOrDefaultAsync(item => item.Code == code);
				if (permission is null)
				{
					permission = new Domain.PermissionDefinition { Code = code, Name = code };
					database.PermissionDefinitions.Add(permission);
				}

				database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
				{
					RoleId = company.RoleId,
					PermissionId = permission.Id,
				});
			}

			await database.SaveChangesAsync();
		});

		return (user, company, periodId);
	}

	public static async Task SelectContextAsync(HttpClient client, Guid companyId, Guid periodId)
	{
		var response = await SendAsync(client, HttpMethod.Post, "/api/v1/context", new { companyId, fiscalPeriodId = periodId });
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	public static async Task<string> DetailAsync(HttpResponseMessage response)
	{
		var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
		return json.GetProperty("detail").GetString()!;
	}
}

/// <summary>Trade access gate through the real pipeline with the production deny-all entitlement.</summary>
public sealed class TradeAccessHttpTests : IClassFixture<TradeProbeFactory>
{
	private readonly TradeProbeFactory factory;

	public TradeAccessHttpTests(TradeProbeFactory factory) => this.factory = factory;

	private static readonly string ViewCode = TradePermissionCodes.FormView("probe");
	private static readonly string UpdateCode = TradePermissionCodes.FormUpdate("probe");

	[Fact]
	public async Task AnonymousGets401()
	{
		using var client = TradeHttp.Create(factory, out _);

		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/trade/capabilities", TestContext.Current.CancellationToken)).StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/trade/_probe/read", TestContext.Current.CancellationToken)).StatusCode);
	}

	[Fact]
	public async Task SignedInWithoutContextGets409ContextRequired()
	{
		var (user, _, _) = await TradeHttp.SeedAsync(factory, permissionCodes: [ViewCode]);
		using var client = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(client, user);

		foreach (var path in new[] { "/api/v1/trade/capabilities", "/api/v1/trade/_probe/read" })
		{
			var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
			Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
			Assert.Equal("trade_context_required", await TradeHttp.DetailAsync(response));
		}
	}

	[Fact]
	public async Task TamperedWorkContextCookieGets409()
	{
		var (user, company, periodId) = await TradeHttp.SeedAsync(factory, permissionCodes: [ViewCode]);
		using var client = TradeHttp.Create(factory, out var jar);
		await Browser.SignInAsync(client, user);
		await TradeHttp.SelectContextAsync(client, company.Id, periodId);
		Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/trade/_probe/read", TestContext.Current.CancellationToken)).StatusCode);

		var contextCookie = Assert.Single(jar.Cookies.Keys, name => name.Contains("Context", StringComparison.OrdinalIgnoreCase));
		jar.Cookies[contextCookie] = jar.Cookies[contextCookie][..^4] + "AAAA";

		var response = await client.GetAsync("/api/v1/trade/_probe/read", TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
		Assert.Equal("trade_context_required", await TradeHttp.DetailAsync(response));
	}

	[Fact]
	public async Task RevokedMembershipAfterSelectionGets409()
	{
		var (user, company, periodId) = await TradeHttp.SeedAsync(factory, permissionCodes: [ViewCode]);
		using var client = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(client, user);
		await TradeHttp.SelectContextAsync(client, company.Id, periodId);

		await factory.WithDatabaseAsync(async database =>
		{
			var membership = await database.UserCompanyMemberships.SingleAsync(item => item.UserId == user.Id);
			membership.IsActive = false;
			await database.SaveChangesAsync();
		});

		var response = await client.GetAsync("/api/v1/trade/capabilities", TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
		Assert.Equal("trade_context_required", await TradeHttp.DetailAsync(response));
	}

	[Fact]
	public async Task PeriodRemovedAfterSelectionGets409()
	{
		var (user, company, periodId) = await TradeHttp.SeedAsync(factory, permissionCodes: [ViewCode]);
		using var client = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(client, user);
		await TradeHttp.SelectContextAsync(client, company.Id, periodId);

		await factory.WithDatabaseAsync(async database =>
		{
			var period = await database.FiscalPeriods.SingleAsync(item => item.Id == periodId);
			database.FiscalPeriods.Remove(period);
			await database.SaveChangesAsync();
		});

		var response = await client.GetAsync("/api/v1/trade/capabilities", TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
	}

	[Fact]
	public async Task ClosedPeriodWriteGets409ReadOnly()
	{
		var (user, company, periodId) = await TradeHttp.SeedAsync(
			factory, Domain.FiscalPeriodStatus.Closed, UpdateCode);
		using var client = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(client, user);
		await TradeHttp.SelectContextAsync(client, company.Id, periodId);

		var response = await TradeHttp.SendAsync(client, HttpMethod.Post, "/api/v1/trade/_probe/write");

		Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
		Assert.Equal("trade_period_read_only", await TradeHttp.DetailAsync(response));
	}

	[Fact]
	public async Task WriteWithoutAntiforgeryTokenGets400()
	{
		var (user, company, periodId) = await TradeHttp.SeedAsync(factory, permissionCodes: [UpdateCode]);
		using var client = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(client, user);
		await TradeHttp.SelectContextAsync(client, company.Id, periodId);

		var response = await client.PostAsync("/api/v1/trade/_probe/write", null, TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task ProductionDenyAllEntitlementGets403ModuleUnavailableEvenWithPermission()
	{
		var (user, company, periodId) = await TradeHttp.SeedAsync(factory, permissionCodes: [ViewCode, UpdateCode]);
		using var client = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(client, user);
		await TradeHttp.SelectContextAsync(client, company.Id, periodId);

		var read = await client.GetAsync("/api/v1/trade/_probe/read", TestContext.Current.CancellationToken);
		var write = await TradeHttp.SendAsync(client, HttpMethod.Post, "/api/v1/trade/_probe/write");

		Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
		Assert.Equal("trade_module_unavailable", await TradeHttp.DetailAsync(read));
		Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
		Assert.Equal("trade_module_unavailable", await TradeHttp.DetailAsync(write));
	}

	[Fact]
	public async Task StateChangingVerbDeclaredAsReadFailsClosedEvenWithAntiforgeryToken()
	{
		var (user, company, periodId) = await TradeHttp.SeedAsync(factory, permissionCodes: [ViewCode]);
		using var client = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(client, user);
		await TradeHttp.SelectContextAsync(client, company.Id, periodId);

		var response = await TradeHttp.SendAsync(client, HttpMethod.Post, "/api/v1/trade/_probe/post-declared-read");

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	[Fact]
	public async Task UnannotatedTradeEndpointFailsClosed()
	{
		var (user, company, periodId) = await TradeHttp.SeedAsync(factory, permissionCodes: [ViewCode]);
		using var client = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(client, user);
		await TradeHttp.SelectContextAsync(client, company.Id, periodId);

		var response = await client.GetAsync("/api/v1/trade/_probe/unannotated", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	[Fact]
	public async Task CapabilitiesWithProductionDiReportUnavailableAndNoPermissions()
	{
		var (user, company, periodId) = await TradeHttp.SeedAsync(factory, permissionCodes: [ViewCode]);
		using var client = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(client, user);
		await TradeHttp.SelectContextAsync(client, company.Id, periodId);

		var response = await client.GetAsync("/api/v1/trade/capabilities", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		var json = JsonDocument.Parse(body).RootElement;
		Assert.False(json.GetProperty("available").GetBoolean());
		Assert.Equal("ReadWrite", json.GetProperty("accessMode").GetString());
		Assert.Equal(0, json.GetProperty("permissions").GetArrayLength());
		TradeAccessHttpAssertions.NoLeak(body, company.Id);
	}
}

/// <summary>The same pipeline with a test-only granting entitlement.</summary>
public sealed class TradeAccessGrantedHttpTests : IClassFixture<GrantingTradeProbeFactory>
{
	private readonly GrantingTradeProbeFactory factory;

	public TradeAccessGrantedHttpTests(GrantingTradeProbeFactory factory) => this.factory = factory;

	private static readonly string ViewCode = TradePermissionCodes.FormView("probe");
	private static readonly string UpdateCode = TradePermissionCodes.FormUpdate("probe");

	[Fact]
	public async Task EntitledUserWithPermissionIsAllowedReadAndWrite()
	{
		var (user, company, periodId) = await TradeHttp.SeedAsync(factory, permissionCodes: [ViewCode, UpdateCode]);
		using var client = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(client, user);
		await TradeHttp.SelectContextAsync(client, company.Id, periodId);

		Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/trade/_probe/read", TestContext.Current.CancellationToken)).StatusCode);
		Assert.Equal(HttpStatusCode.OK,
			(await TradeHttp.SendAsync(client, HttpMethod.Post, "/api/v1/trade/_probe/write")).StatusCode);
	}

	[Fact]
	public async Task EntitledUserWithoutPermissionGets403Forbidden()
	{
		var (user, company, periodId) = await TradeHttp.SeedAsync(factory, permissionCodes: [ViewCode]);
		using var client = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(client, user);
		await TradeHttp.SelectContextAsync(client, company.Id, periodId);

		var response = await TradeHttp.SendAsync(client, HttpMethod.Post, "/api/v1/trade/_probe/write");

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
		Assert.Equal("trade_forbidden", await TradeHttp.DetailAsync(response));
	}

	[Fact]
	public async Task CapabilitiesListOnlyTradePermissionsWhenEntitled()
	{
		var (user, company, periodId) = await TradeHttp.SeedAsync(
			factory, permissionCodes: [ViewCode, UpdateCode, "accounting.vouchers.view"]);
		using var client = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(client, user);
		await TradeHttp.SelectContextAsync(client, company.Id, periodId);

		var response = await client.GetAsync("/api/v1/trade/capabilities", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		var json = JsonDocument.Parse(body).RootElement;
		Assert.True(json.GetProperty("available").GetBoolean());
		var permissions = json.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()).ToArray();
		Assert.Equal(new[] { UpdateCode, ViewCode }.OrderBy(code => code, StringComparer.Ordinal), permissions);
		Assert.All(permissions, code => Assert.StartsWith("trade.", code));
		TradeAccessHttpAssertions.NoLeak(body, company.Id);
	}

	[Fact]
	public async Task CapabilitiesInClosedPeriodReportReadOnly()
	{
		var (user, company, periodId) = await TradeHttp.SeedAsync(
			factory, Domain.FiscalPeriodStatus.Closed, ViewCode);
		using var client = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(client, user);
		await TradeHttp.SelectContextAsync(client, company.Id, periodId);

		var json = await client.GetFromJsonAsync<JsonElement>("/api/v1/trade/capabilities", TestContext.Current.CancellationToken);

		Assert.Equal("ReadOnly", json.GetProperty("accessMode").GetString());
	}
}

internal static class TradeAccessHttpAssertions
{
	public static void NoLeak(string body, Guid tenantId)
	{
		foreach (var forbidden in new[] { "license", "signature", "expires", "TRADE_IMPORT" })
		{
			Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
		}

		Assert.DoesNotContain(tenantId.ToString(), body, StringComparison.OrdinalIgnoreCase);
	}
}
