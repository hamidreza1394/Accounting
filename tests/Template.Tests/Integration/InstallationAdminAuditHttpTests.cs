using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// D-088 over real HTTP with the real cookie sign-in: the legacy administrator pages record views and
/// changes, and the administrator role alone does not open company-scoped or Trade data (OD-1 item 4).
/// </summary>
public sealed class InstallationAdminAuditHttpTests : IClassFixture<FaranouApplicationFactory>
{
	public InstallationAdminAuditHttpTests(FaranouApplicationFactory factory)
	{
		Factory = factory;
	}

	private FaranouApplicationFactory Factory { get; }

	private async Task<SeededUser> AddAdministratorAsync() => await Factory.WithDatabaseAsync(async database =>
	{
		var user = await TestData.AddUserAsync(database);
		var role = await database.Roles.FirstOrDefaultAsync(item => item.Name == Constants.Role.Admin);
		if (role is null)
		{
			role = new Domain.Role(Constants.Role.Admin) { IsActive = true };
			database.Roles.Add(role);
			await database.SaveChangesAsync();
		}

		(await database.Users.SingleAsync(item => item.Id == user.Id)).RoleId = role.Id;
		await database.SaveChangesAsync();
		return user;
	});

	private Task<List<Domain.SecurityAuditEvent>> EventsAsync(Guid actorId, string eventType) =>
		Factory.WithDatabaseAsync(database => database.SecurityAuditEvents.AsNoTracking()
			.Where(item => item.UserId == actorId && item.EventType == eventType).ToListAsync());

	[Fact]
	public async Task TheUsersPages_RecordTheAdministratorsViews()
	{
		var admin = await AddAdministratorAsync();
		var target = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, admin);

		Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Admin/Users/Index")).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Admin/Users/Details?id={target.Id}")).StatusCode);

		var views = await EventsAsync(admin.Id, Infrastructure.Security.InstallationAdminAudit.DataViewed);
		Assert.Contains(views, item => item.EntityType == "Users");
		Assert.Contains(views, item => item.EntityType == "User" && item.EntityId == target.Id.ToString());
	}

	[Fact]
	public async Task DeletingAUserOnTheLegacyPage_IsRecordedWithThePreviousValues_AndTheAuditRecordSurvives()
	{
		var admin = await AddAdministratorAsync();
		var target = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, admin);

		var token = await Browser.ReadFormTokenAsync(client, $"/Admin/Users/Delete?id={target.Id}");
		var response = await client.PostAsync($"/Admin/Users/Delete?id={target.Id}",
			new FormUrlEncodedContent([new("__RequestVerificationToken", token)]));

		Assert.True((int)response.StatusCode is >= 200 and < 400, $"delete returned {(int)response.StatusCode}");
		Assert.False(await Factory.WithDatabaseAsync(database => database.Users.AnyAsync(item => item.Id == target.Id)));
		var deleted = Assert.Single(await EventsAsync(admin.Id, Infrastructure.Security.InstallationAdminAudit.UserDeleted));
		Assert.Equal(target.Id.ToString(), deleted.EntityId);
		using var details = JsonDocument.Parse(deleted.DetailsJson!);
		var username = details.RootElement.GetProperty("changes").EnumerateArray()
			.Single(change => change.GetProperty("Field").GetString() == "Username");
		Assert.Equal(target.Username, username.GetProperty("Previous").GetString());
		Assert.Equal(JsonValueKind.Null, username.GetProperty("Current").ValueKind);
	}

	[Fact]
	public async Task TheAdministratorRoleAlone_DoesNotOpenCompanyScopedOrTradeData()
	{
		var admin = await AddAdministratorAsync();
		var company = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, Guid.NewGuid().ToString("N")));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, admin);

		// no membership of the company: the company user directory and Trade data stay closed (the D-071 membership-request queue is the one audited exception)
		var directory = await client.GetAsync($"/api/v1/companies/{company.Id}/users");
		var tradeParties = await client.GetAsync("/api/v1/trade/parties");
		var tradeCapabilities = await client.GetAsync("/api/v1/trade/capabilities");

		Assert.False(directory.IsSuccessStatusCode, $"company user directory returned {(int)directory.StatusCode}");
		Assert.False(tradeParties.IsSuccessStatusCode, $"trade parties returned {(int)tradeParties.StatusCode}");
		Assert.False(tradeCapabilities.IsSuccessStatusCode, $"trade capabilities returned {(int)tradeCapabilities.StatusCode}");
	}

	[Fact]
	public async Task TheAdministratorReadingAnyCompanysMembershipQueue_IsRecordedWithTheCompany()
	{
		var admin = await AddAdministratorAsync();
		var company = await Factory.WithDatabaseAsync(database => TestData.AddCompanyAsync(database, Guid.NewGuid().ToString("N")));
		using var client = Factory.CreateBrowser();
		await Browser.SignInAsync(client, admin);

		var response = await client.GetAsync($"/api/v1/admin/companies/{company.Id}/membership-requests/pending");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var views = await EventsAsync(admin.Id, Infrastructure.Security.InstallationAdminAudit.DataViewed);
		Assert.Contains(views, item => item.EntityType == "CompanyMembershipRequestsPending" && item.TenantId == company.Id);
	}
}
