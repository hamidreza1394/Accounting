using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

public sealed class UserAdministrationEndpointTests
{
    [Fact]
    public async Task ListUsers_RequiresAnActiveInstallationAdministratorFromTheDatabase()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", "مدیر", "سیستم", Constants.Role.Admin);
        var ordinary = fixture.AddUser("accountant", "کاربر", "عادی", "Accountant");
        await fixture.Database.SaveChangesAsync();

        var denied = await Server.Api.UserAdministrationEndpoints.GetUsersAsync(
            new DefaultHttpContext(), Principal(ordinary.Id), fixture.Database,
            search: null, status: "all", page: 1, pageSize: 25, sort: "created",
            descending: true, cancellationToken: default);
        Assert.Equal(StatusCodes.Status403Forbidden, (denied as IStatusCodeHttpResult)?.StatusCode);
        var payload = Assert.IsAssignableFrom<IValueHttpResult>(denied);
        using var errorDocument = JsonDocument.Parse(JsonSerializer.Serialize(payload.Value));
        Assert.Equal("فقط مدیر نصب سامانه مجاز به این عملیات است.", errorDocument.RootElement.GetProperty("error").GetString());

        var allowed = await Server.Api.UserAdministrationEndpoints.GetUsersAsync(
            new DefaultHttpContext(), Principal(admin.Id), fixture.Database,
            search: null, status: "all", page: 1, pageSize: 25, sort: "created",
            descending: true, cancellationToken: default);
        var ok = Assert.IsType<Ok<Server.Api.UserListResponse>>(allowed);
        Assert.Equal(2, ok.Value!.TotalCount);
    }

    [Fact]
    public async Task ListUsers_FiltersAndPaginatesOnTheServer()
    {
        await using var fixture = new Fixture();
        fixture.AddUser("alice", "آلیس", "مثالی", Constants.Role.Admin);
        fixture.AddUser("bob", "باب", "نمونه", "Accountant");
        fixture.AddUser("charlie", "چارلی", "آزمایشی", "Accountant");
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.UserAdministrationEndpoints.GetUsersAsync(
            new DefaultHttpContext(), Principal(fixture.AdminId), fixture.Database,
            search: "bob", status: "active", page: 1, pageSize: 1, sort: "username",
            descending: false, cancellationToken: default);

        var ok = Assert.IsType<Ok<Server.Api.UserListResponse>>(result);
        Assert.Equal(1, ok.Value!.TotalCount);
        Assert.Single(ok.Value.Items);
        Assert.Equal("bob", ok.Value.Items[0].Username);
    }

    [Fact]
    public async Task ListUsers_PutsTheNewestCreatedUserFirstByDefault_WithAStableTiebreak()
    {
        await using var fixture = new Fixture();
        var oldest = fixture.AddUser("oldest", "قدیمی", "اول", Constants.Role.Admin);
        var middle = fixture.AddUser("middle", "میانی", "دوم", "Accountant");
        var newest = fixture.AddUser("newest", "جدید", "سوم", "Accountant");
        fixture.Database.Entry(oldest).Property("InsertDateTime").CurrentValue = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        fixture.Database.Entry(middle).Property("InsertDateTime").CurrentValue = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        fixture.Database.Entry(newest).Property("InsertDateTime").CurrentValue = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        await fixture.Database.SaveChangesAsync();

        // No sort and no direction: the newest-created user comes first.
        var byDefault = await Server.Api.UserAdministrationEndpoints.GetUsersAsync(
            new DefaultHttpContext(), Principal(fixture.AdminId), fixture.Database,
            search: null, status: "all", page: 1, pageSize: 25, sort: null,
            descending: null, cancellationToken: default);
        var defaultOrder = Assert.IsType<Ok<Server.Api.UserListResponse>>(byDefault).Value!.Items.Select(item => item.Username);
        Assert.Equal(new[] { "newest", "middle", "oldest" }, defaultOrder);

        // The newest user is on page 1 even with a small page size, and an explicit ascending order still works.
        var firstPage = await Server.Api.UserAdministrationEndpoints.GetUsersAsync(
            new DefaultHttpContext(), Principal(fixture.AdminId), fixture.Database,
            search: null, status: "all", page: 1, pageSize: 1, sort: "created",
            descending: null, cancellationToken: default);
        Assert.Equal("newest", Assert.IsType<Ok<Server.Api.UserListResponse>>(firstPage).Value!.Items.Single().Username);

        var ascending = await Server.Api.UserAdministrationEndpoints.GetUsersAsync(
            new DefaultHttpContext(), Principal(fixture.AdminId), fixture.Database,
            search: null, status: "all", page: 1, pageSize: 25, sort: "created",
            descending: false, cancellationToken: default);
        Assert.Equal("oldest", Assert.IsType<Ok<Server.Api.UserListResponse>>(ascending).Value!.Items.First().Username);
    }

    [Fact]
    public async Task ListUsers_ListsAUserWithoutEmail_AndTheSearchSkipsTheMissingEmail()
    {
        await using var fixture = new Fixture();
        fixture.AddUser("admin", "مدیر", "سیستم", Constants.Role.Admin);
        var noEmail = fixture.AddUser("noemail", "بدون", "ایمیل", "Accountant");
        noEmail.EmailAddress = null;
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.UserAdministrationEndpoints.GetUsersAsync(
            new DefaultHttpContext(), Principal(fixture.AdminId), fixture.Database,
            search: "example", status: "all", page: 1, pageSize: 25, sort: "created",
            descending: true, cancellationToken: default);

        var items = Assert.IsType<Ok<Server.Api.UserListResponse>>(result).Value!.Items;
        Assert.DoesNotContain(items, item => item.Username == "noemail");

        var byName = await Server.Api.UserAdministrationEndpoints.GetUsersAsync(
            new DefaultHttpContext(), Principal(fixture.AdminId), fixture.Database,
            search: "noemail", status: "all", page: 1, pageSize: 25, sort: "created",
            descending: true, cancellationToken: default);
        var found = Assert.Single(Assert.IsType<Ok<Server.Api.UserListResponse>>(byName).Value!.Items);
        Assert.Null(found.EmailAddress);
    }

    [Fact]
    public async Task GetUser_ReturnsNotFoundForUnknownIdAndNeverSerializesPassword()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", "مدیر", "سیستم", Constants.Role.Admin);
        var target = fixture.AddUser("alice", "آلیس", "مثالی", "Accountant");
        await fixture.Database.SaveChangesAsync();

        var missing = await Server.Api.UserAdministrationEndpoints.GetUserAsync(
            Guid.NewGuid(), new DefaultHttpContext(), Principal(admin.Id), fixture.Database, default);
        Assert.IsType<NotFound>(missing);

        var result = await Server.Api.UserAdministrationEndpoints.GetUserAsync(
            target.Id, new DefaultHttpContext(), Principal(admin.Id), fixture.Database, default);
        var ok = Assert.IsType<Ok<Server.Api.UserDetailsResponse>>(result);
        Assert.Equal("alice", ok.Value!.Username);
        Assert.DoesNotContain("Password", typeof(Server.Api.UserDetailsResponse).GetProperties().Select(x => x.Name));
    }

    [Fact]
    public async Task GetUserLoginHistory_RequiresAnActiveInstallationAdministratorFromTheDatabase()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", "مدیر", "سیستم", Constants.Role.Admin);
        var ordinary = fixture.AddUser("accountant", "کاربر", "عادی", "Accountant");
        await fixture.Database.SaveChangesAsync();

        var denied = await Server.Api.UserAdministrationEndpoints.GetUserLoginHistoryAsync(
            ordinary.Id, new DefaultHttpContext(), Principal(ordinary.Id), fixture.Database,
            cancellationToken: default);
        Assert.Equal(StatusCodes.Status403Forbidden, (denied as IStatusCodeHttpResult)?.StatusCode);

        var allowed = await Server.Api.UserAdministrationEndpoints.GetUserLoginHistoryAsync(
            ordinary.Id, new DefaultHttpContext(), Principal(admin.Id), fixture.Database,
            cancellationToken: default);
        Assert.IsType<Ok<IReadOnlyList<Server.Api.LoginHistoryItemResponse>>>(allowed);
    }

    [Fact]
    public async Task GetUserLoginHistory_ReturnsOnlyAuthenticationEventsOfTheTargetUser_NewestFirst()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", "مدیر", "سیستم", Constants.Role.Admin);
        var target = fixture.AddUser("alice", "آلیس", "مثالی", "Accountant");
        var stranger = fixture.AddUser("bob", "باب", "نمونه", "Accountant");
        await fixture.Database.SaveChangesAsync();

        var baseTime = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        fixture.Database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        { UserId = target.Id, EventType = "LoginSucceeded", OccurredAtUtc = baseTime, IpAddress = "10.0.0.1" });
        fixture.Database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        { UserId = target.Id, EventType = "LoginFailed", OccurredAtUtc = baseTime.AddHours(1), IpAddress = "10.0.0.2" });
        fixture.Database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        { UserId = target.Id, EventType = "LogoutSucceeded", OccurredAtUtc = baseTime.AddHours(2), IpAddress = "10.0.0.3" });
        // A non-authentication audit event of the target user must never leak into the login history.
        fixture.Database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        { UserId = target.Id, EventType = "CompanyRoleUpdated", OccurredAtUtc = baseTime.AddHours(3) });
        // Another user's events must not leak either.
        fixture.Database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        { UserId = stranger.Id, EventType = "LoginSucceeded", OccurredAtUtc = baseTime.AddHours(4) });
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.UserAdministrationEndpoints.GetUserLoginHistoryAsync(
            target.Id, new DefaultHttpContext(), Principal(admin.Id), fixture.Database, take: 2,
            cancellationToken: default);
        var ok = Assert.IsType<Ok<IReadOnlyList<Server.Api.LoginHistoryItemResponse>>>(result);
        Assert.Equal(2, ok.Value!.Count);
        Assert.Equal("LogoutSucceeded", ok.Value[0].EventType);
        Assert.Equal("LoginFailed", ok.Value[1].EventType);
        Assert.Equal(DateTimeKind.Utc, ok.Value[0].OccurredAtUtc.Kind);
    }

    [Fact]
    public async Task GetUserLoginHistory_ReturnsNotFoundForUnknownOrEmptyUser()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", "مدیر", "سیستم", Constants.Role.Admin);
        await fixture.Database.SaveChangesAsync();

        var unknown = await Server.Api.UserAdministrationEndpoints.GetUserLoginHistoryAsync(
            Guid.NewGuid(), new DefaultHttpContext(), Principal(admin.Id), fixture.Database,
            cancellationToken: default);
        Assert.IsType<NotFound>(unknown);

        var empty = await Server.Api.UserAdministrationEndpoints.GetUserLoginHistoryAsync(
            Guid.Empty, new DefaultHttpContext(), Principal(admin.Id), fixture.Database,
            cancellationToken: default);
        Assert.IsType<NotFound>(empty);
    }

    [Fact]
    public async Task GetUserMembershipRequests_RequiresAnActiveInstallationAdministratorFromTheDatabase()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", "مدیر", "سیستم", Constants.Role.Admin);
        var target = fixture.AddUser("alice", "آلیس", "مثالی", "Accountant");
        await fixture.Database.SaveChangesAsync();

        var denied = await Server.Api.UserAdministrationEndpoints.GetUserMembershipRequestsAsync(
            target.Id, new DefaultHttpContext(), Principal(target.Id), fixture.Database,
            cancellationToken: default);
        Assert.Equal(StatusCodes.Status403Forbidden, (denied as IStatusCodeHttpResult)?.StatusCode);

        var allowed = await Server.Api.UserAdministrationEndpoints.GetUserMembershipRequestsAsync(
            target.Id, new DefaultHttpContext(), Principal(admin.Id), fixture.Database,
            cancellationToken: default);
        Assert.IsType<Ok<Server.Api.AdminUserMembershipRequestListResponse>>(allowed);
    }

    [Fact]
    public async Task GetUserMembershipRequests_ReturnsOnlyTheTargetUsersRequests_NewestFirst_WithDecisionDetails()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", "مدیر", "سیستم", Constants.Role.Admin);
        var target = fixture.AddUser("alice", "آلیس", "مثالی", "Accountant");
        var stranger = fixture.AddUser("bob", "باب", "نمونه", "Accountant");
        var pendingCompany = new Domain.Company { Name = "شرکت مقصد", IsActive = true };
        var rejectedCompany = new Domain.Company { Name = "شرکت ردشده", IsActive = true };
        fixture.Database.Companies.AddRange(pendingCompany, rejectedCompany);
        await fixture.Database.SaveChangesAsync();

        var baseTime = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        fixture.Database.CompanyMembershipRequests.Add(new Domain.CompanyMembershipRequest
        {
            UserId = target.Id, DestinationTenantId = rejectedCompany.Id,
            Status = Domain.CompanyMembershipRequestStatuses.Rejected,
            RequestedAtUtc = baseTime, DecidedAtUtc = baseTime.AddHours(2),
            DecidedByUserId = admin.Id, DecisionReason = "درخواست در دورهٔ بسته بررسی شد",
        });
        fixture.Database.CompanyMembershipRequests.Add(new Domain.CompanyMembershipRequest
        {
            UserId = target.Id, DestinationTenantId = pendingCompany.Id,
            Status = Domain.CompanyMembershipRequestStatuses.Pending,
            RequestedAtUtc = baseTime.AddHours(5),
        });
        // Another user's request must never leak into this projection.
        fixture.Database.CompanyMembershipRequests.Add(new Domain.CompanyMembershipRequest
        {
            UserId = stranger.Id, DestinationTenantId = pendingCompany.Id,
            Status = Domain.CompanyMembershipRequestStatuses.Pending,
            RequestedAtUtc = baseTime.AddHours(6),
        });
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.UserAdministrationEndpoints.GetUserMembershipRequestsAsync(
            target.Id, new DefaultHttpContext(), Principal(admin.Id), fixture.Database,
            cancellationToken: default);
        var ok = Assert.IsType<Ok<Server.Api.AdminUserMembershipRequestListResponse>>(result);
        Assert.Equal(2, ok.Value!.TotalCount);
        Assert.Equal(2, ok.Value.Items.Count);
        Assert.Equal("Pending", ok.Value.Items[0].Status);
        Assert.Equal("شرکت مقصد", ok.Value.Items[0].DestinationCompanyName);
        Assert.Null(ok.Value.Items[0].DecisionReason);
        Assert.Equal("Rejected", ok.Value.Items[1].Status);
        Assert.Equal("شرکت ردشده", ok.Value.Items[1].DestinationCompanyName);
        Assert.Equal("درخواست در دورهٔ بسته بررسی شد", ok.Value.Items[1].DecisionReason);
        Assert.Equal("مدیر سیستم", ok.Value.Items[1].DecidedByDisplayName);
        Assert.Equal(DateTimeKind.Utc, ok.Value.Items[0].RequestedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, ok.Value.Items[1].DecidedAtUtc!.Value.Kind);
    }

    [Fact]
    public async Task GetUserMembershipRequests_FiltersByTheStatusAllowlist()
    {
        await using var fixture = new Fixture();
        fixture.AddUser("admin", "مدیر", "سیستم", Constants.Role.Admin);
        var target = fixture.AddUser("alice", "آلیس", "مثالی", "Accountant");
        var company = new Domain.Company { Name = "شرکت مقصد", IsActive = true };
        fixture.Database.Companies.Add(company);
        await fixture.Database.SaveChangesAsync();

        var baseTime = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        fixture.Database.CompanyMembershipRequests.Add(new Domain.CompanyMembershipRequest
        {
            UserId = target.Id, DestinationTenantId = company.Id,
            Status = Domain.CompanyMembershipRequestStatuses.Pending, RequestedAtUtc = baseTime,
        });
        fixture.Database.CompanyMembershipRequests.Add(new Domain.CompanyMembershipRequest
        {
            UserId = target.Id, DestinationTenantId = company.Id,
            Status = Domain.CompanyMembershipRequestStatuses.Rejected, RequestedAtUtc = baseTime.AddHours(1),
        });
        await fixture.Database.SaveChangesAsync();

        var pending = await Server.Api.UserAdministrationEndpoints.GetUserMembershipRequestsAsync(
            target.Id, new DefaultHttpContext(), Principal(fixture.AdminId), fixture.Database,
            status: "Pending", cancellationToken: default);
        var pendingOk = Assert.IsType<Ok<Server.Api.AdminUserMembershipRequestListResponse>>(pending);
        Assert.Equal(1, pendingOk.Value!.TotalCount);
        Assert.Equal("Pending", pendingOk.Value.Items[0].Status);

        var invalid = await Server.Api.UserAdministrationEndpoints.GetUserMembershipRequestsAsync(
            target.Id, new DefaultHttpContext(), Principal(fixture.AdminId), fixture.Database,
            status: "Expired", cancellationToken: default);
        Assert.Equal(StatusCodes.Status400BadRequest, (invalid as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task GetUserMembershipRequests_ReturnsNotFoundForUnknownOrEmptyUser()
    {
        await using var fixture = new Fixture();
        fixture.AddUser("admin", "مدیر", "سیستم", Constants.Role.Admin);
        await fixture.Database.SaveChangesAsync();

        var unknown = await Server.Api.UserAdministrationEndpoints.GetUserMembershipRequestsAsync(
            Guid.NewGuid(), new DefaultHttpContext(), Principal(fixture.AdminId), fixture.Database,
            cancellationToken: default);
        Assert.IsType<NotFound>(unknown);

        var empty = await Server.Api.UserAdministrationEndpoints.GetUserMembershipRequestsAsync(
            Guid.Empty, new DefaultHttpContext(), Principal(fixture.AdminId), fixture.Database,
            cancellationToken: default);
        Assert.IsType<NotFound>(empty);
    }

    private static ClaimsPrincipal Principal(Guid userId) => new(
        new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("AccountKind", "User"),
        ], "test"));

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        }

        public Data.DatabaseContext Database { get; }
        public Guid AdminId { get; private set; }

        public Domain.User AddUser(string username, string firstName, string lastName, string roleName)
        {
            var role = Database.Roles.Local.FirstOrDefault(x => x.Name == roleName)
                ?? new Domain.Role(roleName) { IsActive = true };
            if (role.Id == Guid.Empty || !Database.Roles.Local.Contains(role))
                Database.Roles.Add(role);

            var user = new Domain.User($"{username}@example.test")
            {
                Username = username,
                FirstName = firstName,
                LastName = lastName,
                IsActive = true,
                Role = role,
                Password = "not-a-real-hash",
            };
            Database.Users.Add(user);
            if (roleName == Constants.Role.Admin)
                AdminId = user.Id;
            return user;
        }

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}
