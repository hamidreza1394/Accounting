using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Server.Api;

/// <summary>
/// Installation-administrator revocation of another user's server-side sessions
/// (D-073 §1, on top of the D-069 session lifecycle).
/// </summary>
/// <remarks>
/// <para>
/// Only an <em>active installation administrator</em> (role re-read from the database on
/// every request) may act; a company manager never gains this ability and no
/// <c>CompanyRole</c> permission can unlock it. Every mutation requires step-up
/// authentication — the administrator re-enters their <em>own</em> current password, which
/// is verified server-side against the stored hash with the same
/// <see cref="Infrastructure.Security.PasswordHashService.Verify" /> mechanism the
/// change-password flow uses — plus a reason code chosen from the server-owned catalog.
/// </para>
/// <para>
/// D-073 §1 scope boundary: these endpoints exist for <em>another</em> user only — a
/// self-targeted request (<c>userId == actorId</c>) is rejected with 400 so the
/// administrator's own sessions stay under the D-069 self-service endpoints exclusively.
/// </para>
/// <para>
/// The projection is the D-069 allowlist: session identity and UTC timestamps only; no IP
/// address, no user agent, no cookie, token or secret is ever returned. A revoked session
/// dies on its next request through <c>Infrastructure.Security.SessionValidator</c>.
/// Revocations are idempotent and audited with
/// <c>AdminUserSessionRevoked</c> / <c>AdminUserSessionsRevoked</c>; the raw password and
/// every other secret stay out of logs and audit payloads. On a relational store the
/// session mutation and its audit row commit in <em>one</em> transaction: a failed audit
/// insert rolls the revocation back, so evidence is never missing for a live revocation
/// (fail-closed).
/// </para>
/// </remarks>
public static class AdminUserSessionEndpoints
{
    /// <summary>
    /// The server-owned catalog of revocation reasons (D-073 §1: the reason must be
    /// chosen from the allowed codes and is stored on the session row and in the audit).
    /// </summary>
    public static readonly IReadOnlyList<string> AllowedRevocationReasonCodes =
    [
        "admin_revoked_suspected_compromise",
        "admin_revoked_offboarding",
        "admin_revoked_user_request",
        "admin_revoked_administrative",
    ];

    public static RouteGroupBuilder MapAdminUserSessionEndpoints(this RouteGroupBuilder api)
    {
        var sessions = api.MapGroup("/admin/users/{userId:guid}/sessions")
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));

        sessions.MapGet("", ListUserSessionsAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        sessions.MapPost("/{sessionId:guid}/revoke", RevokeUserSessionAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        sessions.MapPost("/revoke-all", RevokeAllUserSessionsAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);

        return sessions;
    }

    /// <summary>GET /api/v1/admin/users/{userId}/sessions — the active sessions of the target user (allowlist projection, D-069); self-targeting is rejected.</summary>
    public static async Task<IResult> ListUserSessionsAsync(
        Guid userId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        Infrastructure.Security.UserSessionService sessionService,
        CancellationToken cancellationToken)
    {
        var actorId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        var authorization = await UserAdministrationEndpoints.AuthorizeInstallationAdministratorAsync(
            principal, database, cancellationToken);
        if (authorization is not null)
            return authorization;

        if (actorId is not null && userId == actorId.Value)
            return RejectSelfTargeting();

        if (userId == Guid.Empty)
            return TypedResults.NotFound();

        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.Pragma = "no-cache";

        var sessions = await sessionService.ListActiveSessionsAsync(userId, currentSessionId: null, cancellationToken);

        return TypedResults.Ok(sessions.Select(session => new AdminUserSessionListItem(
            session.SessionId,
            DateTime.SpecifyKind(session.CreatedAtUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(session.LastSeenAtUtc, DateTimeKind.Utc)))
            .ToList());
    }

    /// <summary>POST /api/v1/admin/users/{userId}/sessions/{sessionId}/revoke — ends one session of the target user (anti-forgery + step-up).</summary>
    public static async Task<IResult> RevokeUserSessionAsync(
        Guid userId,
        Guid sessionId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] AdminRevokeSessionRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        Infrastructure.Security.UserSessionService sessionService,
        Infrastructure.Security.PasswordHashService passwordHashService,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actorId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        var authorization = await UserAdministrationEndpoints.AuthorizeInstallationAdministratorAsync(
            principal, database, cancellationToken);
        if (authorization is not null)
            return authorization;

        // D-073 §1: the administrative path targets another user only; own sessions stay
        // under the D-069 self-service endpoints. Checked before any other validation so a
        // self-targeted request never consumes step-up or anti-forgery validation.
        if (actorId is not null && userId == actorId.Value)
            return RejectSelfTargeting();

        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(
                title: "درخواست نامعتبر است. صفحه را دوباره بارگذاری کنید.",
                statusCode: StatusCodes.Status400BadRequest);

        var stepUp = await VerifyAdministratorReauthenticationAsync(
            database, actorId, request.CurrentPassword, passwordHashService, cancellationToken);
        if (stepUp is not null)
            return stepUp;

        var reasonCode = request.ReasonCode?.Trim();
        if (reasonCode is null || !AllowedRevocationReasonCodes.Contains(reasonCode))
            return TypedResults.BadRequest(new { error = "کد دلیل ابطال معتبر نیست." });

        httpContext.Response.Headers.CacheControl = "no-store";

        // The revocation and its mandatory audit evidence commit together: if the audit
        // insert fails, the whole transaction rolls back and the session stays active
        // (fail-closed, D-073 §1 audit requirement).
        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        // The ownership check is part of the UPDATE predicate: a session of another user is
        // never revocable here, and a foreign or unknown identifier is indistinguishable.
        var revoked = await sessionService.RevokeAsync(
            userId, sessionId, actorId, cancellationToken, reasonCode);

        if (!revoked && !await sessionService.ExistsAsync(userId, sessionId, cancellationToken))
            return TypedResults.NotFound();

        if (revoked)
        {
            database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
            {
                UserId = actorId!.Value,
                EventType = "AdminUserSessionRevoked",
                EntityType = "UserSession",
                EntityId = sessionId.ToString(),
                OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                CorrelationId = System.Diagnostics.Activity.Current?.Id,
                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    targetUserId = userId,
                    reasonCode,
                }),
            });
            await database.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    /// <summary>POST /api/v1/admin/users/{userId}/sessions/revoke-all — ends every active session of the target user (anti-forgery + step-up).</summary>
    public static async Task<IResult> RevokeAllUserSessionsAsync(
        Guid userId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] AdminRevokeAllSessionsRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        Infrastructure.Security.UserSessionService sessionService,
        Infrastructure.Security.PasswordHashService passwordHashService,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actorId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        var authorization = await UserAdministrationEndpoints.AuthorizeInstallationAdministratorAsync(
            principal, database, cancellationToken);
        if (authorization is not null)
            return authorization;

        // D-073 §1: the administrative path targets another user only; own sessions stay
        // under the D-069 self-service endpoints (revoke-others keeps the current session).
        if (actorId is not null && userId == actorId.Value)
            return RejectSelfTargeting();

        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(
                title: "درخواست نامعتبر است. صفحه را دوباره بارگذاری کنید.",
                statusCode: StatusCodes.Status400BadRequest);

        var stepUp = await VerifyAdministratorReauthenticationAsync(
            database, actorId, request.CurrentPassword, passwordHashService, cancellationToken);
        if (stepUp is not null)
            return stepUp;

        var reasonCode = request.ReasonCode?.Trim();
        if (reasonCode is null || !AllowedRevocationReasonCodes.Contains(reasonCode))
            return TypedResults.BadRequest(new { error = "کد دلیل ابطال معتبر نیست." });

        httpContext.Response.Headers.CacheControl = "no-store";

        // Same atomicity contract as the single revocation: the set-based write and the
        // audit row commit together or not at all.
        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        var revokedCount = await sessionService.RevokeAllAsync(
            userId, actorId, cancellationToken, reasonCode);

        if (revokedCount > 0)
        {
            database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
            {
                UserId = actorId!.Value,
                EventType = "AdminUserSessionsRevoked",
                EntityType = "UserSession",
                EntityId = userId.ToString(),
                OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                CorrelationId = System.Diagnostics.Activity.Current?.Id,
                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    targetUserId = userId,
                    reasonCode,
                    revokedCount,
                }),
            });
            await database.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return TypedResults.Ok(new AdminRevokeAllSessionsResponse(revokedCount));
    }

    /// <summary>
    /// Step-up authentication of the acting administrator: the current password of the
    /// administrator is re-verified against the stored hash (the secure pattern the
    /// change-password flow already uses). A missing or wrong password yields the same
    /// uniform 403, without disclosing which one failed.
    /// </summary>
    private static async Task<IResult?> VerifyAdministratorReauthenticationAsync(
        Data.DatabaseContext database,
        Guid? actorId,
        string? currentPassword,
        Infrastructure.Security.PasswordHashService passwordHashService,
        CancellationToken cancellationToken)
    {
        if (actorId is null)
            return ReauthenticationFailed();

        var actor = await database.Users.AsNoTracking()
            .Where(user => user.Id == actorId.Value && user.IsActive)
            .Select(user => new { user.Password })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null)
            return ReauthenticationFailed();

        return passwordHashService
            .Verify(actor.Password, currentPassword) == Infrastructure.Security.PasswordVerificationOutcome.Failed
            ? ReauthenticationFailed()
            : null;
    }

    private static IResult ReauthenticationFailed() =>
        TypedResults.Json(
            new { error = "احراز هویت مجدد ناموفق بود؛ رمز عبور جاری مدیر را درست وارد کنید." },
            statusCode: StatusCodes.Status403Forbidden);

    private static IResult RejectSelfTargeting() =>
        TypedResults.Json(
            new { error = "مدیریت نشست‌های شخصی از مسیر مدیریتی مجاز نیست؛ برای نشست‌های خودتان از قابلیت‌های خود-خدمتی (D-069) استفاده کنید." },
            statusCode: StatusCodes.Status400BadRequest);
}

public sealed record AdminUserSessionListItem(
    Guid SessionId,
    DateTime CreatedAtUtc,
    DateTime LastSeenAtUtc);

/// <summary>Step-up body of a single administrative revocation; nothing else is accepted (allowlist).</summary>
public sealed record AdminRevokeSessionRequest(
    string? CurrentPassword,
    string? ReasonCode);

/// <summary>Step-up body of an administrative revoke-all; nothing else is accepted (allowlist).</summary>
public sealed record AdminRevokeAllSessionsRequest(
    string? CurrentPassword,
    string? ReasonCode);

public sealed record AdminRevokeAllSessionsResponse(int RevokedCount);
