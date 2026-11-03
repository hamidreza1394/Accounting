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
        Assert.Contains("فقط مدیر نصب سامانه مجاز به این عملیات است.", JsonSerializer.Serialize(payload.Value));

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
