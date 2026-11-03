using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

public sealed class UserAdministrationMutationTests
{
    [Fact]
    public async Task CreateUser_HashesPasswordAndAuditsWithoutGrantingGlobalRole()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        await fixture.Database.SaveChangesAsync();
        var hasher = new Infrastructure.Security.PasswordHashService();
        var request = new Server.Api.CreateUserRequest(
            "new.user", "کاربر", "جدید", "new.user@example.test", null,
            "A-Long-Unique-Phrase-1406!", "A-Long-Unique-Phrase-1406!", true);

        var result = await Server.Api.UserAdministrationEndpoints.CreateUserAsync(
            new DefaultHttpContext(), Principal(admin.Id), request, new AllowAntiforgery(),
            fixture.Database, hasher, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status201Created, (result as IStatusCodeHttpResult)?.StatusCode);
        var created = await fixture.Database.Users.SingleAsync(x => x.Username == "new.user");
        Assert.NotEqual("A-Long-Unique-Phrase-1406!", created.Password);
        Assert.Equal(Infrastructure.Security.PasswordVerificationOutcome.Success,
            hasher.Verify(created.Password, "A-Long-Unique-Phrase-1406!"));
        Assert.Null(created.RoleId);
        Assert.Contains(await fixture.Database.SecurityAuditEvents.ToListAsync(),
            audit => audit.EventType == "UserCreated" && audit.EntityId == created.Id.ToString());
    }


    [Fact]
    public async Task CreateUser_RejectsPhoneNumbersOutsideTheExistingElevenDigitContract()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        await fixture.Database.SaveChangesAsync();
        var request = new Server.Api.CreateUserRequest(
            "new.user", "کاربر", "جدید", "new.user@example.test", "091234567890",
            "A-Long-Unique-Phrase-1406!", "A-Long-Unique-Phrase-1406!", true);

        var result = await Server.Api.UserAdministrationEndpoints.CreateUserAsync(
            new DefaultHttpContext(), Principal(admin.Id), request, new AllowAntiforgery(),
            fixture.Database, new Infrastructure.Security.PasswordHashService(), TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status400BadRequest, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.False(await fixture.Database.Users.AnyAsync(x => x.Username == "new.user"));
    }

    [Fact]
    public async Task CreateUser_RejectsPasswordConfirmationMismatchAndDoesNotPersist()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        await fixture.Database.SaveChangesAsync();
        var request = new Server.Api.CreateUserRequest(
            "new.user", "کاربر", "جدید", "new.user@example.test", null,
            "A-Long-Unique-Phrase-1406!", "A-Different-Long-Phrase-1406!", true);

        var result = await Server.Api.UserAdministrationEndpoints.CreateUserAsync(
            new DefaultHttpContext(), Principal(admin.Id), request, new AllowAntiforgery(),
            fixture.Database, new Infrastructure.Security.PasswordHashService(), TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status400BadRequest, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.False(await fixture.Database.Users.AnyAsync(x => x.Username == "new.user"));
    }

    [Fact]
    public async Task UpdateUser_CannotDisableCurrentOrLastInstallationAdministrator()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        await fixture.Database.SaveChangesAsync();
        var request = new Server.Api.UpdateUserRequest(
            "admin", "مدیر", "سیستم", "admin@example.test", null, false);

        var result = await Server.Api.UserAdministrationEndpoints.UpdateUserAsync(
            admin.Id, new DefaultHttpContext(), Principal(admin.Id), request,
            new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status409Conflict, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.True((await fixture.Database.Users.SingleAsync(x => x.Id == admin.Id)).IsActive);
    }

    [Fact]
    public async Task UpdateUser_UpdatesOnlyWhitelistedProfileFieldsAndWritesAudit()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        var target = fixture.AddUser("alice", "Accountant");
        await fixture.Database.SaveChangesAsync();
        var request = new Server.Api.UpdateUserRequest(
            "alice2", "آلیس", "مثالی", "alice2@example.test", "09123456789", true);

        var result = await Server.Api.UserAdministrationEndpoints.UpdateUserAsync(
            target.Id, new DefaultHttpContext(), Principal(admin.Id), request,
            new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

        Assert.IsType<NoContent>(result);
        var updated = await fixture.Database.Users.SingleAsync(x => x.Id == target.Id);
        Assert.Equal("alice2", updated.Username);
        Assert.Equal("alice2@example.test", updated.EmailAddress);
        Assert.Null(updated.RoleId);
        Assert.Contains(await fixture.Database.SecurityAuditEvents.ToListAsync(),
            audit => audit.EventType == "UserUpdated" && audit.EntityId == target.Id.ToString());
    }




    [Fact]
    public async Task UpdateUser_ResetsContactVerificationWhenEmailOrPhoneChanges()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        var target = fixture.AddUser("alice", "Accountant");
        target.IsEmailAddressVerified = true;
        target.IsCellPhoneNumberVerified = true;
        target.CellPhoneNumber = "09123456789";
        await fixture.Database.SaveChangesAsync();
        var previousEmailVerificationKey = target.EmailAddressVerificationKey;
        var request = new Server.Api.UpdateUserRequest(
            "alice", "آلیس", "مثالی", "alice2@example.test", "09123456780", true);

        var result = await Server.Api.UserAdministrationEndpoints.UpdateUserAsync(
            target.Id, new DefaultHttpContext(), Principal(admin.Id), request,
            new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

        Assert.IsType<NoContent>(result);
        var updated = await fixture.Database.Users.SingleAsync(x => x.Id == target.Id);
        Assert.False(updated.IsEmailAddressVerified);
        Assert.False(updated.IsCellPhoneNumberVerified);
        Assert.NotEqual(previousEmailVerificationKey, updated.EmailAddressVerificationKey);
        Assert.Null(updated.CellPhoneNumberVerificationKey);
    }

    [Fact]
    public async Task UpdateUser_RejectsPhoneNumbersOutsideTheExistingElevenDigitContract()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        var target = fixture.AddUser("alice", "Accountant");
        await fixture.Database.SaveChangesAsync();
        var request = new Server.Api.UpdateUserRequest(
            "alice", "آلیس", "مثالی", "alice@example.test", "091234567890", true);

        var result = await Server.Api.UserAdministrationEndpoints.UpdateUserAsync(
            target.Id, new DefaultHttpContext(), Principal(admin.Id), request,
            new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status400BadRequest, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Null((await fixture.Database.Users.SingleAsync(x => x.Id == target.Id)).CellPhoneNumber);
    }

    [Fact]
    public async Task UpdateUser_DoesNotTreatAnOrdinaryRoleAsAnInstallationAdministrator()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        var accountantRole = new Domain.Role("Accountant") { IsActive = true };
        fixture.Database.Roles.Add(accountantRole);
        var target = fixture.AddUser("alice", "Accountant");
        target.Role = accountantRole;
        await fixture.Database.SaveChangesAsync();
        var request = new Server.Api.UpdateUserRequest(
            "alice", "آلیس", "مثالی", "alice@example.test", null, false);

        var result = await Server.Api.UserAdministrationEndpoints.UpdateUserAsync(
            target.Id, new DefaultHttpContext(), Principal(admin.Id), request,
            new AllowAntiforgery(), fixture.Database, TimeProvider.System, default);

        Assert.IsType<NoContent>(result);
        Assert.False((await fixture.Database.Users.SingleAsync(x => x.Id == target.Id)).IsActive);
    }

    [Fact]
    public async Task UpdateUser_UsesInjectedTimeProviderForAuditTimestamp()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", Constants.Role.Admin);
        var target = fixture.AddUser("alice", "Accountant");
        await fixture.Database.SaveChangesAsync();
        var expected = new DateTimeOffset(2026, 11, 3, 6, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(expected);
        var request = new Server.Api.UpdateUserRequest(
            "alice", "آلیس", "مثالی", "alice@example.test", null, true);

        var result = await Server.Api.UserAdministrationEndpoints.UpdateUserAsync(
            target.Id, new DefaultHttpContext(), Principal(admin.Id), request,
            new AllowAntiforgery(), fixture.Database, timeProvider, default);

        Assert.IsType<NoContent>(result);
        var audit = await fixture.Database.SecurityAuditEvents.SingleAsync(x => x.EventType == "UserUpdated");
        Assert.Equal(expected.UtcDateTime, audit.OccurredAtUtc);
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

        public Domain.User AddUser(string username, string roleName)
        {
            Domain.Role? role = null;
            if (roleName == Constants.Role.Admin)
            {
                role = new Domain.Role(roleName) { IsActive = true };
                Database.Roles.Add(role);
            }

            var user = new Domain.User($"{username}@example.test")
            {
                Username = username,
                FirstName = username,
                LastName = "test",
                IsActive = true,
                Role = role,
                Password = new Infrastructure.Security.PasswordHashService().Hash("Admin-Password-Phrase-1406!"),
            };
            Database.Users.Add(user);
            return user;
        }

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class AllowAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }
}
