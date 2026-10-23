using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// The company and fiscal period selection through the real pipeline (cookie authentication,
/// anti-forgery, the work-context cookie, the database).
/// </summary>
public sealed class WorkContextHttpTests : IClassFixture<FaranouApplicationFactory>
{
	private static readonly DateOnly Start = new(2026, 3, 21);
	private static readonly DateOnly End = new(2027, 3, 20);

	public WorkContextHttpTests(FaranouApplicationFactory factory)
	{
		Factory = factory;
	}

	private FaranouApplicationFactory Factory { get; }

	private async Task<(SeededUser User, SeededCompany Company, Guid PeriodId)> UserWithOneOpenPeriodAsync()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		var company = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("شرکت-")));
		await Factory.WithDatabaseAsync(database => TestData.AddMembershipAsync(database, user, company));
		var periodId = await Factory.WithDatabaseAsync(database =>
			TestData.AddFiscalPeriodAsync(database, company, "سال مالی ۱۴۰۵", Start, End));

		return (user, company, periodId);
	}

	private static async Task<HttpResponseMessage> SendWithTokenAsync(
		HttpClient client, HttpMethod method, string path, object? body = null)
	{
		var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/antiforgery/token");
		using var request = new HttpRequestMessage(method, path);
		request.Headers.Add(token.GetProperty("headerName").GetString()!, token.GetProperty("requestToken").GetString());

		if (body is not null)
		{
			request.Content = JsonContent.Create(body);
		}

		return await client.SendAsync(request);
	}

	private static Task<HttpResponseMessage> SelectAsync(HttpClient client, Guid companyId, Guid periodId)
	{
		return SendWithTokenAsync(client, HttpMethod.Post, "/api/v1/context", new { companyId, fiscalPeriodId = periodId });
	}

	private static Task<JsonElement> GetContextAsync(HttpClient client)
	{
		return client.GetFromJsonAsync<JsonElement>("/api/v1/context");
	}

	[Fact]
	public async Task AnAnonymousRequest_Gets401()
	{
		using var client = Factory.CreateBrowser();

		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/context")).StatusCode);
	}

	[Fact]
	public async Task OneCompanyWithOneOpenPeriod_IsEnteredDirectly_AfterTheServerConfirmsIt()
	{
		var (user, company, periodId) = await UserWithOneOpenPeriodAsync();
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var before = await GetContextAsync(client);
		Assert.Equal("directEntry", before.GetProperty("entry").GetString());
		Assert.Equal(JsonValueKind.Null, before.GetProperty("current").ValueKind);
		var direct = before.GetProperty("directSelection");
		Assert.Equal(company.Id.ToString(), direct.GetProperty("companyId").GetString());
		Assert.Equal(periodId.ToString(), direct.GetProperty("fiscalPeriodId").GetString());

		var selected = await SelectAsync(client, company.Id, periodId);
		Assert.Equal(HttpStatusCode.OK, selected.StatusCode);

		var after = await GetContextAsync(client);
		Assert.Equal(company.Id.ToString(), after.GetProperty("current").GetProperty("company").GetProperty("id").GetString());
		Assert.Equal(periodId.ToString(), after.GetProperty("current").GetProperty("fiscalPeriod").GetProperty("id").GetString());
	}

	[Fact]
	public async Task OneCompanyWithTwoOpenPeriods_RequiresASelection()
	{
		var (user, company, _) = await UserWithOneOpenPeriodAsync();
		await Factory.WithDatabaseAsync(database => TestData.AddFiscalPeriodAsync(
			database, company, "سال مالی ۱۴۰۶", Start.AddYears(1), End.AddYears(1)));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var context = await GetContextAsync(client);

		Assert.Equal("selectionRequired", context.GetProperty("entry").GetString());
		Assert.Equal(JsonValueKind.Null, context.GetProperty("directSelection").ValueKind);
		Assert.Equal(2, Assert.Single(context.GetProperty("companies").EnumerateArray()).GetProperty("fiscalPeriods").GetArrayLength());
	}

	[Fact]
	public async Task SeveralCompanies_RequireASelection()
	{
		var (user, _, _) = await UserWithOneOpenPeriodAsync();
		var second = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("شرکت-")));
		await Factory.WithDatabaseAsync(database => TestData.AddMembershipAsync(database, user, second));
		await Factory.WithDatabaseAsync(database => TestData.AddFiscalPeriodAsync(database, second, "دوره", Start, End));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var context = await GetContextAsync(client);

		Assert.Equal("selectionRequired", context.GetProperty("entry").GetString());
		Assert.Equal(2, context.GetProperty("companies").GetArrayLength());
	}

	[Fact]
	public async Task DefaultCompanyPreference_IsPersistedPerUser_AndReturnedByTheContextApi()
	{
		var (user, first, _) = await UserWithOneOpenPeriodAsync();
		var second = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("شرکت-")));
		await Factory.WithDatabaseAsync(database => TestData.AddMembershipAsync(database, user, second));
		await Factory.WithDatabaseAsync(database => TestData.AddFiscalPeriodAsync(database, second, "دوره دوم", Start, End));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var response = await SendWithTokenAsync(client, HttpMethod.Post, "/api/v1/context/default-company", new { companyId = second.Id });
		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

		var context = await GetContextAsync(client);
		Assert.Equal(second.Id.ToString(), context.GetProperty("defaultCompanyId").GetString());
		var storedPreference = await Factory.WithDatabaseAsync(database => database.Users
			.Where(account => account.Id == user.Id)
			.Select(account => account.DefaultCompanyId)
			.SingleAsync());
		Assert.Equal(second.Id, storedPreference);
		Assert.NotEqual(first.Id, storedPreference);
	}

	[Fact]
	public async Task DefaultCompanyPreference_CannotBeSetToACompanyWithoutActiveMembership()
	{
		var (user, ownCompany, _) = await UserWithOneOpenPeriodAsync();
		var foreignCompany = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("شرکت-")));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var response = await SendWithTokenAsync(client, HttpMethod.Post, "/api/v1/context/default-company", new { companyId = foreignCompany.Id });
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

		var storedPreference = await Factory.WithDatabaseAsync(database => database.Users
			.Where(account => account.Id == user.Id)
			.Select(account => account.DefaultCompanyId)
			.SingleAsync());
		Assert.Null(storedPreference);
		Assert.Equal(ownCompany.Id.ToString(), Assert.Single((await GetContextAsync(client)).GetProperty("companies").EnumerateArray()).GetProperty("id").GetString());
	}

	[Fact]
	public async Task CompanyRelationshipTypes_AreShownOnlyWhenBothCompaniesShareAProvisionedBoundary()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		var parent = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("مادر-")));
		var child = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("فرعی-")));
		await Factory.WithDatabaseAsync(database => TestData.AddMembershipAsync(database, user, parent));
		await Factory.WithDatabaseAsync(database => TestData.AddMembershipAsync(database, user, child));
		await Factory.WithDatabaseAsync(database => TestData.AddFiscalPeriodAsync(database, parent, "دوره", Start, End));
		await Factory.WithDatabaseAsync(database => TestData.AddFiscalPeriodAsync(database, child, "دوره", Start, End));
		var boundaryId = Guid.NewGuid();
		await Factory.WithDatabaseAsync(async database =>
		{
			var parentEntity = await database.Companies.SingleAsync(company => company.Id == parent.Id);
			var childEntity = await database.Companies.SingleAsync(company => company.Id == child.Id);
			parentEntity.CustomerBoundaryId = boundaryId;
			childEntity.CustomerBoundaryId = boundaryId;
			database.OwnershipRelationships.Add(new Domain.OwnershipRelationship
			{
				ParentCompanyId = parent.Id,
				SubsidiaryCompanyId = child.Id,
				OwnershipPercentage = 100m,
				VotingPercentage = 100m,
				ControlBasis = Domain.ControlBasis.Control,
				ValidFrom = Start,
			});
			await database.SaveChangesAsync();
		});

		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);
		var companies = (await GetContextAsync(client)).GetProperty("companies");
		var parentTypes = Assert.Single(companies.EnumerateArray().Where(item => item.GetProperty("id").GetGuid() == parent.Id)).GetProperty("relationshipTypes");
		var childTypes = Assert.Single(companies.EnumerateArray().Where(item => item.GetProperty("id").GetGuid() == child.Id)).GetProperty("relationshipTypes");
		Assert.Contains("parent", parentTypes.EnumerateArray().Select(item => item.GetString()));
		Assert.Contains("subsidiary", childTypes.EnumerateArray().Select(item => item.GetString()));
	}

	[Fact]
	public async Task CompanyRelationshipTypes_DoNotClassifyAnUnprovisionedOrCrossBoundaryRelationship()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		var parent = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("مادر-")));
		var child = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("فرعی-")));
		await Factory.WithDatabaseAsync(database => TestData.AddMembershipAsync(database, user, parent));
		await Factory.WithDatabaseAsync(database => TestData.AddMembershipAsync(database, user, child));
		await Factory.WithDatabaseAsync(database => TestData.AddFiscalPeriodAsync(database, parent, "دوره", Start, End));
		await Factory.WithDatabaseAsync(database => TestData.AddFiscalPeriodAsync(database, child, "دوره", Start, End));
		await Factory.WithDatabaseAsync(async database =>
		{
			var parentEntity = await database.Companies.SingleAsync(company => company.Id == parent.Id);
			var childEntity = await database.Companies.SingleAsync(company => company.Id == child.Id);
			parentEntity.CustomerBoundaryId = Guid.NewGuid();
			childEntity.CustomerBoundaryId = Guid.NewGuid();
			database.OwnershipRelationships.Add(new Domain.OwnershipRelationship
			{
				ParentCompanyId = parent.Id,
				SubsidiaryCompanyId = child.Id,
				OwnershipPercentage = 100m,
				VotingPercentage = 100m,
				ControlBasis = Domain.ControlBasis.Control,
				ValidFrom = Start,
			});
			await database.SaveChangesAsync();
		});

		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);
		var companies = (await GetContextAsync(client)).GetProperty("companies");
		foreach (var company in companies.EnumerateArray())
		{
			var types = company.GetProperty("relationshipTypes").EnumerateArray().Select(item => item.GetString()).ToArray();
			Assert.Equal(new[] { "unknown" }, types);
			Assert.DoesNotContain("parent", types);
			Assert.DoesNotContain("subsidiary", types);
		}
	}

	[Fact]
	public async Task CompanyUser_CannotProvisionCustomerBoundaries()
	{
		var (user, company, _) = await UserWithOneOpenPeriodAsync();
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var response = await client.PostAsJsonAsync("/api/v1/operator/customer-boundaries/assign", new
		{
			boundaryId = Guid.NewGuid(),
			companyIds = new[] { company.Id },
		});
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
		var boundary = await Factory.WithDatabaseAsync(database => database.Companies
			.Where(item => item.Id == company.Id)
			.Select(item => item.CustomerBoundaryId)
			.SingleAsync());
		Assert.Null(boundary);
	}

	[Fact]
	public async Task NoCompany_IsReportedAndNothingCanBeSelected()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var context = await GetContextAsync(client);

		Assert.Equal("noCompany", context.GetProperty("entry").GetString());
		Assert.Equal(0, context.GetProperty("companies").GetArrayLength());
		Assert.Equal(HttpStatusCode.NotFound, (await SelectAsync(client, Guid.NewGuid(), Guid.NewGuid())).StatusCode);
	}

	[Fact]
	public async Task ACompanyWithoutAnySelectablePeriod_IsReported_AndEntryIsRefused()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		var company = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("شرکت-")));
		await Factory.WithDatabaseAsync(database => TestData.AddMembershipAsync(database, user, company));
		var closedId = await Factory.WithDatabaseAsync(database => TestData.AddFiscalPeriodAsync(
			database, company, "بایگانی", Start, End, Domain.FiscalPeriodStatus.Archived));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var context = await GetContextAsync(client);

		Assert.Equal("noEligiblePeriod", context.GetProperty("entry").GetString());
		var period = Assert.Single(Assert.Single(context.GetProperty("companies").EnumerateArray())
			.GetProperty("fiscalPeriods").EnumerateArray());
		Assert.False(period.GetProperty("isSelectable").GetBoolean());
		Assert.Equal("Archived", period.GetProperty("status").GetString());
		Assert.Equal(JsonValueKind.Null, period.GetProperty("accessMode").ValueKind);

		// The server refuses the archived period even though the client sends a real identifier.
		Assert.Equal(HttpStatusCode.Conflict, (await SelectAsync(client, company.Id, closedId)).StatusCode);
		Assert.Equal(JsonValueKind.Null, (await GetContextAsync(client)).GetProperty("current").ValueKind);
	}

	[Fact]
	public async Task ACompanyOfSomeoneElse_CannotBeSelected_AndLooksLikeAMissingOne()
	{
		var (user, _, _) = await UserWithOneOpenPeriodAsync();
		var (_, foreignCompany, foreignPeriod) = await UserWithOneOpenPeriodAsync();
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var foreign = await SelectAsync(client, foreignCompany.Id, foreignPeriod);
		var missing = await SelectAsync(client, Guid.NewGuid(), Guid.NewGuid());

		Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
		Assert.Equal(await missing.Content.ReadAsStringAsync(), await foreign.Content.ReadAsStringAsync());
		Assert.Equal(JsonValueKind.Null, (await GetContextAsync(client)).GetProperty("current").ValueKind);
	}

	[Fact]
	public async Task AFiscalPeriodOfAnotherCompany_IsRejected_EvenWithOwnCompany()
	{
		var (user, company, _) = await UserWithOneOpenPeriodAsync();
		var (_, _, foreignPeriod) = await UserWithOneOpenPeriodAsync();
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var response = await SelectAsync(client, company.Id, foreignPeriod);

		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
		Assert.Empty(response.Headers.TryGetValues("Set-Cookie", out var cookies)
			? cookies.Where(value => value.StartsWith(".Faranou.WorkContext=", StringComparison.Ordinal) && !value.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase))
			: Array.Empty<string>());
	}

	[Fact]
	public async Task TheResponses_NeverContainDataOfAnotherCompany()
	{
		var (user, _, _) = await UserWithOneOpenPeriodAsync();
		var foreign = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, "شرکت-محرمانه-دیگران"));
		await Factory.WithDatabaseAsync(database => TestData.AddFiscalPeriodAsync(database, foreign, "دوره-محرمانه", Start, End));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var body = await (await client.GetAsync("/api/v1/context")).Content.ReadAsStringAsync();

		Assert.DoesNotContain("محرمانه", body, StringComparison.Ordinal);
		Assert.DoesNotContain(foreign.Id.ToString(), body, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task ChangesToTheContext_RequireAValidAntiForgeryToken()
	{
		var (user, company, periodId) = await UserWithOneOpenPeriodAsync();
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		using var noToken = new HttpRequestMessage(HttpMethod.Post, "/api/v1/context")
		{
			Content = JsonContent.Create(new { companyId = company.Id, fiscalPeriodId = periodId }),
		};
		using var forged = new HttpRequestMessage(HttpMethod.Post, "/api/v1/context")
		{
			Content = JsonContent.Create(new { companyId = company.Id, fiscalPeriodId = periodId }),
		};
		forged.Headers.Add("X-XSRF-TOKEN", "forged-token");

		Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(noToken)).StatusCode);
		Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(forged)).StatusCode);
		Assert.Equal(JsonValueKind.Null, (await GetContextAsync(client)).GetProperty("current").ValueKind);

		using var deleteWithoutToken = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/context");
		Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(deleteWithoutToken)).StatusCode);
	}

	[Fact]
	public async Task AnInvalidBody_IsRejected()
	{
		var (user, _, _) = await UserWithOneOpenPeriodAsync();
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var empty = await SendWithTokenAsync(client, HttpMethod.Post, "/api/v1/context", new { });
		var notGuid = await SendWithTokenAsync(client, HttpMethod.Post, "/api/v1/context",
			new { companyId = "not-a-guid", fiscalPeriodId = "x" });

		Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
		Assert.Equal(HttpStatusCode.BadRequest, notGuid.StatusCode);
	}

	[Fact]
	public async Task ARevokedMembership_EndsTheContextOnTheNextRequest()
	{
		var (user, company, periodId) = await UserWithOneOpenPeriodAsync();
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);
		Assert.Equal(HttpStatusCode.OK, (await SelectAsync(client, company.Id, periodId)).StatusCode);
		Assert.NotEqual(JsonValueKind.Null, (await GetContextAsync(client)).GetProperty("current").ValueKind);

		await Factory.WithDatabaseAsync(async database =>
		{
			var membership = await database.UserCompanyMemberships
				.SingleAsync(item => item.UserId == user.Id && item.TenantId == company.Id);
			membership.IsActive = false;
			await database.SaveChangesAsync();
		});

		var after = await GetContextAsync(client);
		Assert.Equal(JsonValueKind.Null, after.GetProperty("current").ValueKind);
		Assert.Equal("noCompany", after.GetProperty("entry").GetString());
	}

	[Fact]
	public async Task APeriodThatIsArchivedAfterTheSelection_EndsTheContext()
	{
		var (user, company, periodId) = await UserWithOneOpenPeriodAsync();
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);
		await SelectAsync(client, company.Id, periodId);

		await Factory.WithDatabaseAsync(async database =>
		{
			var period = await database.FiscalPeriods.SingleAsync(item => item.Id == periodId);
			period.Status = Domain.FiscalPeriodStatus.Archived;
			await database.SaveChangesAsync();
		});

		Assert.Equal(JsonValueKind.Null, (await GetContextAsync(client)).GetProperty("current").ValueKind);
	}

	[Fact]
	public async Task ADeactivatedCompany_EndsTheContext()
	{
		var (user, company, periodId) = await UserWithOneOpenPeriodAsync();
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);
		await SelectAsync(client, company.Id, periodId);

		await Factory.WithDatabaseAsync(async database =>
		{
			var entity = await database.Companies.SingleAsync(item => item.Id == company.Id);
			entity.IsActive = false;
			await database.SaveChangesAsync();
		});

		Assert.Equal(JsonValueKind.Null, (await GetContextAsync(client)).GetProperty("current").ValueKind);
	}

	[Fact]
	public async Task ChangingTheSelection_ReplacesTheContext_AndClearingForgetsIt()
	{
		var (user, company, firstPeriod) = await UserWithOneOpenPeriodAsync();
		var secondPeriod = await Factory.WithDatabaseAsync(database => TestData.AddFiscalPeriodAsync(
			database, company, "سال مالی ۱۴۰۶", Start.AddYears(1), End.AddYears(1)));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		await SelectAsync(client, company.Id, firstPeriod);
		await SelectAsync(client, company.Id, secondPeriod);
		Assert.Equal(secondPeriod.ToString(),
			(await GetContextAsync(client)).GetProperty("current").GetProperty("fiscalPeriod").GetProperty("id").GetString());

		Assert.Equal(HttpStatusCode.NoContent, (await SendWithTokenAsync(client, HttpMethod.Delete, "/api/v1/context")).StatusCode);
		Assert.Equal(JsonValueKind.Null, (await GetContextAsync(client)).GetProperty("current").ValueKind);
	}

	[Fact]
	public async Task ANewSignIn_NeverRestoresThePreviousChoice()
	{
		var (user, company, periodId) = await UserWithOneOpenPeriodAsync();
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);
		await SelectAsync(client, company.Id, periodId);
		Assert.NotEqual(JsonValueKind.Null, (await GetContextAsync(client)).GetProperty("current").ValueKind);

		// A second sign-in in the same browser (the choice cookie is still in the jar).
		var again = await Browser.PostLoginAsync(client, user.Username, user.Password);
		Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);

		Assert.Equal(JsonValueKind.Null, (await GetContextAsync(client)).GetProperty("current").ValueKind);
	}

	[Fact]
	public async Task AfterSigningOut_TheContextIsGoneAndTheApiIsClosed()
	{
		var (user, company, periodId) = await UserWithOneOpenPeriodAsync();
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);
		await SelectAsync(client, company.Id, periodId);

		Assert.Equal(HttpStatusCode.NoContent, (await SendWithTokenAsync(client, HttpMethod.Post, "/api/v1/session/logout")).StatusCode);

		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/context")).StatusCode);
	}

	[Fact]
	public async Task TheContextResponse_IsNotCacheable()
	{
		var (user, _, _) = await UserWithOneOpenPeriodAsync();
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var response = await client.GetAsync("/api/v1/context");

		Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
	}

	[Fact]
	public async Task TheContextCookie_IsHttpOnlySecureAndEndsWithTheBrowserSession()
	{
		var (user, company, periodId) = await UserWithOneOpenPeriodAsync();
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var response = await SelectAsync(client, company.Id, periodId);

		var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"),
			value => value.StartsWith(".Faranou.WorkContext=", StringComparison.Ordinal));
		Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);
		// Only opaque protected data: neither identifier appears in clear text.
		Assert.DoesNotContain(company.Id.ToString(), cookie, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain(periodId.ToString(), cookie, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task ACompanyWithOnlyAClosedPeriod_CanBeEnteredReadOnly_AndTheModeFollowsTheDatabase()
	{
		var (user, company, periodId) = await UserWithOneOpenPeriodAsync();
		await Factory.WithDatabaseAsync(async database =>
		{
			var period = await database.FiscalPeriods.SingleAsync(item => item.Id == periodId);
			period.Status = Domain.FiscalPeriodStatus.Closed;
			await database.SaveChangesAsync();
		});
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		var response = await SelectAsync(client, company.Id, periodId);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		Assert.Equal("readOnly", body.GetProperty("fiscalPeriod").GetProperty("accessMode").GetString());
		var current = (await GetContextAsync(client)).GetProperty("current");
		Assert.Equal("readOnly", current.GetProperty("fiscalPeriod").GetProperty("accessMode").GetString());

		// The period is reopened: the very next read reports full access.
		await Factory.WithDatabaseAsync(async database =>
		{
			var period = await database.FiscalPeriods.SingleAsync(item => item.Id == periodId);
			period.Status = Domain.FiscalPeriodStatus.Open;
			await database.SaveChangesAsync();
		});
		var reopened = (await GetContextAsync(client)).GetProperty("current");
		Assert.Equal("readWrite", reopened.GetProperty("fiscalPeriod").GetProperty("accessMode").GetString());
	}

	[Fact]
	public async Task ChangingTheWorkContextInTheSameSignIn_ReplacesIt_IsAudited_AndNeedsNoNewSignIn()
	{
		var (user, first, firstPeriod) = await UserWithOneOpenPeriodAsync();
		var second = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("شرکت-")));
		await Factory.WithDatabaseAsync(database => TestData.AddMembershipAsync(database, user, second));
		var secondPeriod = await Factory.WithDatabaseAsync(database =>
			TestData.AddFiscalPeriodAsync(database, second, "سال مالی دوم", Start, End));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);

		Assert.Equal(HttpStatusCode.OK, (await SelectAsync(client, first.Id, firstPeriod)).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await SelectAsync(client, second.Id, secondPeriod)).StatusCode);

		var current = (await GetContextAsync(client)).GetProperty("current");
		Assert.Equal(second.Id, current.GetProperty("company").GetProperty("id").GetGuid());
		Assert.Equal(secondPeriod, current.GetProperty("fiscalPeriod").GetProperty("id").GetGuid());

		var events = await Factory.WithDatabaseAsync(database => database.SecurityAuditEvents
			.Where(item => item.UserId == user.Id && item.EventType.StartsWith("WorkContext"))
			.OrderBy(item => item.OccurredAtUtc)
			.ToListAsync());
		Assert.Equal(new[] { "WorkContextSelected", "WorkContextChanged" }, events.Select(item => item.EventType).ToArray());
		Assert.Equal(second.Id, events[1].TenantId);
		Assert.Contains(first.Id.ToString(), events[1].DetailsJson);
		Assert.DoesNotContain("Cookie", events[1].DetailsJson!, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task ADeniedChange_KeepsThePreviousContext_DoesNotGrantTheOtherCompany_AndIsAudited()
	{
		var (user, own, ownPeriod) = await UserWithOneOpenPeriodAsync();
		var foreign = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("شرکت-")));
		var foreignPeriod = await Factory.WithDatabaseAsync(database =>
			TestData.AddFiscalPeriodAsync(database, foreign, "دوره-محرمانه", Start, End));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);
		await SelectAsync(client, own.Id, ownPeriod);

		var denied = await SelectAsync(client, foreign.Id, foreignPeriod);

		Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
		var current = (await GetContextAsync(client)).GetProperty("current");
		Assert.Equal(own.Id, current.GetProperty("company").GetProperty("id").GetGuid());
		var events = await Factory.WithDatabaseAsync(database => database.SecurityAuditEvents
			.Where(item => item.UserId == user.Id && item.EventType == "WorkContextSelectionDenied")
			.ToListAsync());
		var deniedEvent = Assert.Single(events);
		Assert.Null(deniedEvent.TenantId);
	}

	[Fact]
	public async Task AfterAChange_TheMembershipOfTheOldCompanyNoLongerMatters_ButTheNewOneIsStillChecked()
	{
		var (user, first, firstPeriod) = await UserWithOneOpenPeriodAsync();
		var second = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, TestData.Unique("شرکت-")));
		await Factory.WithDatabaseAsync(database => TestData.AddMembershipAsync(database, user, second));
		var secondPeriod = await Factory.WithDatabaseAsync(database =>
			TestData.AddFiscalPeriodAsync(database, second, "دوره", Start, End));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, user);
		await SelectAsync(client, first.Id, firstPeriod);
		await SelectAsync(client, second.Id, secondPeriod);

		await Factory.WithDatabaseAsync(async database =>
		{
			var membership = await database.UserCompanyMemberships.SingleAsync(item => item.UserId == user.Id && item.TenantId == first.Id);
			membership.IsActive = false;
			await database.SaveChangesAsync();
		});
		Assert.Equal(second.Id, (await GetContextAsync(client)).GetProperty("current").GetProperty("company").GetProperty("id").GetGuid());

		await Factory.WithDatabaseAsync(async database =>
		{
			var membership = await database.UserCompanyMemberships.SingleAsync(item => item.UserId == user.Id && item.TenantId == second.Id);
			membership.IsActive = false;
			await database.SaveChangesAsync();
		});
		Assert.Equal(JsonValueKind.Null, (await GetContextAsync(client)).GetProperty("current").ValueKind);
	}
}
