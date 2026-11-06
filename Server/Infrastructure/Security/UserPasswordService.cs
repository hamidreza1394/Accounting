using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Security;

public enum ChangePasswordOutcome
{
    Changed,
    AccountUnavailable,
    CurrentPasswordInvalid,
    ConfirmationMismatch,
    PasswordPolicyRejected,
    NewPasswordMustDiffer,
}

public sealed record ChangePasswordResult(ChangePasswordOutcome Outcome);

/// <summary>
/// Handles a password change initiated by the authenticated account owner.
/// The caller must derive the user identifier from the verified principal.
/// </summary>
public sealed class UserPasswordService
{
    private readonly Data.DatabaseContext _databaseContext;
    private readonly PasswordHashService _passwordHashService;
    private readonly TimeProvider _timeProvider;

    public UserPasswordService(
        Data.DatabaseContext databaseContext,
        PasswordHashService passwordHashService,
        TimeProvider timeProvider)
    {
        _databaseContext = databaseContext;
        _passwordHashService = passwordHashService;
        _timeProvider = timeProvider;
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(
        Guid userId,
        string? currentPassword,
        string? newPassword,
        string? confirmation,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return new ChangePasswordResult(ChangePasswordOutcome.AccountUnavailable);
        }

        if (string.IsNullOrEmpty(newPassword) ||
            newPassword.Length < Constants.MinLength.UserPassword ||
            newPassword.Length > Constants.MaxLength.UserPassword ||
            !new ViewModels.Validation.NotCommonPasswordAttribute().IsValid(newPassword))
        {
            return new ChangePasswordResult(ChangePasswordOutcome.PasswordPolicyRejected);
        }

        if (!string.Equals(newPassword, confirmation, StringComparison.Ordinal))
        {
            return new ChangePasswordResult(ChangePasswordOutcome.ConfirmationMismatch);
        }

        var user = await _databaseContext.Users
            .Where(candidate => candidate.Id == userId && candidate.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return new ChangePasswordResult(ChangePasswordOutcome.AccountUnavailable);
        }

        var verification = _passwordHashService.Verify(user.Password, currentPassword);
        if (verification == PasswordVerificationOutcome.Failed)
        {
            return new ChangePasswordResult(ChangePasswordOutcome.CurrentPasswordInvalid);
        }

        if (string.Equals(newPassword, currentPassword, StringComparison.Ordinal))
        {
            return new ChangePasswordResult(ChangePasswordOutcome.NewPasswordMustDiffer);
        }

        user.Password = _passwordHashService.Hash(newPassword);
        user.SetUpdateDateTime();

        // D-069: a changed password revokes every server-side session of this user, so a
        // stolen cookie of another device dies even before its next request is validated.
        // The current credential stamp stays in place as defense in depth. Revoking here
        // (before the password SaveChanges) is fail-safe: even if the password write then
        // failed, ending the sessions is the more secure state, never the weaker one.
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        int revokedSessions;

        if (_databaseContext.Database.IsRelational())
        {
            // Set-based update on the production provider; the ownership predicate keeps
            // the write scoped to this user's rows. No RowVersion conflict can surface.
            revokedSessions = await _databaseContext.UserSessions
                .Where(session => session.UserId == user.Id && session.RevokedAtUtc == null)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(session => session.RevokedAtUtc, nowUtc)
                    .SetProperty(session => session.RevocationReasonCode, "password_changed"),
                cancellationToken);
        }
        else
        {
            // The InMemory provider of the unit tests does not support ExecuteUpdate; the
            // tracked write has the same outcome there.
            var activeSessions = await _databaseContext.UserSessions
                .Where(session => session.UserId == user.Id && session.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var session in activeSessions)
            {
                session.RevokedAtUtc = nowUtc;
                session.RevocationReasonCode = "password_changed";
            }

            revokedSessions = activeSessions.Count;
        }

        _databaseContext.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = user.Id,
            EventType = "PasswordChanged",
            EntityType = "User",
            EntityId = user.Id.ToString(),
            OccurredAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = ipAddress,
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(
                new { source = "SelfService", revokedSessions }),
        });

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return new ChangePasswordResult(ChangePasswordOutcome.Changed);
    }
}
