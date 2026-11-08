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
