using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

/// <summary>
/// D-088: every view, search and change of customer data by the installation administrator is recorded in
/// the existing audit table (actor, subject, company, time, operation, previous and new value, reason); the
/// audit records can not be changed or removed through the application; an independent, read-only report
/// lists the administrator's activity; and non-administrators neither read nor leave any record.
/// </summary>
public sealed class InstallationAdminAuditTests
{
	private static readonly string RepoRoot = FindRepoRoot();

	// ---- reads ----

	[Fact]
	public async Task ListingUsers_IsRecordedWithTheActorTheFiltersAndTheResultCount()
	{
		await using var fixture = new Fixture();

		var result = await Server.Api.UserAdministrationEndpoints.GetUsersAsync(
			Http(), fixture.AdminPrincipal(), fixture.Database, null, "all", 1, 25, null, null, default);

		Assert.IsType<Ok<Server.Api.UserListResponse>>(result);
		var recorded = Assert.Single(await fixture.EventsAsync(Infrastructure.Security.InstallationAdminAudit.DataViewed));
		Assert.Equal(fixture.Admin.Id, recorded.UserId);
		Assert.Equal("Users", recorded.EntityType);
		using var details = JsonDocument.Parse(recorded.DetailsJson!);
		Assert.Equal("Users", details.RootElement.GetProperty("resource").GetString());
		Assert.True(details.RootElement.GetProperty("resultCount").GetInt32() >= 2);
	}

	[Fact]
	public async Task SearchingUsers_IsRecordedAsASearchWithTheTerm()
	{
		await using var fixture = new Fixture();

		await Server.Api.UserAdministrationEndpoints.GetUsersAsync(
			Http(), fixture.AdminPrincipal(), fixture.Database, "target", "all", 1, 25, null, null, default);

		var recorded = Assert.Single(await fixture.EventsAsync(Infrastructure.Security.InstallationAdminAudit.DataSearched));
		Assert.Contains("target", recorded.DetailsJson);
		Assert.Empty(await fixture.EventsAsync(Infrastructure.Security.InstallationAdminAudit.DataViewed));
	}

	[Fact]
	public async Task ViewingOneUser_IsRecordedWithTheSubject_AndAMissingUserIsNot()
	{
		await using var fixture = new Fixture();

		await Server.Api.UserAdministrationEndpoints.GetUserAsync(
			fixture.Target.Id, Http(), fixture.AdminPrincipal(), fixture.Database, default);
		var missing = await Server.Api.UserAdministrationEndpoints.GetUserAsync(
			Guid.NewGuid(), Http(), fixture.AdminPrincipal(), fixture.Database, default);

		Assert.IsType<NotFound>(missing);
		var recorded = Assert.Single(await fixture.EventsAsync(Infrastructure.Security.InstallationAdminAudit.DataViewed));
		Assert.Equal("User", recorded.EntityType);
		Assert.Equal(fixture.Target.Id.ToString(), recorded.EntityId);
	}

	[Fact]
	public async Task ViewingCompaniesAndTheAuditLog_IsRecorded()
	{
		await using var fixture = new Fixture();

		await Server.Api.SecurityAuditAdministrationEndpoints.GetAdminCompaniesAsync(
			Http(), fixture.AdminPrincipal(), fixture.Database, null, 1, 25, default);
		await Server.Api.SecurityAuditAdministrationEndpoints.GetAuditEventsAsync(
			Http(), fixture.AdminPrincipal(), fixture.Database, cancellationToken: default);

		var resources = (await fixture.EventsAsync(Infrastructure.Security.InstallationAdminAudit.DataViewed))
			.Select(item => item.EntityType).OrderBy(name => name).ToArray();
		Assert.Equal(["Companies", "SecurityAuditEvents"], resources);
	}

	[Fact]
	public async Task NonAdministrators_ReadNothing_AndLeaveNoAdministratorRecord()
	{
		await using var fixture = new Fixture();
		var plain = fixture.PlainPrincipal();

		var users = await Server.Api.UserAdministrationEndpoints.GetUsersAsync(
			Http(), plain, fixture.Database, null, "all", 1, 25, null, null, default);
		var user = await Server.Api.UserAdministrationEndpoints.GetUserAsync(
			fixture.Target.Id, Http(), plain, fixture.Database, default);
		var report = await Server.Api.SecurityAuditAdministrationEndpoints.GetInstallationAdminActivityAsync(
			Http(), plain, fixture.Database, cancellationToken: default);

		foreach (var denied in new IResult[] { users, user, report })
		{
			Assert.Equal(StatusCodes.Status403Forbidden, (denied as IStatusCodeHttpResult)?.StatusCode);
		}

		Assert.Empty(await fixture.Database.SecurityAuditEvents.ToListAsync());
	}

	// ---- changes ----

	[Fact]
	public async Task UpdatingAUser_RecordsPreviousAndNewValuesTheCompanyAndTheReason_WithoutSecrets()
	{
		await using var fixture = new Fixture();
		var company = new Domain.Company { Name = "Customer Co" };
		fixture.Database.Companies.Add(company);
		await fixture.Database.SaveChangesAsync();
		fixture.Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
		{
			UserId = fixture.Target.Id,
			TenantId = company.Id,
			IsActive = true,
		});
		await fixture.Database.SaveChangesAsync();

		var result = await Server.Api.UserAdministrationEndpoints.UpdateUserAsync(
			fixture.Target.Id, Http(), fixture.AdminPrincipal(),
			new Server.Api.UpdateUserRequest("target", "New", "Name", "target@example.test", null, true, "support ticket 4711"),
			new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

		Assert.IsNotType<ProblemHttpResult>(result);
		var recorded = Assert.Single(await fixture.EventsAsync("UserUpdated"));
		Assert.Equal(fixture.Admin.Id, recorded.UserId);
		Assert.Equal(fixture.Target.Id.ToString(), recorded.EntityId);
		using var details = JsonDocument.Parse(recorded.DetailsJson!);
		var root = details.RootElement;
		Assert.Equal(fixture.Target.Id, root.GetProperty("subjectUserId").GetGuid());
		Assert.Equal("support ticket 4711", root.GetProperty("reason").GetString());
		Assert.Contains(company.Id, root.GetProperty("companyIds").EnumerateArray().Select(item => item.GetGuid()));
		var first = root.GetProperty("changes").EnumerateArray()
			.Single(change => change.GetProperty("Field").GetString() == "FirstName");
		Assert.Equal(JsonValueKind.Null, first.GetProperty("Previous").ValueKind);
		Assert.Equal("New", first.GetProperty("Current").GetString());
		// unchanged fields are not listed; no secret is ever written
		Assert.DoesNotContain(root.GetProperty("changes").EnumerateArray(),
			change => change.GetProperty("Field").GetString() == "Username");
		Assert.DoesNotContain("Password", recorded.DetailsJson, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task UpdatingAUserAsANonAdministrator_ChangesNothingAndRecordsNothing()
	{
		await using var fixture = new Fixture();

		var result = await Server.Api.UserAdministrationEndpoints.UpdateUserAsync(
			fixture.Target.Id, Http(), fixture.PlainPrincipal(),
			new Server.Api.UpdateUserRequest("hacked", "x", "y", "hacked@example.test", null, true, null),
			new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

		Assert.Equal(StatusCodes.Status403Forbidden, (result as IStatusCodeHttpResult)?.StatusCode);
		Assert.Equal("target", (await fixture.Database.Users.SingleAsync(user => user.Id == fixture.Target.Id)).Username);
		Assert.Empty(await fixture.Database.SecurityAuditEvents.ToListAsync());
	}

	// ---- independent report ----

	[Fact]
	public async Task TheActivityReport_ListsOnlyAdministratorActivity_FiltersByActor_AndIsItselfRecorded()
	{
		await using var fixture = new Fixture();
		await Server.Api.UserAdministrationEndpoints.GetUserAsync(
			fixture.Target.Id, Http(), fixture.AdminPrincipal(), fixture.Database, default);
		fixture.Database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
		{
			UserId = fixture.Target.Id,
			EventType = "LoginSucceeded",
		});
		await fixture.Database.SaveChangesAsync();

		var result = await Server.Api.SecurityAuditAdministrationEndpoints.GetInstallationAdminActivityAsync(
			Http(), fixture.AdminPrincipal(), fixture.Database, cancellationToken: default);

		var ok = Assert.IsType<Ok<Server.Api.InstallationAdminActivityListResponse>>(result);
		Assert.Single(ok.Value!.Items);
		var item = ok.Value.Items[0];
		Assert.Equal(Infrastructure.Security.InstallationAdminAudit.DataViewed, item.EventType);
		Assert.Equal("admin", item.ActorUsername);
		Assert.Equal(fixture.Target.Id.ToString(), item.EntityId);
		Assert.DoesNotContain(ok.Value.Items, row => row.EventType == "LoginSucceeded");
		Assert.Single(await fixture.EventsAsync(Infrastructure.Security.InstallationAdminAudit.DataViewed), e => e.EntityType == "InstallationAdminActivityReport");

		var other = await Server.Api.SecurityAuditAdministrationEndpoints.GetInstallationAdminActivityAsync(
			Http(), fixture.AdminPrincipal(), fixture.Database, actorId: fixture.Target.Id, cancellationToken: default);
		Assert.Empty(Assert.IsType<Ok<Server.Api.InstallationAdminActivityListResponse>>(other).Value!.Items);
	}

	// ---- audit records can not be changed or removed ----

	[Fact]
	public async Task AnExistingAuditEvent_CanNotBeModified_OrDeleted_ThroughTheApplication()
	{
		await using var fixture = new Fixture();
		var audit = new Domain.SecurityAuditEvent { UserId = fixture.Admin.Id, EventType = "UserUpdated" };
		fixture.Database.SecurityAuditEvents.Add(audit);
		await fixture.Database.SaveChangesAsync();

		audit.EventType = "Tampered";
		await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Database.SaveChangesAsync());
		fixture.Database.ChangeTracker.Clear();

		var stored = await fixture.Database.SecurityAuditEvents.SingleAsync(item => item.Id == audit.Id);
		fixture.Database.SecurityAuditEvents.Remove(stored);
		await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Database.SaveChangesAsync());
		fixture.Database.ChangeTracker.Clear();

		Assert.Equal("UserUpdated", (await fixture.Database.SecurityAuditEvents.SingleAsync(item => item.Id == audit.Id)).EventType);
		// appending stays possible
		fixture.Database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent { EventType = "Another" });
		Assert.Equal(1, await fixture.Database.SaveChangesAsync());
	}

	[Fact]
	public void NoApplicationCode_UpdatesOrDeletesAuditEventsInBulk()
	{
		var offenders = FindAuditMutations(
			Directory.EnumerateFiles(Path.Combine(RepoRoot, "Server"), "*.cs", SearchOption.AllDirectories)
				.Concat(Directory.EnumerateFiles(Path.Combine(RepoRoot, "Infrastructure"), "*.cs", SearchOption.AllDirectories))
				.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
					!path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
				.Select(path => (path, File.ReadAllText(path))));

		Assert.True(offenders.Count == 0, "audit events must stay append-only: " + string.Join(", ", offenders));
	}

	[Theory]
	[InlineData("db.SecurityAuditEvents.Remove(x);")]
	[InlineData("db.SecurityAuditEvents.RemoveRange(items);")]
	[InlineData("await db.SecurityAuditEvents.ExecuteDeleteAsync();")]
	[InlineData("await db.SecurityAuditEvents.Where(a => a.Id == id).ExecuteUpdateAsync(s => s);")]
	[InlineData("db.SecurityAuditEvents.Update(x);")]
	public void TheAppendOnlyScan_CatchesEachKindOfMutation(string source)
	{
		Assert.Single(FindAuditMutations([("fixture.cs", source)]));
	}

	private static List<string> FindAuditMutations(IEnumerable<(string Path, string Text)> files)
	{
		var pattern = new Regex(@"SecurityAuditEvents\s*(\.\s*(Remove|RemoveRange|Update|UpdateRange)\s*\(|[^;]*?\.\s*Execute(Delete|Update)(Async)?\s*\()", RegexOptions.Singleline);
		return files.Where(file => pattern.IsMatch(file.Text)).Select(file => file.Path).ToList();
	}

	// ---- helpers ----

	private static DefaultHttpContext Http() => new();

	private static string FindRepoRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null)
		{
			if (File.Exists(Path.Combine(dir.FullName, "Template.sln")) && File.Exists(Path.Combine(dir.FullName, "Server", "Server.csproj")))
			{
				return dir.FullName;
			}

			dir = dir.Parent;
		}

		throw new InvalidOperationException("Repository root not found.");
	}

	private sealed class AllowAntiforgery : IAntiforgery
	{
		public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
		public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
		public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
		public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
		public void SetCookieTokenAndHeader(HttpContext httpContext) { }
	}

	private sealed class Fixture : IAsyncDisposable
	{
		public Fixture()
		{
			Database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
				.UseInMemoryDatabase(Guid.NewGuid().ToString())
				.Options);
			Admin = AddUser("admin", Constants.Role.Admin);
			Target = AddUser("target", "Accountant");
			Plain = AddUser("plain", "Accountant");
			Database.SaveChanges();
		}

		public Data.DatabaseContext Database { get; }
		public Domain.User Admin { get; }
		public Domain.User Target { get; }
		public Domain.User Plain { get; }

		public Task<List<Domain.SecurityAuditEvent>> EventsAsync(string eventType) =>
			Database.SecurityAuditEvents.AsNoTracking().Where(item => item.EventType == eventType).ToListAsync();

		private Domain.User AddUser(string username, string roleName)
		{
			var role = Database.Roles.Local.FirstOrDefault(x => x.Name == roleName) ?? new Domain.Role(roleName) { IsActive = true };
			if (!Database.Roles.Local.Contains(role))
			{
				Database.Roles.Add(role);
			}

			var user = new Domain.User($"{username}@example.test")
			{
				Username = username,
				IsActive = true,
				Role = role,
				Password = "not-a-real-hash",
			};
			Database.Users.Add(user);
			return user;
		}

		public ClaimsPrincipal AdminPrincipal() => Principal(Admin.Id);
		public ClaimsPrincipal PlainPrincipal() => Principal(Plain.Id);

		private static ClaimsPrincipal Principal(Guid userId) => new(new ClaimsIdentity(
			[new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim("AccountKind", "User")],
			CookieAuthenticationDefaults.AuthenticationScheme));

		public ValueTask DisposeAsync() => Database.DisposeAsync();
	}
}
