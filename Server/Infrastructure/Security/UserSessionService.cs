using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Security;

/// <summary>
/// Self-service lifecycle of the server-side sessions of one company user (D-069):
/// listing the active sessions, revoking one device and revoking every other device.
/// The caller must derive the user identifier from the verified principal; a session
/// of another user is never visible nor revocable through this service.
/// </summary>
/// <remarks>
/// No IP address and no user agent are stored or returned (D-069): the listing shows
/// only the session identity, whether it is the current one and its UTC timestamps.
/// The installation-administrator revocation of another user's sessions (D-073 §1)
/// reuses the same set-based writes with an explicit, catalog-validated reason code.
/// </remarks>
public sealed class UserSessionService
{
    /// <summary>The reason code written on a session that its owner revoked explicitly.</summary>
    public const string SelfRevokedReasonCode = "self_revoked";

    /// <summary>The reason code written on the sessions a revoke-others request ended.</summary>
    public const string RevokedOthersReasonCode = "revoked_others";

    private readonly Data.DatabaseContext _databaseContext;
    private readonly TimeProvider _timeProvider;

    public UserSessionService(
        Data.DatabaseContext databaseContext,
        TimeProvider timeProvider)
    {
        _databaseContext = databaseContext;
        _timeProvider = timeProvider;
    }

    /// <summary>The active (not revoked, not idle-expired) sessions of one user, newest use first.</summary>
    public async Task<IReadOnlyList<UserSessionListItem>> ListActiveSessionsAsync(
        Guid userId,
        Guid? currentSessionId,
        CancellationToken cancellationToken = default)
    {
        var idleBoundaryUtc = _timeProvider.GetUtcNow().UtcDateTime.Subtract(SessionValidator.IdleWindow);

        var sessions = await _databaseContext.UserSessions
            .AsNoTracking()
            .Where(session => session.UserId == userId && session.RevokedAtUtc == null)
            .Where(session => session.LastSeenAtUtc >= idleBoundaryUtc)
            .OrderByDescending(session => session.LastSeenAtUtc)
            .ThenByDescending(session => session.Id)
            .Select(session => new UserSessionListItem(
                session.Id,
                session.CreatedAtUtc,
                session.LastSeenAtUtc))
            .ToListAsync(cancellationToken);

        return sessions;
    }

    /// <summary>
    /// Revokes one session of the user. Revoking an already-revoked session is an
    /// idempotent no-op; an unknown or foreign session identifier is indistinguishable
    /// (no disclosure). Returns whether this call actually ended the session.
    /// </summary>
    public async Task<bool> RevokeAsync(
        Guid userId,
        Guid sessionId,
        Guid? revokedByUserId,
        CancellationToken cancellationToken = default,
        string? reasonCode = null)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        reasonCode ??= SelfRevokedReasonCode;

        if (_databaseContext.Database.IsRelational())
        {
            // Set-based update on the production provider: the ownership check is part of
            // the predicate, so the call is race-free without a concurrency exception.
            var updated = await _databaseContext.UserSessions
                .Where(session => session.Id == sessionId
                    && session.UserId == userId
                    && session.RevokedAtUtc == null)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(session => session.RevokedAtUtc, nowUtc)
                    .SetProperty(session => session.RevokedByUserId, revokedByUserId)
                    .SetProperty(session => session.RevocationReasonCode, reasonCode),
                cancellationToken);

            return updated == 1;
        }

        // The InMemory provider of the unit tests does not support ExecuteUpdate; the
        // tracked write has the same outcome there.
        var sessionRow = await _databaseContext.UserSessions
            .FirstOrDefaultAsync(session => session.Id == sessionId
                && session.UserId == userId
                && session.RevokedAtUtc == null,
            cancellationToken);

        if (sessionRow is null)
        {
            return false;
        }

        sessionRow.RevokedAtUtc = nowUtc;
        sessionRow.RevokedByUserId = revokedByUserId;
        sessionRow.RevocationReasonCode = reasonCode;

        try
        {
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent writer revoked the same session first: the goal state holds.
            return false;
        }

        return true;
    }

    /// <summary>Whether the user still owns a session row with this identifier.</summary>
    public Task<bool> ExistsAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return _databaseContext.UserSessions
            .AsNoTracking()
            .AnyAsync(session => session.Id == sessionId && session.UserId == userId, cancellationToken);
    }

    /// <summary>
    /// Revokes every active session of the user except <paramref name="keepSessionId" />.
    /// Returns the number of sessions this call actually ended.
    /// </summary>
    public async Task<int> RevokeOthersAsync(
        Guid userId,
        Guid keepSessionId,
        Guid? revokedByUserId,
        CancellationToken cancellationToken = default,
        string? reasonCode = null)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        reasonCode ??= RevokedOthersReasonCode;

        if (_databaseContext.Database.IsRelational())
        {
            return await _databaseContext.UserSessions
                .Where(session => session.UserId == userId
                    && session.Id != keepSessionId
                    && session.RevokedAtUtc == null)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(session => session.RevokedAtUtc, nowUtc)
                    .SetProperty(session => session.RevokedByUserId, revokedByUserId)
                    .SetProperty(session => session.RevocationReasonCode, reasonCode),
                cancellationToken);
        }

        var otherSessions = await _databaseContext.UserSessions
            .Where(session => session.UserId == userId
                && session.Id != keepSessionId
                && session.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var session in otherSessions)
        {
            session.RevokedAtUtc = nowUtc;
            session.RevokedByUserId = revokedByUserId;
            session.RevocationReasonCode = reasonCode;
        }

        try
        {
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent writer revoked some of these sessions first; the goal state holds.
            return 0;
        }

        return otherSessions.Count;
    }

    /// <summary>
    /// Revokes every active session of the user with no exception (D-073 §1:
    /// the installation administrator ends all sessions of a target user).
    /// Returns the number of sessions this call actually ended; a second call is
    /// an idempotent no-op that returns zero.
    /// </summary>
    public async Task<int> RevokeAllAsync(
        Guid userId,
        Guid? revokedByUserId,
        CancellationToken cancellationToken = default,
        string? reasonCode = null)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        if (_databaseContext.Database.IsRelational())
        {
            return await _databaseContext.UserSessions
                .Where(session => session.UserId == userId && session.RevokedAtUtc == null)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(session => session.RevokedAtUtc, nowUtc)
                    .SetProperty(session => session.RevokedByUserId, revokedByUserId)
                    .SetProperty(session => session.RevocationReasonCode, reasonCode ?? RevokedOthersReasonCode),
                cancellationToken);
        }

        var activeSessions = await _databaseContext.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
        {
            session.RevokedAtUtc = nowUtc;
            session.RevokedByUserId = revokedByUserId;
            session.RevocationReasonCode = reasonCode ?? RevokedOthersReasonCode;
        }

        try
        {
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent writer revoked some of these sessions first; the goal state holds.
            return 0;
        }

        return activeSessions.Count;
    }
}

/// <summary>The allowlisted view of one active session (D-069: no IP, no user agent).</summary>
public sealed record UserSessionListItem(
    Guid SessionId,
    DateTime CreatedAtUtc,
    DateTime LastSeenAtUtc);
