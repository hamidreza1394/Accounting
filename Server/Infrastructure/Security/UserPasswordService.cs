using System.ComponentModel.DataAnnotations;
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

        _databaseContext.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = user.Id,
            EventType = "PasswordChanged",
            EntityType = "User",
            EntityId = user.Id.ToString(),
            OccurredAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = ipAddress,
            DetailsJson = "{\"source\":\"SelfService\"}",
        });

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return new ChangePasswordResult(ChangePasswordOutcome.Changed);
    }
}
