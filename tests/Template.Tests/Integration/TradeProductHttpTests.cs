using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Faranou.Modules.Trade.Application.Authorization;
using Faranou.Modules.Trade.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

internal static class TradeProductTestSupport
{
	public static readonly string View = TradePermissionCodes.FormView("product");
	public static readonly string Create = TradePermissionCodes.FormCreate("product");
	public static readonly string Update = TradePermissionCodes.FormUpdate("product");
	public const string Base = "/api/v1/trade/products";

	public static object Body(string code, string name = "Steel", string? group = null, string? hs = null,
		string unit = "TON", Guid? tenantId = null, bool? isActive = null) => new
		{
			code,
			name,
			productGroupCode = group,
			hsCode = hs,
			baseUnitCode = unit,
			tenantId,
			isActive,
		};

	public static async Task<Guid> SeedAsync(
		FaranouApplicationFactory factory, Guid tenantId, string code, string name = "Seeded", string? group = null,
		string? hs = null, string unit = "TON", bool deleted = false, bool inactive = false)
	{
		Guid id = Guid.Empty;
		await TradePartyTestSupport.WithTradeAsync(factory, async database =>
		{
			var now = DateTime.UtcNow;
			var product = new Product(tenantId, code, name, Guid.NewGuid(), now, group, hs, unit);
			if (inactive)
			{
				product.SetActive(false, Guid.NewGuid(), now);
			}

			database.Products.Add(product);
			await database.SaveChangesAsync();
			id = product.Id;

			if (deleted)
			{
				product.SoftDelete(Guid.NewGuid(), now);
				await database.SaveChangesAsync();
			}
		});
		return id;
	}
}

/// <summary>Product endpoints behind the real gate with a granting entitlement double.</summary>
[Collection(TradeHttp.CollectionName)]
public sealed class TradeProductHttpTests : IClassFixture<GrantingTradeProbeFactory>
{
	private readonly GrantingTradeProbeFactory factory;

	public TradeProductHttpTests(GrantingTradeProbeFactory factory) => this.factory = factory;

	private static string Base => TradeProductTestSupport.Base;

	private static void RequireSql() =>
		Assert.SkipUnless(TradePartyTestSupport.SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

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
			TradeProductTestSupport.View, TradeProductTestSupport.Create, TradeProductTestSupport.Update);

	private static async Task<JsonElement> CreateOkAsync(HttpClient client, object body)
	{
		var response = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, body);
		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		return await TradePartyTestSupport.JsonAsync(response);
	}

	private static object UpdateBody(JsonElement current, string? code = null, string? name = null, string? rowVersion = null,
		string? unit = null) => new
		{
			code = code ?? current.GetProperty("code").GetString(),
			name = name ?? current.GetProperty("name").GetString(),
			baseUnitCode = unit ?? current.GetProperty("baseUnitCode").GetString(),
			rowVersion = rowVersion ?? current.GetProperty("rowVersion").GetString(),
		};

	private static object MinimalPut(string? rowVersion = null) => new
	{
		code = "X-1",
		name = "x",
		baseUnitCode = "TON",
		rowVersion = rowVersion ?? Convert.ToBase64String(new byte[8]),
	};

	// ---- gate behaviour (no Trade data is touched) ----

	[Fact]
	public async Task AnonymousGets401OnEveryProductRouteAndUnitsReference()
	{
		using var client = TradeHttp.Create(factory, out _);
		var id = Guid.NewGuid();

		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Base, TestContext.Current.CancellationToken)).StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{Base}/{id}", TestContext.Current.CancellationToken)).StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/trade/reference/units", TestContext.Current.CancellationToken)).StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Base, TradeProductTestSupport.Body("A"), TestContext.Current.CancellationToken)).StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync($"{Base}/{id}", TradeProductTestSupport.Body("A"), TestContext.Current.CancellationToken)).StatusCode);
	}

	[Fact]
	public async Task MissingPermissionGets403TradeForbiddenOnEveryRoute()
	{
		var (viewOnly, _, _) = await SignedInAsync(Domain.FiscalPeriodStatus.Open, TradeProductTestSupport.View);
		var id = Guid.NewGuid();

		var post = await TradeHttp.SendAsync(viewOnly, HttpMethod.Post, Base, TradeProductTestSupport.Body("A"));
		var put = await TradeHttp.SendAsync(viewOnly, HttpMethod.Put, $"{Base}/{id}", MinimalPut());
		Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
		Assert.Equal("trade_forbidden", await TradeHttp.DetailAsync(post));
		Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
		Assert.Equal("trade_forbidden", await TradeHttp.DetailAsync(put));

		var (noView, _, _) = await SignedInAsync(Domain.FiscalPeriodStatus.Open, TradeProductTestSupport.Create, TradeProductTestSupport.Update);
		foreach (var path in new[] { Base, $"{Base}/{id}", "/api/v1/trade/reference/units" })
		{
			var response = await noView.GetAsync(path, TestContext.Current.CancellationToken);
			Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
			Assert.Equal("trade_forbidden", await TradeHttp.DetailAsync(response));
			TradePartyTestSupport.AssertNoStore(response);
		}

		// Party permissions grant nothing on products.
		var (partyOnly, _, _) = await SignedInAsync(Domain.FiscalPeriodStatus.Open,
			TradePartyTestSupport.View, TradePartyTestSupport.Create, TradePartyTestSupport.Update);
		Assert.Equal(HttpStatusCode.Forbidden, (await partyOnly.GetAsync(Base, TestContext.Current.CancellationToken)).StatusCode);
		Assert.Equal(HttpStatusCode.Forbidden, (await TradeHttp.SendAsync(partyOnly, HttpMethod.Post, Base, TradeProductTestSupport.Body("A"))).StatusCode);
	}

	[Fact]
	public async Task WritesWithoutAntiforgeryTokenGet400FromTheAntiforgeryCheck()
	{
		var (client, _, _) = await FullAsync();

		var post = await client.PostAsJsonAsync(Base, TradeProductTestSupport.Body("AF-1"), TestContext.Current.CancellationToken);
		var put = await client.PutAsJsonAsync($"{Base}/{Guid.NewGuid()}", MinimalPut(), TestContext.Current.CancellationToken);

		foreach (var response in new[] { post, put })
		{
			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			var json = await TradePartyTestSupport.JsonAsync(response);
			Assert.Contains("درخواست نامعتبر", json.GetProperty("title").GetString(), StringComparison.Ordinal);
			Assert.False(json.TryGetProperty("errors", out _));
		}
	}

	[Fact]
	public async Task UnitsReferenceIsOrderedLocalAndNeedsProductView()
	{
		var (client, _, _) = await FullAsync();

		var response = await client.GetAsync("/api/v1/trade/reference/units", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		TradePartyTestSupport.AssertNoStore(response);
		var items = (await TradePartyTestSupport.JsonAsync(response)).EnumerateArray().ToArray();
		Assert.True(items.Length >= 13);
		var codes = items.Select(item => item.GetProperty("code").GetString()!).ToArray();
		Assert.Equal(codes.Order(StringComparer.Ordinal), codes);
		var ton = Assert.Single(items, item => item.GetProperty("code").GetString() == "TON");
		Assert.Contains(ton.GetProperty("nameFa").GetString()!, character => character >= '؀' && character <= 'ۿ');
		Assert.False(string.IsNullOrWhiteSpace(ton.GetProperty("nameEn").GetString()));
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

	[Fact]
	public async Task UpdateOrCreateWithoutViewGetsTheSame403ForExistingAndMissingIdsAndNoProductData()
	{
		var (client, companyId, _) = await SignedInAsync(Domain.FiscalPeriodStatus.Open, TradeProductTestSupport.Update, TradeProductTestSupport.Create);
		var targets = new List<Guid> { Guid.NewGuid() };
		if (TradePartyTestSupport.SqlServerAvailable())
		{
			targets.Add(await TradeProductTestSupport.SeedAsync(factory, companyId, "NOVIEW-1", "Secret Product Name"));
		}

		var bodies = new List<string>();
		foreach (var target in targets)
		{
			var put = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{target}", MinimalPut());
			Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
			Assert.Equal("trade_forbidden", await TradeHttp.DetailAsync(put));
			bodies.Add(await TradePartyTestSupport.BodyAsync(put));
		}

		var post = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, TradeProductTestSupport.Body("NOVIEW-1"));
		Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
		Assert.Equal("trade_forbidden", await TradeHttp.DetailAsync(post));
		bodies.Add(await TradePartyTestSupport.BodyAsync(post));

		Assert.All(bodies, body =>
		{
			Assert.DoesNotContain("Secret", body, StringComparison.OrdinalIgnoreCase);
			Assert.DoesNotContain("rowVersion", body, StringComparison.Ordinal);
		});
		Assert.Single(bodies.Distinct());
	}

	[Fact]
	public async Task ClosedPeriodProductWritesStillNeedPermissionsAndContext()
	{
		var (client, _, _) = await SignedInAsync(Domain.FiscalPeriodStatus.Closed, TradeProductTestSupport.View);
		var post = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, TradeProductTestSupport.Body("A"));
		var put = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{Guid.NewGuid()}", MinimalPut());
		Assert.Equal("trade_forbidden", await TradeHttp.DetailAsync(post));
		Assert.Equal("trade_forbidden", await TradeHttp.DetailAsync(put));

		var (user, _, _) = await TradeHttp.SeedAsync(factory, Domain.FiscalPeriodStatus.Closed,
			TradeProductTestSupport.View, TradeProductTestSupport.Create, TradeProductTestSupport.Update);
		using var noContext = TradeHttp.Create(factory, out _);
		await Browser.SignInAsync(noContext, user);
		var withoutContext = await TradeHttp.SendAsync(noContext, HttpMethod.Post, Base, TradeProductTestSupport.Body("A"));
		Assert.Equal(HttpStatusCode.Conflict, withoutContext.StatusCode);
		Assert.Equal("trade_context_required", await TradeHttp.DetailAsync(withoutContext));
	}

	[Fact]
	public async Task ClosedPeriodProductWritesAreAllowedBecauseProductIsTenantLevelMasterData()
	{
		RequireSql();
		var (client, companyId, _) = await SignedInAsync(Domain.FiscalPeriodStatus.Closed,
			TradeProductTestSupport.View, TradeProductTestSupport.Create, TradeProductTestSupport.Update);

		var created = await CreateOkAsync(client, TradeProductTestSupport.Body("CLOSED-1"));
		var id = created.GetProperty("id").GetGuid();
		var put = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{id}", UpdateBody(created, name: "Renamed in closed period"));
		var read = await client.GetAsync($"{Base}/{id}", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.OK, put.StatusCode);
		Assert.Equal("Renamed in closed period", (await TradePartyTestSupport.JsonAsync(put)).GetProperty("name").GetString());
		Assert.Equal(HttpStatusCode.OK, read.StatusCode);
		await TradePartyTestSupport.WithTradeAsync(factory, async database =>
			Assert.Equal(companyId, (await database.Products.AsNoTracking().SingleAsync(item => item.Id == id)).TenantId));
	}

	[Fact]
	public async Task UpdateWithViewAndUpdatePermissionsSucceeds()
	{
		RequireSql();
		var (client, companyId, _) = await FullAsync();
		var id = await TradeProductTestSupport.SeedAsync(factory, companyId, "BOTH-1");
		var detail = await TradePartyTestSupport.JsonAsync(await client.GetAsync($"{Base}/{id}", TestContext.Current.CancellationToken));

		var put = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{id}", UpdateBody(detail));

		Assert.Equal(HttpStatusCode.OK, put.StatusCode);
	}

	// ---- request body size limit (real on TestServer: enforced by hand) ----

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
		content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
		return await client.SendAsync(request, TestContext.Current.CancellationToken);
	}

	private static byte[] JsonBytesOfLength(int total, string code = "")
	{
		var prefix = $"{{\"code\":\"{code}\",\"name\":\"N\",\"baseUnitCode\":\"TON\",\"padding\":\"";
		const string suffix = "\"}";
		var bytes = System.Text.Encoding.UTF8.GetBytes(prefix + new string('p', total - prefix.Length - suffix.Length) + suffix);
		Assert.Equal(total, bytes.Length);
		return bytes;
	}

	[Fact]
	public async Task BodyWithinTheLimitIsValidatedWhileOversizedBodiesGet413WithTheMachineCode()
	{
		var (client, _, _) = await FullAsync();
		const int max = 16 * 1024;

		foreach (var (method, path) in new[] { (HttpMethod.Post, Base), (HttpMethod.Put, $"{Base}/{Guid.NewGuid()}") })
		{
			var atLimit = await SendRawAsync(client, method, path, new ByteArrayContent(JsonBytesOfLength(max)));
			Assert.Equal(HttpStatusCode.BadRequest, atLimit.StatusCode);
			Assert.True((await TradePartyTestSupport.JsonAsync(atLimit)).TryGetProperty("errors", out _));

			var over = await SendRawAsync(client, method, path, new ByteArrayContent(JsonBytesOfLength(max + 1)));
			Assert.Equal(HttpStatusCode.RequestEntityTooLarge, over.StatusCode);
			Assert.Equal("trade_request_too_large", await TradeHttp.DetailAsync(over));
			TradePartyTestSupport.AssertNoStore(over);

			var chunkedOk = await SendRawAsync(client, method, path, new UnknownLengthContent(JsonBytesOfLength(max)));
			Assert.Equal(HttpStatusCode.BadRequest, chunkedOk.StatusCode);
			var chunkedOver = await SendRawAsync(client, method, path, new UnknownLengthContent(JsonBytesOfLength(max + 1)));
			Assert.Equal(HttpStatusCode.RequestEntityTooLarge, chunkedOver.StatusCode);

			var garbage = new byte[max + 100];
			Array.Fill(garbage, (byte)'{');
			Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await SendRawAsync(client, method, path, new ByteArrayContent(garbage))).StatusCode);
			Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await SendRawAsync(client, method, path, new UnknownLengthContent(garbage))).StatusCode);

			var small = await SendRawAsync(client, method, path, new ByteArrayContent("{not json"u8.ToArray()));
			Assert.Equal(HttpStatusCode.BadRequest, small.StatusCode);
			Assert.Equal("trade_invalid_request_body", await TradeHttp.DetailAsync(small));
		}
	}

	[Fact]
	public async Task NonJsonContentTypeGets415AndOversizedBodyFromUnauthorisedCallerGets403()
	{
		var (client, _, _) = await FullAsync();
		foreach (var (method, path) in new[] { (HttpMethod.Post, Base), (HttpMethod.Put, $"{Base}/{Guid.NewGuid()}") })
		{
			using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = "A" });
			using var plain = new StringContent("{}", System.Text.Encoding.UTF8, "text/plain");
			foreach (var content in new HttpContent[] { form, plain })
			{
				using var request = new HttpRequestMessage(method, path) { Content = content };
				var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
				Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
				Assert.Equal("trade_unsupported_media_type", await TradeHttp.DetailAsync(response));
			}
		}

		var (viewOnly, _, _) = await SignedInAsync(Domain.FiscalPeriodStatus.Open, TradeProductTestSupport.View);
		var big = await SendRawAsync(viewOnly, HttpMethod.Post, Base, new ByteArrayContent(JsonBytesOfLength(20 * 1024)));
		Assert.Equal(HttpStatusCode.Forbidden, big.StatusCode);
	}

	[Fact]
	public async Task OversizedCreateNeverPersistsAProduct()
	{
		RequireSql();
		var (client, companyId, _) = await FullAsync();

		var over = await SendRawAsync(client, HttpMethod.Post, Base, new ByteArrayContent(JsonBytesOfLength(16 * 1024 + 1, "BIG-1")));
		var ok = await SendRawAsync(client, HttpMethod.Post, Base, new ByteArrayContent(JsonBytesOfLength(16 * 1024, "BIG-2")));

		Assert.Equal(HttpStatusCode.RequestEntityTooLarge, over.StatusCode);
		Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
		await TradePartyTestSupport.WithTradeAsync(factory, async database =>
			Assert.Equal(["BIG-2"], await database.Products.AsNoTracking().Where(item => item.TenantId == companyId).Select(item => item.Code).ToListAsync()));
	}

	[Fact]
	public async Task InvalidInputGets400WithCamelCaseKeysAndNoEchoedValues()
	{
		var (client, _, _) = await FullAsync();
		var tooLong = new string('x', 65);

		var post = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, new
		{
			code = tooLong,
			name = " ",
			hsCode = "12\u202E34",
			baseUnitCode = "NOPE",
		});
		var put = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{Guid.NewGuid()}", MinimalPut("***"));
		var list = await client.GetAsync($"{Base}?search={new string('s', 101)}&productGroupCode={new string('g', 65)}", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
		var postBody = await TradePartyTestSupport.BodyAsync(post);
		var errors = JsonDocument.Parse(postBody).RootElement.GetProperty("errors");
		foreach (var key in new[] { "code", "name", "hsCode", "baseUnitCode" })
		{
			Assert.True(errors.TryGetProperty(key, out _), $"missing {key}");
		}

		Assert.DoesNotContain(tooLong, postBody, StringComparison.Ordinal);
		Assert.DoesNotContain("NOPE", postBody, StringComparison.Ordinal);
		Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
		Assert.True(JsonDocument.Parse(await TradePartyTestSupport.BodyAsync(put)).RootElement.GetProperty("errors").TryGetProperty("rowVersion", out _));
		Assert.Equal(HttpStatusCode.BadRequest, list.StatusCode);
		var listErrors = JsonDocument.Parse(await TradePartyTestSupport.BodyAsync(list)).RootElement.GetProperty("errors");
		Assert.True(listErrors.TryGetProperty("search", out _));
		Assert.True(listErrors.TryGetProperty("productGroupCode", out _));
	}

	[Theory]
	[InlineData("code", "\u202E")]
	[InlineData("name", "\u2066")]
	[InlineData("code", "\u061C")]
	[InlineData("name", "\uFEFF")]
	public async Task BidiAndInvisibleCharactersInCodeOrNameGet400OnPostAndPutWithoutEcho(string field, string bad)
	{
		var (client, _, _) = await FullAsync();
		var value = "a" + bad + "b";
		var code = field == "code" ? value : "OK-1";
		var name = field == "name" ? value : "Ok";

		var post = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, TradeProductTestSupport.Body(code, name));
		var put = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{Guid.NewGuid()}", new
		{
			code,
			name,
			baseUnitCode = "TON",
			rowVersion = Convert.ToBase64String(new byte[8]),
		});

		foreach (var response in new[] { post, put })
		{
			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			var body = await TradePartyTestSupport.BodyAsync(response);
			Assert.True(JsonDocument.Parse(body).RootElement.GetProperty("errors").TryGetProperty(field, out _));
			Assert.DoesNotContain(bad, body, StringComparison.Ordinal);
		}
	}

	// ---- SQL Server behaviour ----

	[Fact]
	public async Task CreateThenDetailThenListRoundTripWithTenantFromSessionOnlyAndIsActiveNotSettable()
	{
		RequireSql();
		var (client, companyId, _) = await FullAsync();

		var created = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, TradeProductTestSupport.Body(
			"  PR-100 ", "Hot rolled coil", group: "STEEL", hs: "0072.10", unit: "kg", tenantId: Guid.NewGuid(), isActive: false));

		Assert.Equal(HttpStatusCode.Created, created.StatusCode);
		TradePartyTestSupport.AssertNoStore(created);
		var detail = await TradePartyTestSupport.JsonAsync(created);
		var id = detail.GetProperty("id").GetGuid();
		Assert.Equal($"{Base}/{id}", created.Headers.Location!.OriginalString);
		Assert.Equal("PR-100", detail.GetProperty("code").GetString());
		Assert.Equal("STEEL", detail.GetProperty("productGroupCode").GetString());
		Assert.Equal("0072.10", detail.GetProperty("hsCode").GetString());
		Assert.Equal("KG", detail.GetProperty("baseUnitCode").GetString());
		Assert.True(detail.GetProperty("isActive").GetBoolean());
		Assert.False(string.IsNullOrEmpty(detail.GetProperty("rowVersion").GetString()));
		Assert.Equal(JsonValueKind.String, detail.GetProperty("createdAtUtc").ValueKind);
		Assert.False(detail.TryGetProperty("tenantId", out _));
		Assert.False(detail.TryGetProperty("createdByUserId", out _));
		await TradePartyTestSupport.WithTradeAsync(factory, async database =>
			Assert.Equal(companyId, (await database.Products.AsNoTracking().SingleAsync(item => item.Id == id)).TenantId));

		var read = await client.GetAsync($"{Base}/{id}", TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.OK, read.StatusCode);
		TradePartyTestSupport.AssertNoStore(read);
		Assert.Equal(detail.GetProperty("rowVersion").GetString(), (await TradePartyTestSupport.JsonAsync(read)).GetProperty("rowVersion").GetString());

		var listResponse = await client.GetAsync(Base, TestContext.Current.CancellationToken);
		TradePartyTestSupport.AssertNoStore(listResponse);
		var listBody = await TradePartyTestSupport.BodyAsync(listResponse);
		Assert.DoesNotContain("rowVersion", listBody, StringComparison.Ordinal);
		var list = JsonDocument.Parse(listBody).RootElement;
		Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
		Assert.Equal(20, list.GetProperty("pageSize").GetInt32());
		var item = Assert.Single(list.GetProperty("items").EnumerateArray());
		Assert.Equal(id, item.GetProperty("id").GetGuid());
		Assert.Equal("KG", item.GetProperty("baseUnitCode").GetString());
	}

	[Fact]
	public async Task ListIsOrderedByCodeWithPagingAndTotalCount()
	{
		RequireSql();
		var (client, companyId, _) = await FullAsync();
		foreach (var code in new[] { "C-3", "C-1", "C-5", "C-2", "C-4" })
		{
			await TradeProductTestSupport.SeedAsync(factory, companyId, code);
		}

		var second = await TradePartyTestSupport.JsonAsync(await client.GetAsync($"{Base}?pageSize=2&page=2", TestContext.Current.CancellationToken));
		Assert.Equal(["C-3", "C-4"], second.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("code").GetString()));
		Assert.Equal(5, second.GetProperty("totalCount").GetInt32());

		var clamped = await TradePartyTestSupport.JsonAsync(await client.GetAsync($"{Base}?pageSize=1000&page=0", TestContext.Current.CancellationToken));
		Assert.Equal(1, clamped.GetProperty("page").GetInt32());
		Assert.Equal(100, clamped.GetProperty("pageSize").GetInt32());
		Assert.Equal(5, clamped.GetProperty("items").GetArrayLength());

		var beyond = await TradePartyTestSupport.JsonAsync(await client.GetAsync($"{Base}?pageSize=2&page=9", TestContext.Current.CancellationToken));
		Assert.Equal(0, beyond.GetProperty("items").GetArrayLength());
		Assert.Equal(5, beyond.GetProperty("totalCount").GetInt32());
	}

	[Fact]
	public async Task SearchMatchesCodeAndNameOnlyAndTreatsWildcardsLiterally()
	{
		RequireSql();
		var (client, companyId, _) = await FullAsync();
		await TradeProductTestSupport.SeedAsync(factory, companyId, "AAA-1", "Alpha Coil", group: "HIDDEN-GRP", hs: "770011");
		await TradeProductTestSupport.SeedAsync(factory, companyId, "BBB_2", "Beta 100% Bar");
		await TradeProductTestSupport.SeedAsync(factory, companyId, "CCC-3", "Gamma");

		async Task<string[]> Search(string text)
		{
			var json = await TradePartyTestSupport.JsonAsync(await client.GetAsync($"{Base}?search={Uri.EscapeDataString(text)}", TestContext.Current.CancellationToken));
			return json.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("code").GetString()!).ToArray();
		}

		Assert.Empty(await Search("HIDDEN"));
		Assert.Empty(await Search("770011"));
		Assert.Equal(["AAA-1"], await Search("alpha"));
		Assert.Equal(["CCC-3"], await Search("ccc"));
		Assert.Equal(["BBB_2"], await Search("_"));
		Assert.Equal(["BBB_2"], await Search("%"));
		Assert.Equal(["BBB_2"], await Search("100%"));
		Assert.Empty(await Search("[a-z]"));
		Assert.Equal(3, (await Search("  ")).Length);
	}

	[Fact]
	public async Task GroupAndIsActiveFiltersApply()
	{
		RequireSql();
		var (client, companyId, _) = await FullAsync();
		await TradeProductTestSupport.SeedAsync(factory, companyId, "G-1", group: "STEEL");
		await TradeProductTestSupport.SeedAsync(factory, companyId, "G-2", group: "STEEL", inactive: true);
		await TradeProductTestSupport.SeedAsync(factory, companyId, "G-3", group: "COPPER");
		await TradeProductTestSupport.SeedAsync(factory, companyId, "G-4");

		async Task<string[]> Codes(string query)
		{
			var response = await client.GetAsync($"{Base}?{query}", TestContext.Current.CancellationToken);
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			return (await TradePartyTestSupport.JsonAsync(response)).GetProperty("items").EnumerateArray()
				.Select(item => item.GetProperty("code").GetString()!).ToArray();
		}

		Assert.Equal(["G-1", "G-2"], await Codes("productGroupCode=STEEL"));
		Assert.Equal(["G-3"], await Codes("productGroupCode=COPPER"));
		Assert.Equal(["G-2"], await Codes("isActive=false"));
		Assert.Equal(["G-1", "G-3", "G-4"], await Codes("isActive=true"));
		Assert.Equal(["G-1"], await Codes("productGroupCode=STEEL&isActive=true"));
		Assert.Empty(await Codes("productGroupCode=NONE"));
		var total = await TradePartyTestSupport.JsonAsync(await client.GetAsync($"{Base}?productGroupCode=STEEL&pageSize=1", TestContext.Current.CancellationToken));
		Assert.Equal(2, total.GetProperty("totalCount").GetInt32());
	}

	[Fact]
	public async Task OtherTenantProductIsInvisibleAndEveryAccessAnswersTheSameEmpty404()
	{
		RequireSql();
		var (client, companyId, _) = await FullAsync();
		var otherCompany = await factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("other-")));
		var foreign = await TradeProductTestSupport.SeedAsync(factory, otherCompany.Id, "FOREIGN-1", "Other tenant product");
		var own = await TradeProductTestSupport.SeedAsync(factory, companyId, "OWN-1");
		var deleted = await TradeProductTestSupport.SeedAsync(factory, companyId, "GONE-1", deleted: true);

		var list = await TradePartyTestSupport.JsonAsync(await client.GetAsync(Base, TestContext.Current.CancellationToken));
		Assert.Equal(["OWN-1"], list.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("code").GetString()));
		Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
		var filtered = await TradePartyTestSupport.JsonAsync(await client.GetAsync($"{Base}?search=FOREIGN", TestContext.Current.CancellationToken));
		Assert.Equal(0, filtered.GetProperty("totalCount").GetInt32());

		var ownDetail = await TradePartyTestSupport.JsonAsync(await client.GetAsync($"{Base}/{own}", TestContext.Current.CancellationToken));
		var putBody = new
		{
			code = "HIJACK",
			name = "Hijack",
			baseUnitCode = "TON",
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
			var row = await database.Products.AsNoTracking().SingleAsync(item => item.Id == foreign);
			Assert.Equal("FOREIGN-1", row.Code);
			Assert.Equal(otherCompany.Id, row.TenantId);
		});
	}

	[Fact]
	public async Task DuplicateActiveCodeConflictsCaseInsensitivelyButIsFreeInOtherTenantsAndAfterSoftDelete()
	{
		RequireSql();
		var (client, companyId, _) = await FullAsync();
		var otherCompany = await factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("other-")));
		await TradeProductTestSupport.SeedAsync(factory, otherCompany.Id, "DUP-1");
		await TradeProductTestSupport.SeedAsync(factory, companyId, "OLD-1", deleted: true);

		await CreateOkAsync(client, TradeProductTestSupport.Body("DUP-1"));
		var same = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, TradeProductTestSupport.Body("DUP-1"));
		var caseOnly = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, TradeProductTestSupport.Body("dup-1"));
		var reuse = await TradeHttp.SendAsync(client, HttpMethod.Post, Base, TradeProductTestSupport.Body("OLD-1"));

		foreach (var conflict in new[] { same, caseOnly })
		{
			Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
			var body = await TradePartyTestSupport.BodyAsync(conflict);
			Assert.Equal("trade_product_code_duplicate", JsonDocument.Parse(body).RootElement.GetProperty("detail").GetString());
			Assert.DoesNotContain("UX_Products", body, StringComparison.Ordinal);
			Assert.DoesNotContain("duplicate key", body, StringComparison.OrdinalIgnoreCase);
		}

		Assert.Equal(HttpStatusCode.Created, reuse.StatusCode);

		var second = await CreateOkAsync(client, TradeProductTestSupport.Body("DUP-2"));
		var clash = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{second.GetProperty("id").GetGuid()}", UpdateBody(second, code: "dup-1"));
		Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
		Assert.Equal("trade_product_code_duplicate", await TradeHttp.DetailAsync(clash));
	}

	[Fact]
	public async Task TwoRacingCreatesOfTheSameCodeYieldOneCreatedAndOneConflict()
	{
		RequireSql();
		var (client, _, _) = await FullAsync();

		var results = await Task.WhenAll(
			Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
				TradeHttp.SendAsync(client, HttpMethod.Post, Base, TradeProductTestSupport.Body("RACE-1")))));

		Assert.Equal(1, results.Count(response => response.StatusCode == HttpStatusCode.Created));
		var loser = Assert.Single(results, response => response.StatusCode == HttpStatusCode.Conflict);
		Assert.Equal("trade_product_code_duplicate", await TradeHttp.DetailAsync(loser));
	}

	[Fact]
	public async Task UpdateEnforcesRowVersionAndChangesItOnEveryAcceptedWrite()
	{
		RequireSql();
		var (client, companyId, _) = await FullAsync();
		var created = await CreateOkAsync(client, TradeProductTestSupport.Body("U-1", group: "G", hs: "1234"));
		var id = created.GetProperty("id").GetGuid();
		var v0 = created.GetProperty("rowVersion").GetString();

		var renamed = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{id}", new
		{
			code = "U-1",
			name = "Renamed",
			productGroupCode = "G2",
			hsCode = (string?)null,
			baseUnitCode = "pcs",
			rowVersion = v0,
			tenantId = Guid.NewGuid(),
			isActive = false,
		});
		Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
		var after = await TradePartyTestSupport.JsonAsync(renamed);
		var v1 = after.GetProperty("rowVersion").GetString();
		Assert.NotEqual(v0, v1);
		Assert.Equal("Renamed", after.GetProperty("name").GetString());
		Assert.Equal("G2", after.GetProperty("productGroupCode").GetString());
		Assert.Equal(JsonValueKind.Null, after.GetProperty("hsCode").ValueKind);
		Assert.Equal("PCS", after.GetProperty("baseUnitCode").GetString());
		Assert.True(after.GetProperty("isActive").GetBoolean());
		Assert.Equal(JsonValueKind.String, after.GetProperty("updatedAtUtc").ValueKind);

		var stale = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{id}", UpdateBody(after, rowVersion: v0, name: "Stale write"));
		Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
		var staleBody = await TradePartyTestSupport.BodyAsync(stale);
		Assert.Equal("trade_concurrency_conflict", JsonDocument.Parse(staleBody).RootElement.GetProperty("detail").GetString());
		Assert.DoesNotContain("Stale write", staleBody, StringComparison.Ordinal);

		var noOp = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{id}", UpdateBody(after));
		Assert.Equal(HttpStatusCode.OK, noOp.StatusCode);
		Assert.NotEqual(v1, (await TradePartyTestSupport.JsonAsync(noOp)).GetProperty("rowVersion").GetString());

		var unknownUnit = await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{id}", UpdateBody(after, unit: "NOPE"));
		Assert.Equal(HttpStatusCode.BadRequest, unknownUnit.StatusCode);

		await TradePartyTestSupport.WithTradeAsync(factory, async database =>
		{
			var row = await database.Products.AsNoTracking().SingleAsync(item => item.Id == id);
			Assert.Equal(companyId, row.TenantId);
			Assert.Equal("Renamed", row.Name);
			Assert.True(row.IsActive);
		});
	}
}

/// <summary>The production deny-all entitlement keeps the Product API closed.</summary>
[Collection(TradeHttp.CollectionName)]
public sealed class TradeProductDenyAllHttpTests : IClassFixture<TradeProbeFactory>
{
	private readonly TradeProbeFactory factory;

	public TradeProductDenyAllHttpTests(TradeProbeFactory factory) => this.factory = factory;

	[Fact]
	public async Task ProductRoutesAre403ModuleUnavailableWithProductionDenyAllEvenInAClosedPeriod()
	{
		foreach (var status in new[] { Domain.FiscalPeriodStatus.Open, Domain.FiscalPeriodStatus.Closed })
		{
			var (user, company, periodId) = await TradeHttp.SeedAsync(factory, status,
				TradeProductTestSupport.View, TradeProductTestSupport.Create, TradeProductTestSupport.Update);
			using var client = TradeHttp.Create(factory, out _);
			await Browser.SignInAsync(client, user);
			await TradeHttp.SelectContextAsync(client, company.Id, periodId);
			var id = Guid.NewGuid();
			const string Base = TradeProductTestSupport.Base;

			var responses = new[]
			{
				await client.GetAsync(Base, TestContext.Current.CancellationToken),
				await client.GetAsync($"{Base}/{id}", TestContext.Current.CancellationToken),
				await client.GetAsync("/api/v1/trade/reference/units", TestContext.Current.CancellationToken),
				await TradeHttp.SendAsync(client, HttpMethod.Post, Base, TradeProductTestSupport.Body("A")),
				await TradeHttp.SendAsync(client, HttpMethod.Put, $"{Base}/{id}", TradeProductTestSupport.Body("A")),
			};

			foreach (var response in responses)
			{
				Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
				Assert.Equal("trade_module_unavailable", await TradeHttp.DetailAsync(response));
			}
		}
	}
}
