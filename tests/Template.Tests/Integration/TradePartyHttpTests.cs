using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Faranou.Modules.Trade.Application.Authorization;
using Faranou.Modules.Trade.Domain.Parties;
using Faranou.Modules.Trade.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Template.Tests.Integration;

internal static class TradePartyTestSupport
{
	public static readonly string View = TradePermissionCodes.FormView("party");
	public static readonly string Create = TradePermissionCodes.FormCreate("party");
	public static readonly string Update = TradePermissionCodes.FormUpdate("party");
	public const string Base = "/api/v1/trade/parties";

	/// <summary>Skips (or, with FARANOU_REQUIRE_SQLSERVER=1, fails) when no SQL Server is configured.</summary>
	public static bool SqlServerAvailable()
	{
		if (IntegrationDatabase.UsesSqlServer)
		{
			return true;
		}

		if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
		{
			throw new InvalidOperationException("FARANOU_REQUIRE_SQLSERVER=1 but FARANOU_TEST_SQLSERVER is not set.");
		}

		return false;
	}

	public static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
		await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

	public static async Task<string> BodyAsync(HttpResponseMessage response) =>
		await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

	public static string[] Roles(JsonElement party) =>
		party.GetProperty("roles").EnumerateArray().Select(item => item.GetString()!).ToArray();

	public static void AssertNoStore(HttpResponseMessage response)
	{
		Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
		Assert.Contains("no-cache", string.Join(",", response.Headers.Pragma.Select(item => item.Name)), StringComparison.Ordinal);
	}

	public static object Body(string code, string[]? roles = null, string? legalIdentifier = null, string? displayName = null,
		string? countryCode = null, string legalName = "Legal Name", Guid? tenantId = null) => new
		{
			code,
			legalName,
			displayName,
			legalIdentifier,
			countryCode,
			roles = roles ?? ["Importer"],
			tenantId,
		};

	public static async Task WithTradeAsync(FaranouApplicationFactory factory, Func<TradeDbContext, Task> action)
	{
		using var scope = factory.Services.CreateScope();
		await action(scope.ServiceProvider.GetRequiredService<TradeDbContext>());
	}

	public static async Task<Guid> SeedPartyAsync(
		FaranouApplicationFactory factory, Guid tenantId, string code, string legalName = "Seeded",
		string? legalIdentifier = null, string? displayName = null, PartyRole[]? roles = null,
		bool deleted = false, bool inactive = false)
	{
		Guid id = Guid.Empty;
		await WithTradeAsync(factory, async database =>
		{
			var now = DateTime.UtcNow;
			var party = new Party(tenantId, code, legalName, Guid.NewGuid(), now, legalIdentifier, null, displayName);
			foreach (var role in roles ?? [])
			{
				party.AddRole(role, Guid.NewGuid(), now);
			}

			if (inactive)
			{
				party.SetActive(false, Guid.NewGuid(), now);
			}

			database.Parties.Add(party);
			await database.SaveChangesAsync();
			id = party.Id;

			if (deleted)
			{
				party.SoftDelete(Guid.NewGuid(), now);
				await database.SaveChangesAsync();
			}
		});
		return id;
	}
}

/// <summary>Party endpoints behind the real gate with a granting entitlement double.</summary>
[Collection(TradeHttp.CollectionName)]
public sealed class TradePartyHttpTests : IClassFixture<GrantingTradeProbeFactory>
{
	private readonly GrantingTradeProbeFactory factory;

	public TradePartyHttpTests(GrantingTradeProbeFactory factory) => this.factory = factory;

	private static string Base => TradePartyTestSupport.Base;

	private async Task<(HttpClient Client, Guid CompanyId, Guid PeriodId)> SignedInAsync(
		Domain.FiscalPeriodStatus status = Domain.FiscalPeriodStatus.Open, params string[] permissions)
	{
		var (user, company, periodId) = await TradeHttp.SeedAsync(factory, status, permissions);
		var client = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(client, user);
		await TradeHttp.SelectContextAsync(client, company.Id, periodId);
		return (client, company.Id, periodId);
	}

	private Task<(HttpClient Client, Guid CompanyId, Guid PeriodId)> FullAsync() =>
		SignedInAsync(Domain.FiscalPeriodStatus.Open,
			TradePartyTestSupport.View, TradePartyTestSupport.Create, TradePartyTestSupport.Update);

	private static async Task<JsonElement> CreateOkAsync(HttpClient client, object body)
	{
		var response = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, body);
		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		return await TradePartyTestSupport.JsonAsync(response);
	}

	private static object UpdateBody(JsonElement current, string? code = null, string[]? roles = null,
		string? rowVersion = null, string? legalIdentifier = null) => new
		{
			code = code ?? current.GetProperty("code").GetString(),
			legalName = current.GetProperty("legalName").GetString(),
			legalIdentifier,
			roles = roles ?? TradePartyTestSupport.Roles(current),
			rowVersion = rowVersion ?? current.GetProperty("rowVersion").GetString(),
		};

	// ---- gate behaviour (works with both InMemory and SQL Server: no Trade data is touched) ----

	[Fact]
	public async Task AnonymousGets401OnEveryPartyRoute()
	{
		using var client = TradeHttp.Create(factory, out _);
		var id = Guid.NewGuid();

		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Base, TestContext.Current.CancellationToken)).StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{Base}/{id}", TestContext.Current.CancellationToken)).StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Base, TradePartyTestSupport.Body("A"), TestContext.Current.CancellationToken)).StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync($"{Base}/{id}", TradePartyTestSupport.Body("A"), TestContext.Current.CancellationToken)).StatusCode);
	}

	[Fact]
	public async Task MissingPermissionGets403TradeForbidden()
	{
		var (client, _, _) = await SignedInAsync(Domain.FiscalPeriodStatus.Open, TradePartyTestSupport.View);
		var id = Guid.NewGuid();

		var post = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, TradePartyTestSupport.Body("A"));
		var put = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{id}", TradePartyTestSupport.Body("A"));

		Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
		Assert.Equal("trade_forbidden", await TradeHttp.DetailAsync(post));
		Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
		Assert.Equal("trade_forbidden", await TradeHttp.DetailAsync(put));

		var (noView, _, _) = await SignedInAsync(Domain.FiscalPeriodStatus.Open, TradePartyTestSupport.Create);
		var list = await noView.GetAsync(Base, TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
		Assert.Equal("trade_forbidden", await TradeHttp.DetailAsync(list));
		TradePartyTestSupport.AssertNoStore(list);
	}

	[Fact]
	public async Task WritesWithoutAntiforgeryTokenGet400FromTheAntiforgeryCheckNotFromValidation()
	{
		var (client, _, _) = await FullAsync();
		var validPost = TradePartyTestSupport.Body("AF-1");
		var validPut = new
		{
			code = "AF-1",
			legalName = "Legal Name",
			roles = new[] { "Importer" },
			rowVersion = Convert.ToBase64String(new byte[8]),
		};

		var post = await client.PostAsJsonAsync(Base, validPost, TestContext.Current.CancellationToken);
		var put = await client.PutAsJsonAsync($"{Base}/{Guid.NewGuid()}", validPut, TestContext.Current.CancellationToken);

		foreach (var response in new[] { post, put })
		{
			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			var json = await TradePartyTestSupport.JsonAsync(response);
			Assert.Contains("درخواست نامعتبر", json.GetProperty("title").GetString(), StringComparison.Ordinal);
			Assert.False(json.TryGetProperty("errors", out _));
		}
	}

	[Fact]
	public async Task CountriesReferenceNeedsViewPermissionAndIsReturnedOrderedWithPersianNames()
	{
		var (client, _, _) = await FullAsync();

		var response = await client.GetAsync("/api/v1/trade/reference/countries", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		TradePartyTestSupport.AssertNoStore(response);
		var items = (await TradePartyTestSupport.JsonAsync(response)).EnumerateArray().ToArray();
		Assert.True(items.Length >= 249, $"Only {items.Length} countries.");
		var codes = items.Select(item => item.GetProperty("code").GetString()!).ToArray();
		Assert.Equal(codes.Order(StringComparer.Ordinal), codes);
		var iran = Assert.Single(items, item => item.GetProperty("code").GetString() == "IR");
		Assert.False(string.IsNullOrWhiteSpace(iran.GetProperty("nameFa").GetString()));
		Assert.Contains(iran.GetProperty("nameFa").GetString()!, character => character >= '\u0600' && character <= '\u06FF');
		Assert.False(string.IsNullOrWhiteSpace(iran.GetProperty("nameEn").GetString()));
	}

	[Fact]
	public async Task CountriesReferenceWithoutViewPermissionGets403AndAnonymousGets401()
	{
		var (client, _, _) = await SignedInAsync(Domain.FiscalPeriodStatus.Open, TradePartyTestSupport.Create);
		var forbidden = await client.GetAsync("/api/v1/trade/reference/countries", TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
		Assert.Equal("trade_forbidden", await TradeHttp.DetailAsync(forbidden));

		using var anonymous = TradeHttp.Create(factory, out _);
		var response = await anonymous.GetAsync("/api/v1/trade/reference/countries", TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task ClosedPeriodPartyWritesAreAllowedBecausePartyIsTenantLevelMasterData()
	{
		Assert.SkipUnless(TradePartyTestSupport.SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		var (client, companyId, _) = await SignedInAsync(
			Domain.FiscalPeriodStatus.Closed, TradePartyTestSupport.View, TradePartyTestSupport.Create, TradePartyTestSupport.Update);

		var created = await CreateOkAsync(client, TradePartyTestSupport.Body("CLOSED-1", ["Seller"]));
		var id = created.GetProperty("id").GetGuid();
		var put = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{id}", UpdateBody(created, roles: ["Seller", "Carrier"]));
		var read = await client.GetAsync($"{Base}/{id}", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.OK, put.StatusCode);
		Assert.Equal(["Carrier", "Seller"], TradePartyTestSupport.Roles(await TradePartyTestSupport.JsonAsync(put)));
		Assert.Equal(HttpStatusCode.OK, read.StatusCode);
		await TradePartyTestSupport.WithTradeAsync(factory, async database =>
			Assert.Equal(companyId, (await database.Parties.AsNoTracking().SingleAsync(item => item.Id == id)).TenantId));
	}

	[Fact]
	public async Task ClosedPeriodPartyWritesStillNeedPermissionsAndContext()
	{
		var (client, _, _) = await SignedInAsync(Domain.FiscalPeriodStatus.Closed, TradePartyTestSupport.View);
		var id = Guid.NewGuid();

		var post = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, TradePartyTestSupport.Body("A"));
		var put = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{id}", TradePartyTestSupport.Body("A"));
		Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
		Assert.Equal("trade_forbidden", await TradeHttp.DetailAsync(post));
		Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
		Assert.Equal("trade_forbidden", await TradeHttp.DetailAsync(put));

		var (user, _, _) = await TradeHttp.SeedAsync(factory, Domain.FiscalPeriodStatus.Closed,
			TradePartyTestSupport.View, TradePartyTestSupport.Create, TradePartyTestSupport.Update);
		using var noContext = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(noContext, user);
		var withoutContext = await TradeHttp.SendAsync(noContext, HttpMethod.Post, Base, TradePartyTestSupport.Body("A"));
		Assert.Equal(HttpStatusCode.Conflict, withoutContext.StatusCode);
		Assert.Equal("trade_context_required", await TradeHttp.DetailAsync(withoutContext));
	}

	[Fact]
	public async Task CapabilitiesInAClosedPeriodStayReadOnlyWhilePartyPermissionsAreListed()
	{
		var (client, _, _) = await SignedInAsync(
			Domain.FiscalPeriodStatus.Closed, TradePartyTestSupport.View, TradePartyTestSupport.Create, TradePartyTestSupport.Update);

		var json = await client.GetFromJsonAsync<JsonElement>("/api/v1/trade/capabilities", TestContext.Current.CancellationToken);

		Assert.Equal("ReadOnly", json.GetProperty("accessMode").GetString());
		var permissions = json.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()).ToArray();
		Assert.Contains(TradePartyTestSupport.View, permissions);
		Assert.Contains(TradePartyTestSupport.Create, permissions);
		Assert.Contains(TradePartyTestSupport.Update, permissions);
	}

	[Fact]
	public async Task UpdateOrCreateWithoutViewGetsTheSame403ForExistingAndMissingIdsAndNoPartyData()
	{
		var (client, companyId, _) = await SignedInAsync(Domain.FiscalPeriodStatus.Open, TradePartyTestSupport.Update, TradePartyTestSupport.Create);
		var missing = Guid.NewGuid();
		var bodies = new List<string>();
		var targets = new List<Guid> { missing };
		if (TradePartyTestSupport.SqlServerAvailable())
		{
			targets.Add(await TradePartyTestSupport.SeedPartyAsync(factory, companyId, "NOVIEW-1", "Secret Legal Name", legalIdentifier: "SECRET-LEGAL-ID"));
		}

		foreach (var target in targets)
		{
			var put = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{target}", new
			{
				code = "NOVIEW-1",
				legalName = "x",
				roles = new[] { "Importer" },
				rowVersion = Convert.ToBase64String(new byte[8]),
			});
			Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
			Assert.Equal("trade_forbidden", await TradeHttp.DetailAsync(put));
			bodies.Add(await TradePartyTestSupport.BodyAsync(put));
		}

		var post = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, TradePartyTestSupport.Body("NOVIEW-1"));
		Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
		Assert.Equal("trade_forbidden", await TradeHttp.DetailAsync(post));
		bodies.Add(await TradePartyTestSupport.BodyAsync(post));

		Assert.All(bodies, body =>
		{
			Assert.DoesNotContain("SECRET", body, StringComparison.OrdinalIgnoreCase);
			Assert.DoesNotContain("legalName", body, StringComparison.Ordinal);
			Assert.DoesNotContain("rowVersion", body, StringComparison.Ordinal);
		});
		Assert.Single(bodies.Distinct());
	}

	[Fact]
	public async Task UpdateWithViewAndUpdatePermissionsSucceeds()
	{
		Assert.SkipUnless(TradePartyTestSupport.SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		var (client, companyId, _) = await FullAsync();
		var id = await TradePartyTestSupport.SeedPartyAsync(factory, companyId, "BOTH-1", roles: [PartyRole.Importer]);
		var detail = await TradePartyTestSupport.JsonAsync(await client.GetAsync($"{Base}/{id}", TestContext.Current.CancellationToken));

		var put = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{id}", UpdateBody(detail));

		Assert.Equal(HttpStatusCode.OK, put.StatusCode);
	}

	// ---- request body size limit (enforced by hand, so it is real on TestServer too) ----

	private sealed class UnknownLengthContent(byte[] payload) : HttpContent
	{
		protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
			stream.WriteAsync(payload, 0, payload.Length);

		protected override bool TryComputeLength(out long length)
		{
			length = 0;
			return false;
		}
	}

	private static async Task<HttpResponseMessage> SendRawAsync(
		HttpClient client, HttpMethod method, string path, HttpContent content, string contentType = "application/json")
	{
		var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/antiforgery/token", TestContext.Current.CancellationToken);
		using var request = new HttpRequestMessage(method, path) { Content = content };
		request.Headers.Add(token.GetProperty("headerName").GetString()!, token.GetProperty("requestToken").GetString());
		content.Headers.ContentType = contentType.Length == 0 ? null : System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
		return await client.SendAsync(request, TestContext.Current.CancellationToken);
	}

	private static byte[] JsonBytesOfLength(int total, string code = "")
	{
		// {"code":"<code>","legalName":"L","roles":["Importer"],"padding":"ppp..."}
		var prefix = $"{{\"code\":\"{code}\",\"legalName\":\"L\",\"roles\":[\"Importer\"],\"padding\":\"";
		const string suffix = "\"}";
		var padding = new string('p', total - prefix.Length - suffix.Length);
		var bytes = System.Text.Encoding.UTF8.GetBytes(prefix + padding + suffix);
		Assert.Equal(total, bytes.Length);
		return bytes;
	}

	[Fact]
	public async Task BodyWithinTheLimitIsParsedAndValidatedWhileOversizedBodiesGet413BeforeAnythingElse()
	{
		var (client, _, _) = await FullAsync();
		const int max = 16 * 1024;
		var id = Guid.NewGuid();

		// (a) exactly at the limit with an empty code: parsed, then rejected by validation (400 with field errors), not 413.
		foreach (var (method, path) in new[] { (HttpMethod.Post, Base), (HttpMethod.Put, $"{Base}/{id}") })
		{
			var atLimit = await SendRawAsync(client, method, path, new ByteArrayContent(JsonBytesOfLength(max)));
			Assert.Equal(HttpStatusCode.BadRequest, atLimit.StatusCode);
			Assert.True((await TradePartyTestSupport.JsonAsync(atLimit)).TryGetProperty("errors", out _));

			// (b) one byte over, declared Content-Length: 413 with the machine code.
			var over = await SendRawAsync(client, method, path, new ByteArrayContent(JsonBytesOfLength(max + 1)));
			Assert.Equal(HttpStatusCode.RequestEntityTooLarge, over.StatusCode);
			Assert.Equal("trade_request_too_large", await TradeHttp.DetailAsync(over));
			TradePartyTestSupport.AssertNoStore(over);

			// chunked (no Content-Length): within the limit is parsed, over the limit is 413.
			var chunkedOk = await SendRawAsync(client, method, path, new UnknownLengthContent(JsonBytesOfLength(max)));
			Assert.Equal(HttpStatusCode.BadRequest, chunkedOk.StatusCode);
			Assert.True((await TradePartyTestSupport.JsonAsync(chunkedOk)).TryGetProperty("errors", out _));
			var chunkedOver = await SendRawAsync(client, method, path, new UnknownLengthContent(JsonBytesOfLength(max + 1)));
			Assert.Equal(HttpStatusCode.RequestEntityTooLarge, chunkedOver.StatusCode);

			// (c) oversized and not even JSON: still 413 (size is checked before parsing/validation/service).
			var garbage = new byte[max + 100];
			Array.Fill(garbage, (byte)'{');
			var oversizedInvalid = await SendRawAsync(client, method, path, new ByteArrayContent(garbage));
			Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversizedInvalid.StatusCode);
			var chunkedInvalid = await SendRawAsync(client, method, path, new UnknownLengthContent(garbage));
			Assert.Equal(HttpStatusCode.RequestEntityTooLarge, chunkedInvalid.StatusCode);

			// A small body that is not valid JSON is a plain 400 (not 413, not 500).
			var small = await SendRawAsync(client, method, path, new ByteArrayContent("{not json"u8.ToArray()));
			Assert.Equal(HttpStatusCode.BadRequest, small.StatusCode);
			Assert.Equal("trade_invalid_request_body", await TradeHttp.DetailAsync(small));
		}
	}

	[Fact]
	public async Task NonJsonContentTypeGets415BeforeAntiforgeryCanReadAFormBody()
	{
		var (client, _, _) = await FullAsync();
		var id = Guid.NewGuid();
		var fields = new Dictionary<string, string> { ["code"] = "A", ["__RequestVerificationToken"] = "x" };

		foreach (var (method, path) in new[] { (HttpMethod.Post, Base), (HttpMethod.Put, $"{Base}/{id}") })
		{
			// No anti-forgery header at all: the media type decides first (415), not the token (400).
			using var form = new FormUrlEncodedContent(fields);
			using var multipart = new MultipartFormDataContent { { new StringContent("A"), "code" } };
			using var plain = new StringContent("{}", System.Text.Encoding.UTF8, "text/plain");
			using var noType = new ByteArrayContent("{}"u8.ToArray());
			foreach (var content in new HttpContent[] { form, multipart, plain, noType })
			{
				using var request = new HttpRequestMessage(method, path) { Content = content };
				var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
				Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
				Assert.Equal("trade_unsupported_media_type", await TradeHttp.DetailAsync(response));
				TradePartyTestSupport.AssertNoStore(response);
			}
		}
	}

	[Fact]
	public async Task JsonContentTypeWithCharsetPassesAndAnEmptyBodyWithoutContentTypeIsLeftToAntiforgery()
	{
		var (client, _, _) = await FullAsync();

		// application/json; charset=utf-8 reaches validation (400 with field errors), proving the media check accepted it.
		var charset = await SendRawAsync(client, HttpMethod.Post, Base,
			new ByteArrayContent("{\"code\":\"\",\"legalName\":\"L\",\"roles\":[\"Importer\"]}"u8.ToArray()), "application/json; charset=utf-8");
		Assert.Equal(HttpStatusCode.BadRequest, charset.StatusCode);
		Assert.True((await TradePartyTestSupport.JsonAsync(charset)).TryGetProperty("errors", out _));

		// No body and no Content-Type: nothing to parse, so the existing anti-forgery answer (400) stays.
		var empty = await client.PostAsync(Base, null, TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
		Assert.Contains("درخواست نامعتبر", (await TradePartyTestSupport.JsonAsync(empty)).GetProperty("title").GetString(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task EmptyJsonBodyGets400InvalidBody()
	{
		var (client, _, _) = await FullAsync();

		foreach (var (method, path) in new[] { (HttpMethod.Post, Base), (HttpMethod.Put, $"{Base}/{Guid.NewGuid()}") })
		{
			var response = await SendRawAsync(client, method, path, new ByteArrayContent([]));
			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			Assert.Equal("trade_invalid_request_body", await TradeHttp.DetailAsync(response));
		}
	}

	[Fact]
	public async Task OversizedBodyFromAnUnauthorisedCallerIsRejectedByTheGateNotByTheBodyReader()
	{
		var (client, _, _) = await SignedInAsync(Domain.FiscalPeriodStatus.Open, TradePartyTestSupport.View);

		var response = await SendRawAsync(client, HttpMethod.Post, Base, new ByteArrayContent(JsonBytesOfLength(20 * 1024)));

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	[Fact]
	public async Task OversizedCreateNeverPersistsAParty()
	{
		Assert.SkipUnless(TradePartyTestSupport.SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		var (client, companyId, _) = await FullAsync();

		var over = await SendRawAsync(client, HttpMethod.Post, Base, new ByteArrayContent(JsonBytesOfLength(16 * 1024 + 1, "BIG-1")));
		var ok = await SendRawAsync(client, HttpMethod.Post, Base, new ByteArrayContent(JsonBytesOfLength(16 * 1024, "BIG-2")));

		Assert.Equal(HttpStatusCode.RequestEntityTooLarge, over.StatusCode);
		Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
		await TradePartyTestSupport.WithTradeAsync(factory, async database =>
		{
			var codes = await database.Parties.AsNoTracking().Where(item => item.TenantId == companyId).Select(item => item.Code).ToListAsync();
			Assert.Equal(["BIG-2"], codes);
		});
	}

	[Fact]
	public async Task InvalidInputGets400WithCamelCaseKeysAndNoEchoedValues()
	{
		var (client, _, _) = await FullAsync();
		var tooLong = new string('x', 65);

		var post = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, new
		{
			code = tooLong,
			legalName = " ",
			countryCode = "ZZZ",
			roles = new[] { "1" },
		});
		var put = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{Guid.NewGuid()}", new
		{
			code = "A",
			legalName = "L",
			roles = new[] { "Importer" },
			rowVersion = "***",
		});
		var list = await client.GetAsync($"{Base}?search={new string('s', 101)}&role=nope", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
		var postBody = await TradePartyTestSupport.BodyAsync(post);
		var errors = JsonDocument.Parse(postBody).RootElement.GetProperty("errors");
		foreach (var key in new[] { "code", "legalName", "countryCode", "roles" })
		{
			Assert.True(errors.TryGetProperty(key, out _), $"missing {key}");
		}

		Assert.DoesNotContain(tooLong, postBody, StringComparison.Ordinal);
		Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
		Assert.True(JsonDocument.Parse(await TradePartyTestSupport.BodyAsync(put)).RootElement.GetProperty("errors").TryGetProperty("rowVersion", out _));
		Assert.Equal(HttpStatusCode.BadRequest, list.StatusCode);
		var listErrors = JsonDocument.Parse(await TradePartyTestSupport.BodyAsync(list)).RootElement.GetProperty("errors");
		Assert.True(listErrors.TryGetProperty("search", out _));
		Assert.True(listErrors.TryGetProperty("role", out _));
	}

	[Fact]
	public async Task NoDeleteActivateOrDeactivateRoutesExist()
	{
		var (client, _, _) = await FullAsync();
		var id = Guid.NewGuid();

		var delete = await TradeHttp.SendAsync(client, HttpMethod.Delete, $"{Base}/{id}");
		var activate = await TradeHttp.SendAsync(client, HttpMethod.Post, $"{Base}/{id}/activate");
		var deactivate = await TradeHttp.SendAsync(client, HttpMethod.Post, $"{Base}/{id}/deactivate");

		Assert.True(delete.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
		Assert.Equal(HttpStatusCode.NotFound, activate.StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, deactivate.StatusCode);
	}

	// ---- SQL Server behaviour ----

	[Fact]
	public async Task CreateThenDetailThenListRoundTripWithTenantFromSessionOnly()
	{
		Assert.SkipUnless(TradePartyTestSupport.SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		var (client, companyId, _) = await FullAsync();
		var foreignTenant = Guid.NewGuid();

		var created = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, TradePartyTestSupport.Body(
			"  P-100 ", ["Seller", "Importer"], legalIdentifier: "ID-SECRET-1", displayName: "Disp", countryCode: "tr", tenantId: foreignTenant));

		Assert.Equal(HttpStatusCode.Created, created.StatusCode);
		TradePartyTestSupport.AssertNoStore(created);
		var detail = await TradePartyTestSupport.JsonAsync(created);
		var id = detail.GetProperty("id").GetGuid();
		Assert.Equal($"{Base}/{id}", created.Headers.Location!.OriginalString);
		Assert.Equal("P-100", detail.GetProperty("code").GetString());
		Assert.Equal("TR", detail.GetProperty("countryCode").GetString());
		Assert.Equal(["Importer", "Seller"], TradePartyTestSupport.Roles(detail));
		Assert.Equal("ID-SECRET-1", detail.GetProperty("legalIdentifier").GetString());
		Assert.True(detail.GetProperty("isActive").GetBoolean());
		Assert.False(string.IsNullOrEmpty(detail.GetProperty("rowVersion").GetString()));
		Assert.False(detail.TryGetProperty("tenantId", out _));
		Assert.False(detail.TryGetProperty("createdByUserId", out _));

		await TradePartyTestSupport.WithTradeAsync(factory, async database =>
		{
			var row = await database.Parties.AsNoTracking().SingleAsync(item => item.Id == id);
			Assert.Equal(companyId, row.TenantId);
		});

		var read = await client.GetAsync($"{Base}/{id}", TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.OK, read.StatusCode);
		TradePartyTestSupport.AssertNoStore(read);
		Assert.Equal(detail.GetProperty("rowVersion").GetString(), (await TradePartyTestSupport.JsonAsync(read)).GetProperty("rowVersion").GetString());

		var listResponse = await client.GetAsync(Base, TestContext.Current.CancellationToken);
		var listBody = await TradePartyTestSupport.BodyAsync(listResponse);
		TradePartyTestSupport.AssertNoStore(listResponse);
		Assert.DoesNotContain("legalIdentifier", listBody, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("ID-SECRET-1", listBody, StringComparison.Ordinal);
		var list = JsonDocument.Parse(listBody).RootElement;
		Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
		Assert.Equal(1, list.GetProperty("page").GetInt32());
		Assert.Equal(20, list.GetProperty("pageSize").GetInt32());
		var item = Assert.Single(list.GetProperty("items").EnumerateArray());
		Assert.Equal(id, item.GetProperty("id").GetGuid());
		Assert.Equal(["Importer", "Seller"], TradePartyTestSupport.Roles(item));
	}

	[Fact]
	public async Task ListIsOrderedByCodeWithPagingAndTotalCount()
	{
		Assert.SkipUnless(TradePartyTestSupport.SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		var (client, companyId, _) = await FullAsync();
		foreach (var code in new[] { "C-3", "C-1", "C-5", "C-2", "C-4" })
		{
			await TradePartyTestSupport.SeedPartyAsync(factory, companyId, code, roles: [PartyRole.Importer]);
		}

		var second = await TradePartyTestSupport.JsonAsync(await client.GetAsync($"{Base}?pageSize=2&page=2", TestContext.Current.CancellationToken));
		Assert.Equal(["C-3", "C-4"], second.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("code").GetString()));
		Assert.Equal(5, second.GetProperty("totalCount").GetInt32());
		Assert.Equal(2, second.GetProperty("page").GetInt32());

		var clamped = await TradePartyTestSupport.JsonAsync(await client.GetAsync($"{Base}?pageSize=1000&page=0", TestContext.Current.CancellationToken));
		Assert.Equal(1, clamped.GetProperty("page").GetInt32());
		Assert.Equal(100, clamped.GetProperty("pageSize").GetInt32());
		Assert.Equal(5, clamped.GetProperty("items").GetArrayLength());

		var beyond = await TradePartyTestSupport.JsonAsync(await client.GetAsync($"{Base}?pageSize=2&page=9", TestContext.Current.CancellationToken));
		Assert.Equal(0, beyond.GetProperty("items").GetArrayLength());
		Assert.Equal(5, beyond.GetProperty("totalCount").GetInt32());
	}

	[Fact]
	public async Task SearchMatchesCodeNamesOnlyAndTreatsWildcardsLiterally()
	{
		Assert.SkipUnless(TradePartyTestSupport.SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		var (client, companyId, _) = await FullAsync();
		await TradePartyTestSupport.SeedPartyAsync(factory, companyId, "AAA-1", "Alpha Trading", legalIdentifier: "HIDDEN-ID-77", displayName: "Zeta");
		await TradePartyTestSupport.SeedPartyAsync(factory, companyId, "BBB_2", "Beta 100% Corp");
		await TradePartyTestSupport.SeedPartyAsync(factory, companyId, "CCC-3", "Gamma");

		async Task<string[]> Search(string text)
		{
			var json = await TradePartyTestSupport.JsonAsync(await client.GetAsync($"{Base}?search={Uri.EscapeDataString(text)}", TestContext.Current.CancellationToken));
			return json.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("code").GetString()!).ToArray();
		}

		Assert.Empty(await Search("HIDDEN"));
		Assert.Empty(await Search("77"));
		Assert.Equal(["AAA-1"], await Search("alpha"));
		Assert.Equal(["AAA-1"], await Search("zeta"));
		Assert.Equal(["CCC-3"], await Search("CCC"));
		Assert.Equal(["BBB_2"], await Search("_"));
		Assert.Equal(["BBB_2"], await Search("%"));
		Assert.Equal(["BBB_2"], await Search("100%"));
		Assert.Empty(await Search("[a-z]"));
		Assert.Equal(3, (await Search("  ")).Length);
	}

	[Fact]
	public async Task RoleAndIsActiveFiltersApply()
	{
		Assert.SkipUnless(TradePartyTestSupport.SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		var (client, companyId, _) = await FullAsync();
		await TradePartyTestSupport.SeedPartyAsync(factory, companyId, "R-1", roles: [PartyRole.Seller, PartyRole.Carrier]);
		await TradePartyTestSupport.SeedPartyAsync(factory, companyId, "R-2", roles: [PartyRole.Carrier], inactive: true);
		await TradePartyTestSupport.SeedPartyAsync(factory, companyId, "R-3", roles: [PartyRole.Importer]);

		async Task<string[]> Codes(string query)
		{
			var response = await client.GetAsync($"{Base}?{query}", TestContext.Current.CancellationToken);
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var json = await TradePartyTestSupport.JsonAsync(response);
			return json.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("code").GetString()!).ToArray();
		}

		Assert.Equal(["R-1", "R-2"], await Codes("role=Carrier"));
		Assert.Equal(["R-1"], await Codes("role=Seller"));
		Assert.Equal(["R-2"], await Codes("isActive=false"));
		Assert.Equal(["R-1", "R-3"], await Codes("isActive=true"));
		Assert.Equal(["R-1"], await Codes("role=Carrier&isActive=true"));
		Assert.Empty(await Codes("role=Bank"));

		var carrierTotal = await TradePartyTestSupport.JsonAsync(await client.GetAsync($"{Base}?role=Carrier&pageSize=1", TestContext.Current.CancellationToken));
		Assert.Equal(2, carrierTotal.GetProperty("totalCount").GetInt32());
		Assert.Equal(["Carrier", "Seller"], TradePartyTestSupport.Roles(carrierTotal.GetProperty("items")[0]));
	}

	[Fact]
	public async Task OtherTenantPartyIsInvisibleAndEveryAccessAnswersTheSameEmpty404()
	{
		Assert.SkipUnless(TradePartyTestSupport.SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		var (client, companyId, _) = await FullAsync();
		var otherCompany = await factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("other-")));
		var foreign = await TradePartyTestSupport.SeedPartyAsync(factory, otherCompany.Id, "FOREIGN-1", "Other tenant co", legalIdentifier: "OTHER-ID", roles: [PartyRole.Seller]);
		var own = await TradePartyTestSupport.SeedPartyAsync(factory, companyId, "OWN-1", roles: [PartyRole.Seller]);
		var deleted = await TradePartyTestSupport.SeedPartyAsync(factory, companyId, "GONE-1", deleted: true, roles: [PartyRole.Seller]);

		var list = await TradePartyTestSupport.JsonAsync(await client.GetAsync(Base, TestContext.Current.CancellationToken));
		Assert.Equal(["OWN-1"], list.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("code").GetString()));
		Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
		var filtered = await TradePartyTestSupport.JsonAsync(await client.GetAsync($"{Base}?role=Seller&search=FOREIGN", TestContext.Current.CancellationToken));
		Assert.Equal(0, filtered.GetProperty("totalCount").GetInt32());

		var ownDetail = await TradePartyTestSupport.JsonAsync(await client.GetAsync($"{Base}/{own}", TestContext.Current.CancellationToken));
		var putBody = new
		{
			code = "HIJACK",
			legalName = "Hijack",
			roles = new[] { "Importer" },
			rowVersion = ownDetail.GetProperty("rowVersion").GetString(),
			tenantId = otherCompany.Id,
		};

		var missing = Guid.NewGuid();
		var responses = new List<HttpResponseMessage>
		{
			await client.GetAsync($"{Base}/{foreign}", TestContext.Current.CancellationToken),
			await client.GetAsync($"{Base}/{missing}", TestContext.Current.CancellationToken),
			await client.GetAsync($"{Base}/{deleted}", TestContext.Current.CancellationToken),
			await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{foreign}", putBody),
			await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{missing}", putBody),
			await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{deleted}", putBody),
		};

		foreach (var response in responses)
		{
			Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
			Assert.Equal(string.Empty, await TradePartyTestSupport.BodyAsync(response));
			TradePartyTestSupport.AssertNoStore(response);
		}

		await TradePartyTestSupport.WithTradeAsync(factory, async database =>
		{
			var row = await database.Parties.AsNoTracking().SingleAsync(item => item.Id == foreign);
			Assert.Equal("FOREIGN-1", row.Code);
			Assert.Equal(otherCompany.Id, row.TenantId);
		});
	}

	[Fact]
	public async Task DuplicateActiveCodeConflictsCaseInsensitivelyButIsFreeInOtherTenantsAndAfterSoftDelete()
	{
		Assert.SkipUnless(TradePartyTestSupport.SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		var (client, companyId, _) = await FullAsync();
		var otherCompany = await factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("other-")));
		await TradePartyTestSupport.SeedPartyAsync(factory, otherCompany.Id, "DUP-1");
		var retired = await TradePartyTestSupport.SeedPartyAsync(factory, companyId, "OLD-1", deleted: true);
		Assert.NotEqual(Guid.Empty, retired);

		var first = await CreateOkAsync(client, TradePartyTestSupport.Body("DUP-1"));
		var same = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, TradePartyTestSupport.Body("DUP-1"));
		var caseOnly = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, TradePartyTestSupport.Body("dup-1"));
		var reuse = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, TradePartyTestSupport.Body("OLD-1"));

		Assert.NotEqual(Guid.Empty, first.GetProperty("id").GetGuid());
		foreach (var conflict in new[] { same, caseOnly })
		{
			Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
			var body = await TradePartyTestSupport.BodyAsync(conflict);
			Assert.Equal("trade_party_code_duplicate", JsonDocument.Parse(body).RootElement.GetProperty("detail").GetString());
			Assert.DoesNotContain("UX_Parties", body, StringComparison.Ordinal);
			Assert.DoesNotContain("duplicate key", body, StringComparison.OrdinalIgnoreCase);
		}

		Assert.Equal(HttpStatusCode.Created, reuse.StatusCode);

		var second = await CreateOkAsync(client, TradePartyTestSupport.Body("DUP-2"));
		var clash = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{second.GetProperty("id").GetGuid()}", UpdateBody(second, code: "dup-1"));
		Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
		Assert.Equal("trade_party_code_duplicate", await TradeHttp.DetailAsync(clash));
	}

	[Fact]
	public async Task TwoRacingCreatesOfTheSameCodeYieldOneCreatedAndOneConflict()
	{
		Assert.SkipUnless(TradePartyTestSupport.SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		var (client, _, _) = await FullAsync();

		var results = await Task.WhenAll(
			Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
				TradeHttp.SendAsync(client, HttpMethod.Post, Base, TradePartyTestSupport.Body("RACE-1")))));

		Assert.Equal(1, results.Count(response => response.StatusCode == HttpStatusCode.Created));
		var loser = Assert.Single(results, response => response.StatusCode == HttpStatusCode.Conflict);
		Assert.Equal("trade_party_code_duplicate", await TradeHttp.DetailAsync(loser));
	}

	[Fact]
	public async Task UpdateEnforcesRowVersionAndChangesItOnEveryAcceptedWrite()
	{
		Assert.SkipUnless(TradePartyTestSupport.SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		var (client, companyId, _) = await FullAsync();
		var created = await CreateOkAsync(client, TradePartyTestSupport.Body("U-1", ["Importer", "Seller"], legalIdentifier: "KEEP-ME"));
		var id = created.GetProperty("id").GetGuid();
		var v0 = created.GetProperty("rowVersion").GetString();

		// Identity change succeeds and moves the row version.
		var renamed = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{id}", new
		{
			code = "U-1",
			legalName = "Renamed",
			legalIdentifier = "NEW-ID",
			countryCode = "tr",
			roles = new[] { "Importer", "Seller" },
			rowVersion = v0,
			tenantId = Guid.NewGuid(),
		});
		Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
		var afterRename = await TradePartyTestSupport.JsonAsync(renamed);
		var v1 = afterRename.GetProperty("rowVersion").GetString();
		Assert.NotEqual(v0, v1);
		Assert.Equal("Renamed", afterRename.GetProperty("legalName").GetString());
		Assert.Equal("TR", afterRename.GetProperty("countryCode").GetString());
		Assert.True(afterRename.GetProperty("updatedAtUtc").ValueKind == JsonValueKind.String);

		// The stale token loses.
		var stale = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{id}", UpdateBody(afterRename, rowVersion: v0));
		Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
		var staleBody = await TradePartyTestSupport.BodyAsync(stale);
		Assert.Equal("trade_concurrency_conflict", JsonDocument.Parse(staleBody).RootElement.GetProperty("detail").GetString());
		Assert.DoesNotContain("NEW-ID", staleBody, StringComparison.Ordinal);

		// A no-op update still checks (and bumps) the token; a roles-only update bumps the party token too.
		var noOp = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{id}", UpdateBody(afterRename, legalIdentifier: "NEW-ID"));
		Assert.Equal(HttpStatusCode.OK, noOp.StatusCode);
		var afterNoOp = await TradePartyTestSupport.JsonAsync(noOp);
		var v2 = afterNoOp.GetProperty("rowVersion").GetString();
		Assert.NotEqual(v1, v2);

		var rolesOnly = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{id}",
			UpdateBody(afterNoOp, roles: ["Importer", "Carrier"], legalIdentifier: "NEW-ID"));
		Assert.Equal(HttpStatusCode.OK, rolesOnly.StatusCode);
		var afterRoles = await TradePartyTestSupport.JsonAsync(rolesOnly);
		Assert.NotEqual(v2, afterRoles.GetProperty("rowVersion").GetString());
		Assert.Equal(["Carrier", "Importer"], TradePartyTestSupport.Roles(afterRoles));

		// Remove then re-add a role in separate updates (unique active-role index must not block it).
		var removed = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{id}",
			UpdateBody(afterRoles, roles: ["Importer"], legalIdentifier: "NEW-ID"));
		Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
		var afterRemove = await TradePartyTestSupport.JsonAsync(removed);
		Assert.Equal(["Importer"], TradePartyTestSupport.Roles(afterRemove));
		var readded = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{id}",
			UpdateBody(afterRemove, roles: ["Importer", "Seller", "Carrier"], legalIdentifier: "NEW-ID"));
		Assert.Equal(HttpStatusCode.OK, readded.StatusCode);
		Assert.Equal(["Carrier", "Importer", "Seller"], TradePartyTestSupport.Roles(await TradePartyTestSupport.JsonAsync(readded)));

		// Persisted state: unchanged roles kept their rows, removed ones are soft-deleted, tenant untouched.
		await TradePartyTestSupport.WithTradeAsync(factory, async database =>
		{
			var row = await database.Parties.AsNoTracking().SingleAsync(item => item.Id == id);
			Assert.Equal(companyId, row.TenantId);
			var assignments = await database.PartyRoleAssignments.AsNoTracking().Where(item => item.PartyId == id).ToListAsync();
			Assert.Equal(1, assignments.Count(item => item.Role == PartyRole.Importer));
			Assert.Equal(2, assignments.Count(item => item.Role == PartyRole.Seller));
			Assert.Single(assignments, item => item.Role == PartyRole.Seller && item.IsDeleted);
		});
	}
}

/// <summary>The production deny-all entitlement keeps the Party API closed.</summary>
[Collection(TradeHttp.CollectionName)]
public sealed class TradePartyDenyAllHttpTests : IClassFixture<TradeProbeFactory>
{
	private readonly TradeProbeFactory factory;

	public TradePartyDenyAllHttpTests(TradeProbeFactory factory) => this.factory = factory;

	[Fact]
	public async Task PartyRoutesAre403ModuleUnavailableWithProductionDenyAll()
	{
		var (user, company, periodId) = await TradeHttp.SeedAsync(factory,
			permissionCodes: [TradePartyTestSupport.View, TradePartyTestSupport.Create, TradePartyTestSupport.Update]);
		using var client = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(client, user);
		await TradeHttp.SelectContextAsync(client, company.Id, periodId);
		var id = Guid.NewGuid();

		var responses = new[]
		{
			await client.GetAsync(TradePartyTestSupport.Base, TestContext.Current.CancellationToken),
			await client.GetAsync($"{TradePartyTestSupport.Base}/{id}", TestContext.Current.CancellationToken),
			await TradeHttp.SendAsync(client, HttpMethod.Post, TradePartyTestSupport.Base, TradePartyTestSupport.Body("A")),
			await TradeHttp.SendAsync(client, HttpMethod.Put, $"{TradePartyTestSupport.Base}/{id}", TradePartyTestSupport.Body("A")),
		};

		foreach (var response in responses)
		{
			Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
			Assert.Equal("trade_module_unavailable", await TradeHttp.DetailAsync(response));
		}
	}

	[Fact]
	public async Task ClosedPeriodPartyWritesAreStillModuleUnavailableWithProductionDenyAll()
	{
		var (user, company, periodId) = await TradeHttp.SeedAsync(factory, Domain.FiscalPeriodStatus.Closed,
			TradePartyTestSupport.View, TradePartyTestSupport.Create, TradePartyTestSupport.Update);
		using var client = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(client, user);
		await TradeHttp.SelectContextAsync(client, company.Id, periodId);

		var post = await TradeHttp.SendAsync(client, HttpMethod.Post, TradePartyTestSupport.Base, TradePartyTestSupport.Body("A"));
		var put = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{TradePartyTestSupport.Base}/{Guid.NewGuid()}", TradePartyTestSupport.Body("A"));

		foreach (var response in new[] { post, put })
		{
			Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
			Assert.Equal("trade_module_unavailable", await TradeHttp.DetailAsync(response));
		}
	}
}
