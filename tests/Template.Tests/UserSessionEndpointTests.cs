using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Template.Tests;

/// <summary>
/// The self-service session lifecycle endpoints (D-069): the list shows only the active
/// sessions of the authenticated user, a revocation ends exactly one owned row (idempotent,
/// audited), revoke-others keeps the current session, and a foreign session identifier is
/// never distinguishable from an unknown one.
/// </summary>
public sealed class UserSessionEndpointTests
{
    private const string SessionIdClaimType = Infrastructure.Tenancy.WorkContextCookie.SessionIdClaimType;

    [Fact]
    public async Task MySessions_ListsOnlyTheCallerActiveSessions_AndMarksTheCurrentOne()
    {
        await using var fixture = new Fixture();
        var current = fixture.AddSession(fixture.Alice);
        var otherDevice = fixture.AddSession(fixture.Alice, lastSeenAtUtc: DateTime.UtcNow.AddMinutes(-2));
        fixture.AddSession(fixture.Alice, revoked: true);
        fixture.AddSession(fixture.Alice, lastSeenAtUtc: DateTime.UtcNow.AddMinutes(-90));
        fixture.AddSession(fixture.Bob);
        fixture.Database.SaveChanges();

        var result = await Server.Api.ApiEndpoints.GetMySessionsAsync(
            new DefaultHttpContext(), Principal(fixture.Alice.Id, current.Id),
            fixture.SessionService, default);

        var ok = Assert.IsType<Ok<IReadOnlyList<Server.Api.MySessionResponse>>>(result.Result);
        Assert.Equal([current.Id, otherDevice.Id],
            ok.Value!.Select(session => session.SessionId).ToArray());
        Assert.Equal([true, false], ok.Value!.Select(session => session.IsCurrent).ToArray());
        // No IP address and no user agent exist in the contract (D-069).
        Assert.DoesNotContain("IpAddress", typeof(Server.Api.MySessionResponse).GetProperties().Select(property => property.Name));
        Assert.DoesNotContain("UserAgent", typeof(Server.Api.MySessionResponse).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public async Task MySessions_IsUnauthorizedWithoutACompanyUserPrincipal()
    {
        await using var fixture = new Fixture();

        var anonymous = await Server.Api.ApiEndpoints.GetMySessionsAsync(
            new DefaultHttpContext(), TenantFixture.InfrastructureAccount(),
            fixture.SessionService, default);
        Assert.IsType<UnauthorizedHttpResult>(anonymous.Result);

        var noSessionClaim = await Server.Api.ApiEndpoints.RevokeMyOtherSessionsAsync(
            new DefaultHttpContext(), TenantFixture.CompanyUser(fixture.Alice.Id),
            new AllowAntiforgery(), fixture.SessionService, fixture.Database, TimeProvider.System, default);
        Assert.IsType<UnauthorizedHttpResult>(noSessionClaim);
    }

    [Fact]
    public async Task RevokeMySession_EndsTheRowWritesAuditAndIsIdempotent()
    {
        await using var fixture = new Fixture();
        var current = fixture.AddSession(fixture.Alice);
        var target = fixture.AddSession(fixture.Alice, lastSeenAtUtc: DateTime.UtcNow.AddMinutes(-2));
        fixture.Database.SaveChanges();

        var result = await Server.Api.ApiEndpoints.RevokeMySessionAsync(
            target.Id, new DefaultHttpContext(), Principal(fixture.Alice.Id, current.Id),
            new AllowAntiforgery(), fixture.SessionService, fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status204NoContent, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.NotNull((await fixture.Database.UserSessions.SingleAsync(session => session.Id == target.Id)).RevokedAtUtc);
        Assert.Null((await fixture.Database.UserSessions.SingleAsync(session => session.Id == current.Id)).RevokedAtUtc);
        var audit = await fixture.Database.SecurityAuditEvents.SingleAsync();
        Assert.Equal("UserSessionRevoked", audit.EventType);
        Assert.Equal("UserSession", audit.EntityType);
        Assert.Equal(target.Id.ToString(), audit.EntityId);
        Assert.Equal(fixture.Alice.Id, audit.UserId);

        // A second call is an idempotent no-op without a second audit event.
        var second = await Server.Api.ApiEndpoints.RevokeMySessionAsync(
            target.Id, new DefaultHttpContext(), Principal(fixture.Alice.Id, current.Id),
            new AllowAntiforgery(), fixture.SessionService, fixture.Database, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status204NoContent, (second as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(1, await fixture.Database.SecurityAuditEvents.CountAsync());
    }

    [Fact]
    public async Task RevokeMySession_Returns404ForAForeignOrUnknownSessionWithoutAudit()
    {
        await using var fixture = new Fixture();
        var current = fixture.AddSession(fixture.Alice);
        var foreign = fixture.AddSession(fixture.Bob);
        fixture.Database.SaveChanges();

        var unknown = await Server.Api.ApiEndpoints.RevokeMySessionAsync(
            Guid.NewGuid(), new DefaultHttpContext(), Principal(fixture.Alice.Id, current.Id),
            new AllowAntiforgery(), fixture.SessionService, fixture.Database, TimeProvider.System, default);
        var foreignResult = await Server.Api.ApiEndpoints.RevokeMySessionAsync(
            foreign.Id, new DefaultHttpContext(), Principal(fixture.Alice.Id, current.Id),
            new AllowAntiforgery(), fixture.SessionService, fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status404NotFound, (unknown as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, (foreignResult as IStatusCodeHttpResult)?.StatusCode);
        Assert.Null((await fixture.Database.UserSessions.SingleAsync(session => session.Id == foreign.Id)).RevokedAtUtc);
        Assert.False(await fixture.Database.SecurityAuditEvents.AnyAsync());
    }

    [Fact]
    public async Task RevokeMyOtherSessions_KeepsTheCurrentOneEndsTheRestAndWritesOneAudit()
    {
        await using var fixture = new Fixture();
        var current = fixture.AddSession(fixture.Alice);
        var firstOther = fixture.AddSession(fixture.Alice, lastSeenAtUtc: DateTime.UtcNow.AddMinutes(-2));
        var alreadyRevoked = fixture.AddSession(fixture.Alice, revoked: true);
        fixture.AddSession(fixture.Bob);
        fixture.Database.SaveChanges();

        var result = await Server.Api.ApiEndpoints.RevokeMyOtherSessionsAsync(
            new DefaultHttpContext(), Principal(fixture.Alice.Id, current.Id),
            new AllowAntiforgery(), fixture.SessionService, fixture.Database, TimeProvider.System, default);

        var ok = Assert.IsType<Ok<Server.Api.RevokeOthersResponse>>(result);
        Assert.Equal(1, ok.Value!.RevokedCount);
        Assert.Null((await fixture.Database.UserSessions.SingleAsync(session => session.Id == current.Id)).RevokedAtUtc);
        Assert.NotNull((await fixture.Database.UserSessions.SingleAsync(session => session.Id == firstOther.Id)).RevokedAtUtc);
        Assert.Equal("revoked_others",
            (await fixture.Database.UserSessions.SingleAsync(session => session.Id == firstOther.Id)).RevocationReasonCode);
        // An already-revoked session is not touched again: its reason stays as it was.
        Assert.Equal("self_revoked",
            (await fixture.Database.UserSessions.SingleAsync(session => session.Id == alreadyRevoked.Id)).RevocationReasonCode);
        var audit = await fixture.Database.SecurityAuditEvents.SingleAsync();
        Assert.Equal("UserSessionsRevokedOthers", audit.EventType);
        Assert.Contains("revokedCount", audit.DetailsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SessionMutationEndpoints_RejectAnInvalidAntiforgeryTokenWithoutRevoking()
    {
        await using var fixture = new Fixture();
        var current = fixture.AddSession(fixture.Alice);
        var target = fixture.AddSession(fixture.Alice, lastSeenAtUtc: DateTime.UtcNow.AddMinutes(-2));
        fixture.Database.SaveChanges();

        var single = await Server.Api.ApiEndpoints.RevokeMySessionAsync(
            target.Id, new DefaultHttpContext(), Principal(fixture.Alice.Id, current.Id),
            new DenyAntiforgery(), fixture.SessionService, fixture.Database, TimeProvider.System, default);
        var others = await Server.Api.ApiEndpoints.RevokeMyOtherSessionsAsync(
            new DefaultHttpContext(), Principal(fixture.Alice.Id, current.Id),
            new DenyAntiforgery(), fixture.SessionService, fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status400BadRequest, (single as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, (others as IStatusCodeHttpResult)?.StatusCode);
        Assert.Null((await fixture.Database.UserSessions.SingleAsync(session => session.Id == target.Id)).RevokedAtUtc);
        Assert.False(await fixture.Database.SecurityAuditEvents.AnyAsync());
    }

    [Fact]
    public async Task RevokingTheCurrentSession_IsAllowedLikeAnyOtherDevice()
    {
        await using var fixture = new Fixture();
        var current = fixture.AddSession(fixture.Alice);
        fixture.Database.SaveChanges();

        var result = await Server.Api.ApiEndpoints.RevokeMySessionAsync(
            current.Id, HttpContextWithAuthentication(), Principal(fixture.Alice.Id, current.Id),
            new AllowAntiforgery(), fixture.SessionService, fixture.Database, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status204NoContent, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.NotNull((await fixture.Database.UserSessions.SingleAsync(session => session.Id == current.Id)).RevokedAtUtc);
    }

    private static DefaultHttpContext HttpContextWithAuthentication()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton<Microsoft.AspNetCore.Authentication.IAuthenticationService, NoopAuthenticationService>()
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        return context;
    }

    private sealed class NoopAuthenticationService : Microsoft.AspNetCore.Authentication.IAuthenticationService
    {
        public Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? scheme, Microsoft.AspNetCore.Authentication.AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, Microsoft.AspNetCore.Authentication.AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, Microsoft.AspNetCore.Authentication.AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, Microsoft.AspNetCore.Authentication.AuthenticationProperties? properties) => Task.CompletedTask;
    }

    private static ClaimsPrincipal Principal(Guid userId, Guid sessionId) => new(
        new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("AccountKind", "User"),
            new Claim(SessionIdClaimType, sessionId.ToString()),
        ], CookieAuthenticationDefaults.AuthenticationScheme));

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
            Alice = AddUser("alice");
            Bob = AddUser("bob");
            Database.SaveChanges();
            SessionService = new Infrastructure.Security.UserSessionService(Database, TimeProvider.System);
        }

        public Data.DatabaseContext Database { get; }
        public Infrastructure.Security.UserSessionService SessionService { get; }
        public Domain.User Alice { get; }
        public Domain.User Bob { get; }

        private Domain.User AddUser(string username)
        {
            var user = new Domain.User($"{username}@example.test")
            {
                Username = username,
                IsActive = true,
            };
            Database.Users.Add(user);
            return user;
        }

        public Domain.UserSession AddSession(
            Domain.User user,
            bool revoked = false,
            DateTime? lastSeenAtUtc = null)
        {
            var session = new Domain.UserSession
            {
                UserId = user.Id,
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = lastSeenAtUtc ?? DateTime.UtcNow,
            };

            if (revoked)
            {
                session.RevokedAtUtc = DateTime.UtcNow;
                session.RevocationReasonCode = "self_revoked";
            }

            Database.UserSessions.Add(session);
            return session;
        }

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }

    private sealed class AllowAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }

    private sealed class DenyAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(false);
        public Task ValidateRequestAsync(HttpContext httpContext) => throw new AntiforgeryValidationException("denied");
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }
}
