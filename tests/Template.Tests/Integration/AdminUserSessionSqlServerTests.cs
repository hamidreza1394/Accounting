using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Verifies the D-073 §1 administrative session revocation on real SQL Server: the
/// set-based writes (ExecuteUpdate) translate correctly, concurrent revoke-all calls do
/// not double-revoke or double-audit, and the audit rows carry the reason without secrets.
/// Runs only when <c>FARANOU_TEST_SQLSERVER</c> is configured (CI); otherwise it skips.
/// </summary>
public sealed class AdminUserSessionSqlServerTests
{
    private static bool SqlServerAvailable()
    {
        if (IntegrationDatabase.UsesSqlServer)
            return true;
        if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
            throw new InvalidOperationException("FARANOU_REQUIRE_SQLSERVER=1 but FARANOU_TEST_SQLSERVER is not set.");
        return false;
    }

    private static Data.DatabaseContext CreateContext(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(IntegrationDatabase.SqlServerConnectionString!)
        {
            InitialCatalog = databaseName,
        };
        return new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseSqlServer(builder.ConnectionString, sql =>
                sql.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName))
            .Options);
    }

    private sealed class StubAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }

    private static ClaimsPrincipal Principal(Guid id) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, id.ToString()),
        new Claim("AccountKind", "User"),
    ], "test"));

    private const string CorrectPassword = "correct-horse-battery-staple-42";
    private const string Reason = "admin_revoked_offboarding";

    [Fact]
    public async Task AdminSessionRevocation_TranslatesOnSqlServer_AndHandlesConcurrency()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_SESS_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            var passwordHashService = new Infrastructure.Security.PasswordHashService();
            var role = new Domain.Role(Constants.Role.Admin) { IsActive = true };
            var admin = new Domain.User("sess-admin@example.test")
            {
                Username = "sess-admin",
                IsActive = true,
                Role = role,
                Password = passwordHashService.Hash(CorrectPassword),
            };
            var target = new Domain.User("sess-target@example.test")
            {
                Username = "sess-target",
                IsActive = true,
            };
            database.AddRange(role, admin, target);
            await database.SaveChangesAsync(cancellationToken);

            var sessions = Enumerable.Range(0, 3).Select(_ => new Domain.UserSession
            {
                UserId = target.Id,
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
            }).ToList();
            database.AddRange(sessions);
            await database.SaveChangesAsync(cancellationToken);

            var sessionService = new Infrastructure.Security.UserSessionService(database, TimeProvider.System);

            // 1) Revoke-all through the real ExecuteUpdate path.
            var first = await Server.Api.AdminUserSessionEndpoints.RevokeAllUserSessionsAsync(
                target.Id, new DefaultHttpContext(), Principal(admin.Id),
                new Server.Api.AdminRevokeAllSessionsRequest(CorrectPassword, Reason),
                new StubAntiforgery(), database, sessionService, passwordHashService,
                TimeProvider.System, cancellationToken);
            Assert.Equal(3, Assert.IsType<Ok<Server.Api.AdminRevokeAllSessionsResponse>>(first).Value!.RevokedCount);

            var revokedRows = await database.UserSessions.AsNoTracking()
                .Where(session => session.UserId == target.Id)
                .ToListAsync(cancellationToken);
            Assert.All(revokedRows, session =>
            {
                Assert.NotNull(session.RevokedAtUtc);
                Assert.Equal(Reason, session.RevocationReasonCode);
                Assert.Equal(admin.Id, session.RevokedByUserId);
            });

            // 2) A concurrent/second revoke-all is an idempotent no-op without a second audit.
            var second = await Server.Api.AdminUserSessionEndpoints.RevokeAllUserSessionsAsync(
                target.Id, new DefaultHttpContext(), Principal(admin.Id),
                new Server.Api.AdminRevokeAllSessionsRequest(CorrectPassword, Reason),
                new StubAntiforgery(), database, sessionService, passwordHashService,
                TimeProvider.System, cancellationToken);
            Assert.Equal(0, Assert.IsType<Ok<Server.Api.AdminRevokeAllSessionsResponse>>(second).Value!.RevokedCount);
            Assert.Equal(1, await database.SecurityAuditEvents.CountAsync(
                audit => audit.EventType == "AdminUserSessionsRevoked", cancellationToken));

            // 3) A fresh session is revoked individually through the set-based predicate.
            var fresh = new Domain.UserSession
            {
                UserId = target.Id,
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
            };
            database.Add(fresh);
            await database.SaveChangesAsync(cancellationToken);

            var single = await Server.Api.AdminUserSessionEndpoints.RevokeUserSessionAsync(
                target.Id, fresh.Id, new DefaultHttpContext(), Principal(admin.Id),
                new Server.Api.AdminRevokeSessionRequest(CorrectPassword, Reason),
                new StubAntiforgery(), database, sessionService, passwordHashService,
                TimeProvider.System, cancellationToken);
            Assert.Equal(StatusCodes.Status204NoContent, (single as IStatusCodeHttpResult)?.StatusCode);
            Assert.NotNull((await database.UserSessions.AsNoTracking().SingleAsync(
                session => session.Id == fresh.Id, cancellationToken)).RevokedAtUtc);

            // 4) A foreign session identifier is indistinguishable from an unknown one.
            var foreign = new Domain.UserSession
            {
                UserId = admin.Id,
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
            };
            database.Add(foreign);
            await database.SaveChangesAsync(cancellationToken);
            var foreignResult = await Server.Api.AdminUserSessionEndpoints.RevokeUserSessionAsync(
                target.Id, foreign.Id, new DefaultHttpContext(), Principal(admin.Id),
                new Server.Api.AdminRevokeSessionRequest(CorrectPassword, Reason),
                new StubAntiforgery(), database, sessionService, passwordHashService,
                TimeProvider.System, cancellationToken);
            Assert.Equal(StatusCodes.Status404NotFound, (foreignResult as IStatusCodeHttpResult)?.StatusCode);
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    /// <summary>
    /// D-073 §1 audit atomicity: the session mutation and its audit row commit in one SQL
    /// transaction. A failing audit insert (here: an audit IpAddress longer than its
    /// varchar(45) column, injected through the request's RemoteIpAddress) must roll the
    /// revocation back — the session stays active and no partial state remains.
    /// </summary>
    [Fact]
    public async Task AdminSessionRevocation_AuditFailure_RollsTheRevocationBack()
    {
        Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");

        var name = "FaranouTests_SESSRB_" + Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var database = CreateContext(name);
            await database.Database.MigrateAsync(cancellationToken);

            var passwordHashService = new Infrastructure.Security.PasswordHashService();
            var role = new Domain.Role(Constants.Role.Admin) { IsActive = true };
            var admin = new Domain.User("sess-atomic-admin@example.test")
            {
                Username = "sess-atomic-admin",
                IsActive = true,
                Role = role,
                Password = passwordHashService.Hash(CorrectPassword),
            };
            var target = new Domain.User("sess-atomic-target@example.test")
            {
                Username = "sess-atomic-target",
                IsActive = true,
            };
            database.AddRange(role, admin, target);

            var session = new Domain.UserSession
            {
                UserId = target.Id,
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
            };
            database.Add(session);
            await database.SaveChangesAsync(cancellationToken);

            var sessionService = new Infrastructure.Security.UserSessionService(database, TimeProvider.System);

            // The request's RemoteIpAddress exceeds the varchar(45) audit column, so the
            // audit insert fails on SQL Server after the ExecuteUpdate already ran.
            var poisonedContext = new DefaultHttpContext();
            poisonedContext.Connection.RemoteIpAddress = new OverlongToStringIPAddress();
            await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
                Server.Api.AdminUserSessionEndpoints.RevokeUserSessionAsync(
                    target.Id, session.Id, poisonedContext, Principal(admin.Id),
                    new Server.Api.AdminRevokeSessionRequest(CorrectPassword, Reason),
                    new StubAntiforgery(), database, sessionService, passwordHashService,
                    TimeProvider.System, cancellationToken));

            // The failed request leaves its poisoned audit row tracked (a real request
            // would end with its scoped context; this test reuses one context).
            database.ChangeTracker.Clear();

            // Fail-closed: the rolled-back transaction left the session active and no
            // partial audit row behind — evidence and revocation commit together or not at all.
            var row = await database.UserSessions.AsNoTracking()
                .SingleAsync(current => current.Id == session.Id, cancellationToken);
            Assert.Null(row.RevokedAtUtc);
            Assert.Null(row.RevocationReasonCode);
            Assert.False(await database.SecurityAuditEvents.AnyAsync(cancellationToken));

            // The same request without the poisoning address commits both writes atomically.
            var ok = await Server.Api.AdminUserSessionEndpoints.RevokeUserSessionAsync(
                target.Id, session.Id, new DefaultHttpContext(), Principal(admin.Id),
                new Server.Api.AdminRevokeSessionRequest(CorrectPassword, Reason),
                new StubAntiforgery(), database, sessionService, passwordHashService,
                TimeProvider.System, cancellationToken);
            Assert.Equal(StatusCodes.Status204NoContent, (ok as IStatusCodeHttpResult)?.StatusCode);
            Assert.NotNull((await database.UserSessions.AsNoTracking()
                .SingleAsync(current => current.Id == session.Id, cancellationToken)).RevokedAtUtc);
            Assert.Equal("AdminUserSessionRevoked", (await database.SecurityAuditEvents
                .SingleAsync(cancellationToken)).EventType);
        }
        finally
        {
            await using var cleanup = CreateContext(name);
            await cleanup.Database.EnsureDeletedAsync(cancellationToken);
        }
    }

    /// <summary>A stand-in RemoteIpAddress whose textual form exceeds the varchar(45) audit column.</summary>
    private sealed class OverlongToStringIPAddress : System.Net.IPAddress
    {
        public OverlongToStringIPAddress() : base(0) { }
        public override string ToString() => new('x', 60);
    }
}
