using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Template.Tests;

/// <summary>
/// The installation-administrator session revocation endpoints (D-073 §1): only an active
/// administrator re-read from the database acts, every mutation re-verifies the
/// administrator's own current password (step-up), the reason comes from the server-owned
/// catalog, revocations are idempotent and audited without secrets, and the projection
/// never carries IP, user agent, cookies or tokens.
/// </summary>
public sealed class AdminUserSessionEndpointTests
{
    private const string CorrectPassword = "correct-horse-battery-staple-42";
    private const string Reason = "admin_revoked_suspected_compromise";
    private const string SessionIdClaimType = Infrastructure.Tenancy.WorkContextCookie.SessionIdClaimType;

    [Fact]
    public async Task List_ShowsOnlyTheTargetActiveSessions_WithTheAllowlistProjection()
    {
        await using var fixture = new Fixture();
        var first = fixture.AddSession(fixture.Target);
        var second = fixture.AddSession(fixture.Target, lastSeenAtUtc: DateTime.UtcNow.AddMinutes(-2));
        fixture.AddSession(fixture.Target, revoked: true);
        fixture.AddSession(fixture.Target, lastSeenAtUtc: DateTime.UtcNow.AddMinutes(-90));
        fixture.AddSession(fixture.Admin);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.AdminUserSessionEndpoints.ListUserSessionsAsync(
            fixture.Target.Id, new DefaultHttpContext(), fixture.AdminPrincipal(),
            fixture.Database, fixture.SessionService, default);

        var ok = Assert.IsType<Ok<List<Server.Api.AdminUserSessionListItem>>>(result);
        Assert.Equal([first.Id, second.Id], ok.Value!.Select(session => session.SessionId).ToArray());
        // D-069 allowlist: no IP address and no user agent in the contract.
        Assert.DoesNotContain("IpAddress", typeof(Server.Api.AdminUserSessionListItem).GetProperties().Select(property => property.Name));
        Assert.DoesNotContain("UserAgent", typeof(Server.Api.AdminUserSessionListItem).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public async Task List_IsDeniedWithoutAnActiveInstallationAdministrator()
    {
        await using var fixture = new Fixture();
        await fixture.Database.SaveChangesAsync();

        // The infrastructure account is not a company user at all.
        var anonymous = await Server.Api.AdminUserSessionEndpoints.ListUserSessionsAsync(
            fixture.Target.Id, new DefaultHttpContext(), TenantFixture.InfrastructureAccount(),
            fixture.Database, fixture.SessionService, default);
        Assert.IsType<UnauthorizedHttpResult>(anonymous);

        // An authenticated company user without the administrator role never passes.
        var denied = await Server.Api.AdminUserSessionEndpoints.ListUserSessionsAsync(
            fixture.Target.Id, new DefaultHttpContext(), fixture.PlainPrincipal(fixture.Plain.Id),
            fixture.Database, fixture.SessionService, default);
        Assert.Equal(StatusCodes.Status403Forbidden, (denied as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task RevokeSingle_EndsTheRowWritesTheReasonAndAudits_AndIsIdempotent()
    {
        await using var fixture = new Fixture();
        var current = fixture.AddSession(fixture.Admin);
        var target = fixture.AddSession(fixture.Target, lastSeenAtUtc: DateTime.UtcNow.AddMinutes(-2));
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.AdminUserSessionEndpoints.RevokeUserSessionAsync(
            fixture.Target.Id, target.Id, new DefaultHttpContext(), fixture.AdminPrincipal(current.Id),
            new Server.Api.AdminRevokeSessionRequest(CorrectPassword, Reason),
            new AllowAntiforgery(), fixture.Database, fixture.SessionService,
            fixture.PasswordHashService, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status204NoContent, (result as IStatusCodeHttpResult)?.StatusCode);
        var row = await fixture.Database.UserSessions.SingleAsync(session => session.Id == target.Id);
        Assert.NotNull(row.RevokedAtUtc);
        Assert.Equal(Reason, row.RevocationReasonCode);
        Assert.Equal(fixture.Admin.Id, row.RevokedByUserId);
        var audit = await fixture.Database.SecurityAuditEvents.SingleAsync();
        Assert.Equal("AdminUserSessionRevoked", audit.EventType);
        Assert.Equal("UserSession", audit.EntityType);
        Assert.Equal(target.Id.ToString(), audit.EntityId);
        Assert.Equal(fixture.Admin.Id, audit.UserId);
        using var details = JsonDocument.Parse(audit.DetailsJson!);
        Assert.Equal(fixture.Target.Id, details.RootElement.GetProperty("targetUserId").GetGuid());
        Assert.Equal(Reason, details.RootElement.GetProperty("reasonCode").GetString());
        Assert.DoesNotContain(CorrectPassword, audit.DetailsJson, StringComparison.Ordinal);

        // A second call is an idempotent no-op without a second audit event.
        var second = await Server.Api.AdminUserSessionEndpoints.RevokeUserSessionAsync(
            fixture.Target.Id, target.Id, new DefaultHttpContext(), fixture.AdminPrincipal(current.Id),
            new Server.Api.AdminRevokeSessionRequest(CorrectPassword, Reason),
            new AllowAntiforgery(), fixture.Database, fixture.SessionService,
            fixture.PasswordHashService, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status204NoContent, (second as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(1, await fixture.Database.SecurityAuditEvents.CountAsync());
    }

    [Fact]
    public async Task RevokeSingle_Returns404ForAForeignOrUnknownSession_WithoutAudit()
    {
        await using var fixture = new Fixture();
        var current = fixture.AddSession(fixture.Admin);
        var foreign = fixture.AddSession(fixture.Plain);
        await fixture.Database.SaveChangesAsync();

        var unknown = await Server.Api.AdminUserSessionEndpoints.RevokeUserSessionAsync(
            fixture.Target.Id, Guid.NewGuid(), new DefaultHttpContext(), fixture.AdminPrincipal(current.Id),
            new Server.Api.AdminRevokeSessionRequest(CorrectPassword, Reason),
            new AllowAntiforgery(), fixture.Database, fixture.SessionService,
            fixture.PasswordHashService, TimeProvider.System, default);
        var foreignResult = await Server.Api.AdminUserSessionEndpoints.RevokeUserSessionAsync(
            fixture.Target.Id, foreign.Id, new DefaultHttpContext(), fixture.AdminPrincipal(current.Id),
            new Server.Api.AdminRevokeSessionRequest(CorrectPassword, Reason),
            new AllowAntiforgery(), fixture.Database, fixture.SessionService,
            fixture.PasswordHashService, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status404NotFound, (unknown as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, (foreignResult as IStatusCodeHttpResult)?.StatusCode);
        Assert.Null((await fixture.Database.UserSessions.SingleAsync(session => session.Id == foreign.Id)).RevokedAtUtc);
        Assert.False(await fixture.Database.SecurityAuditEvents.AnyAsync());
    }

    [Fact]
    public async Task RevokeSingle_RejectsAFailedStepUp_WithoutRevokingOrAuditing()
    {
        await using var fixture = new Fixture();
        var current = fixture.AddSession(fixture.Admin);
        var target = fixture.AddSession(fixture.Target, lastSeenAtUtc: DateTime.UtcNow.AddMinutes(-2));
        await fixture.Database.SaveChangesAsync();

        var wrong = await Server.Api.AdminUserSessionEndpoints.RevokeUserSessionAsync(
            fixture.Target.Id, target.Id, new DefaultHttpContext(), fixture.AdminPrincipal(current.Id),
            new Server.Api.AdminRevokeSessionRequest("totally-wrong-password", Reason),
            new AllowAntiforgery(), fixture.Database, fixture.SessionService,
            fixture.PasswordHashService, TimeProvider.System, default);
        var missing = await Server.Api.AdminUserSessionEndpoints.RevokeUserSessionAsync(
            fixture.Target.Id, target.Id, new DefaultHttpContext(), fixture.AdminPrincipal(current.Id),
            new Server.Api.AdminRevokeSessionRequest(null, Reason),
            new AllowAntiforgery(), fixture.Database, fixture.SessionService,
            fixture.PasswordHashService, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status403Forbidden, (wrong as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(StatusCodes.Status403Forbidden, (missing as IStatusCodeHttpResult)?.StatusCode);
        Assert.Null((await fixture.Database.UserSessions.SingleAsync(session => session.Id == target.Id)).RevokedAtUtc);
        Assert.False(await fixture.Database.SecurityAuditEvents.AnyAsync());
    }

    [Fact]
    public async Task RevokeSingle_RejectsAnUnknownReasonCode_AndInvalidAntiforgery()
    {
        await using var fixture = new Fixture();
        var current = fixture.AddSession(fixture.Admin);
        var target = fixture.AddSession(fixture.Target, lastSeenAtUtc: DateTime.UtcNow.AddMinutes(-2));
        await fixture.Database.SaveChangesAsync();

        var badReason = await Server.Api.AdminUserSessionEndpoints.RevokeUserSessionAsync(
            fixture.Target.Id, target.Id, new DefaultHttpContext(), fixture.AdminPrincipal(current.Id),
            new Server.Api.AdminRevokeSessionRequest(CorrectPassword, "made_up_reason"),
            new AllowAntiforgery(), fixture.Database, fixture.SessionService,
            fixture.PasswordHashService, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status400BadRequest, (badReason as IStatusCodeHttpResult)?.StatusCode);

        var denied = await Server.Api.AdminUserSessionEndpoints.RevokeUserSessionAsync(
            fixture.Target.Id, target.Id, new DefaultHttpContext(), fixture.AdminPrincipal(current.Id),
            new Server.Api.AdminRevokeSessionRequest(CorrectPassword, Reason),
            new DenyAntiforgery(), fixture.Database, fixture.SessionService,
            fixture.PasswordHashService, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status400BadRequest, (denied as IStatusCodeHttpResult)?.StatusCode);

        Assert.Null((await fixture.Database.UserSessions.SingleAsync(session => session.Id == target.Id)).RevokedAtUtc);
        Assert.False(await fixture.Database.SecurityAuditEvents.AnyAsync());
    }

    [Fact]
    public async Task RevokeAll_EndsEveryActiveSessionOfTheTarget_KeepsOthers_AndIsIdempotent()
    {
        await using var fixture = new Fixture();
        var current = fixture.AddSession(fixture.Admin);
        var first = fixture.AddSession(fixture.Target);
        var second = fixture.AddSession(fixture.Target, lastSeenAtUtc: DateTime.UtcNow.AddMinutes(-2));
        var alreadyRevoked = fixture.AddSession(fixture.Target, revoked: true);
        var otherUser = fixture.AddSession(fixture.Plain);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.AdminUserSessionEndpoints.RevokeAllUserSessionsAsync(
            fixture.Target.Id, new DefaultHttpContext(), fixture.AdminPrincipal(current.Id),
            new Server.Api.AdminRevokeAllSessionsRequest(CorrectPassword, Reason),
            new AllowAntiforgery(), fixture.Database, fixture.SessionService,
            fixture.PasswordHashService, TimeProvider.System, default);

        var ok = Assert.IsType<Ok<Server.Api.AdminRevokeAllSessionsResponse>>(result);
        Assert.Equal(2, ok.Value!.RevokedCount);
        Assert.NotNull((await fixture.Database.UserSessions.SingleAsync(session => session.Id == first.Id)).RevokedAtUtc);
        Assert.Equal(Reason, (await fixture.Database.UserSessions.SingleAsync(session => session.Id == first.Id)).RevocationReasonCode);
        Assert.Null((await fixture.Database.UserSessions.SingleAsync(session => session.Id == otherUser.Id)).RevokedAtUtc);
        // An already-revoked session is not touched again: its reason stays as it was.
        Assert.Equal("self_revoked",
            (await fixture.Database.UserSessions.SingleAsync(session => session.Id == alreadyRevoked.Id)).RevocationReasonCode);
        var audit = await fixture.Database.SecurityAuditEvents.SingleAsync();
        Assert.Equal("AdminUserSessionsRevoked", audit.EventType);
        using var details = JsonDocument.Parse(audit.DetailsJson!);
        Assert.Equal(2, details.RootElement.GetProperty("revokedCount").GetInt32());

        // A second call is an idempotent no-op: zero revocations, no second audit.
        var secondCall = await Server.Api.AdminUserSessionEndpoints.RevokeAllUserSessionsAsync(
            fixture.Target.Id, new DefaultHttpContext(), fixture.AdminPrincipal(current.Id),
            new Server.Api.AdminRevokeAllSessionsRequest(CorrectPassword, Reason),
            new AllowAntiforgery(), fixture.Database, fixture.SessionService,
            fixture.PasswordHashService, TimeProvider.System, default);
        Assert.Equal(0, Assert.IsType<Ok<Server.Api.AdminRevokeAllSessionsResponse>>(secondCall).Value!.RevokedCount);
        Assert.Equal(1, await fixture.Database.SecurityAuditEvents.CountAsync());
    }

    [Fact]
    public async Task RevokeAll_IsDeniedForANonAdministrator_EvenWithAValidPassword()
    {
        await using var fixture = new Fixture();
        var targetSession = fixture.AddSession(fixture.Target);
        await fixture.Database.SaveChangesAsync();

        var denied = await Server.Api.AdminUserSessionEndpoints.RevokeAllUserSessionsAsync(
            fixture.Target.Id, new DefaultHttpContext(), fixture.PlainPrincipal(fixture.Plain.Id),
            new Server.Api.AdminRevokeAllSessionsRequest(CorrectPassword, Reason),
            new AllowAntiforgery(), fixture.Database, fixture.SessionService,
            fixture.PasswordHashService, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status403Forbidden, (denied as IStatusCodeHttpResult)?.StatusCode);
        Assert.Null((await fixture.Database.UserSessions.SingleAsync(session => session.Id == targetSession.Id)).RevokedAtUtc);
        Assert.False(await fixture.Database.SecurityAuditEvents.AnyAsync());
    }

    [Fact]
    public async Task RevokingOwnSessions_EndsTheCurrentCookieToo()
    {
        await using var fixture = new Fixture();
        var current = fixture.AddSession(fixture.Admin);
        var otherDevice = fixture.AddSession(fixture.Admin, lastSeenAtUtc: DateTime.UtcNow.AddMinutes(-2));
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.AdminUserSessionEndpoints.RevokeAllUserSessionsAsync(
            fixture.Admin.Id, fixture.HttpContextWithAuthentication(), fixture.AdminPrincipal(current.Id),
            new Server.Api.AdminRevokeAllSessionsRequest(CorrectPassword, Reason),
            new AllowAntiforgery(), fixture.Database, fixture.SessionService,
            fixture.PasswordHashService, TimeProvider.System, default);

        var ok = Assert.IsType<Ok<Server.Api.AdminRevokeAllSessionsResponse>>(result);
        Assert.Equal(2, ok.Value!.RevokedCount);
        Assert.NotNull((await fixture.Database.UserSessions.SingleAsync(session => session.Id == current.Id)).RevokedAtUtc);
        Assert.NotNull((await fixture.Database.UserSessions.SingleAsync(session => session.Id == otherDevice.Id)).RevokedAtUtc);
        Assert.Equal(1, fixture.AuthenticationService.SignOutCalls);
    }

    [Fact]
    public async Task Authorization_IsReReadFromTheDatabase_OnEveryRequest()
    {
        await using var fixture = new Fixture();
        var current = fixture.AddSession(fixture.Admin);
        var target = fixture.AddSession(fixture.Target, lastSeenAtUtc: DateTime.UtcNow.AddMinutes(-2));
        await fixture.Database.SaveChangesAsync();

        var allowed = await Server.Api.AdminUserSessionEndpoints.RevokeUserSessionAsync(
            fixture.Target.Id, target.Id, new DefaultHttpContext(), fixture.AdminPrincipal(current.Id),
            new Server.Api.AdminRevokeSessionRequest(CorrectPassword, Reason),
            new AllowAntiforgery(), fixture.Database, fixture.SessionService,
            fixture.PasswordHashService, TimeProvider.System, default);
        Assert.Equal(StatusCodes.Status204NoContent, (allowed as IStatusCodeHttpResult)?.StatusCode);

        // The administrator role is deactivated in the database: the same cookie must now
        // be denied even though its claims still say "admin".
        var role = await fixture.Database.Roles.SingleAsync(role => role.Name == Constants.Role.Admin);
        role.IsActive = false;
        await fixture.Database.SaveChangesAsync();

        var secondTarget = fixture.AddSession(fixture.Target, lastSeenAtUtc: DateTime.UtcNow.AddMinutes(-3));
        await fixture.Database.SaveChangesAsync();
        var denied = await Server.Api.AdminUserSessionEndpoints.RevokeUserSessionAsync(
            fixture.Target.Id, secondTarget.Id, new DefaultHttpContext(), fixture.AdminPrincipal(current.Id),
            new Server.Api.AdminRevokeSessionRequest(CorrectPassword, Reason),
            new AllowAntiforgery(), fixture.Database, fixture.SessionService,
            fixture.PasswordHashService, TimeProvider.System, default);

        Assert.Equal(StatusCodes.Status403Forbidden, (denied as IStatusCodeHttpResult)?.StatusCode);
        Assert.Null((await fixture.Database.UserSessions.SingleAsync(session => session.Id == secondTarget.Id)).RevokedAtUtc);
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

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public int SignOutCalls { get; private set; }
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignOutCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            PasswordHashService = new Infrastructure.Security.PasswordHashService();
            Database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
            Admin = AddUser("admin", Constants.Role.Admin, hashedPassword: PasswordHashService.Hash(CorrectPassword));
            Target = AddUser("target", "Accountant");
            Plain = AddUser("plain", "Accountant");
            SessionService = new Infrastructure.Security.UserSessionService(Database, TimeProvider.System);
        }

        public Data.DatabaseContext Database { get; }
        public Infrastructure.Security.UserSessionService SessionService { get; }
        public Infrastructure.Security.PasswordHashService PasswordHashService { get; }
        public RecordingAuthenticationService AuthenticationService { get; } = new();
        public Domain.User Admin { get; }
        public Domain.User Target { get; }
        public Domain.User Plain { get; }

        public Domain.User AddUser(string username, string roleName, string? hashedPassword = null)
        {
            var role = Database.Roles.Local.FirstOrDefault(x => x.Name == roleName)
                ?? new Domain.Role(roleName) { IsActive = true };
            if (role.Id == Guid.Empty || !Database.Roles.Local.Contains(role))
                Database.Roles.Add(role);

            var user = new Domain.User($"{username}@example.test")
            {
                Username = username,
                IsActive = true,
                Role = role,
                Password = hashedPassword ?? "not-a-real-hash",
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

        public ClaimsPrincipal AdminPrincipal(Guid? sessionId = null) => Principal(Admin.Id, sessionId);
        public ClaimsPrincipal PlainPrincipal(Guid userId, Guid? sessionId = null) => Principal(userId, sessionId);

        private static ClaimsPrincipal Principal(Guid userId, Guid? sessionId)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new("AccountKind", "User"),
            };
            if (sessionId is Guid id)
                claims.Add(new Claim(SessionIdClaimType, id.ToString()));

            return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        }

        public DefaultHttpContext HttpContextWithAuthentication()
        {
            var services = new ServiceCollection()
                .AddSingleton<Microsoft.AspNetCore.Authentication.IAuthenticationService>(AuthenticationService)
                .BuildServiceProvider();
            return new DefaultHttpContext { RequestServices = services };
        }

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}
