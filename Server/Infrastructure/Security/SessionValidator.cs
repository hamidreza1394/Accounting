using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Security;

/// <summary>
/// Re-validates the authentication cookie on every request.
/// </summary>
/// <remarks>
/// <para>
/// A cookie is self-contained: without this check it stays valid until it expires, even when
/// the account was deactivated or its password was changed in the meantime. Every request of
/// a company user therefore re-reads the account (one primary-key lookup) and rejects the
/// session when the account no longer exists, is inactive, or its stored credential changed
/// since the sign-in. The infrastructure account is checked against its configuration.
/// </para>
/// <para>
/// The "credential stamp" in the cookie is a SHA-256 digest of the stored password hash, so a
/// changed password (or a changed hash format) invalidates all sessions issued before. The
/// stored hash itself never leaves the server; the cookie is additionally encrypted by ASP.NET
/// Core Data Protection.
/// </para>
/// <para>
/// A session without a stamp (issued by an earlier release) is rejected once: the user signs
/// in again. A rejected session is signed out and the request continues as anonymous, so
/// pages redirect to the sign-in page and the API answers 401.
/// </para>
/// </remarks>
public static class SessionValidator
{
	public const string CredentialStampClaimType = "CredentialStamp";

	/// <summary>
	/// A server-side session (D-069) is valid only while its last use is inside this
	/// idle window; it matches the sliding expiration of the authentication cookie.
	/// </summary>
	public static readonly System.TimeSpan IdleWindow = System.TimeSpan.FromMinutes(30);

	/// <summary>
	/// The <c>LastSeenAtUtc</c> of a session is refreshed at most once per this
	/// interval, so normal traffic does not write on every request.
	/// </summary>
	public static readonly System.TimeSpan LastSeenWriteInterval = System.TimeSpan.FromMinutes(5);

	public static string ComputeCredentialStamp(string? storedCredential)
	{
		var bytes = System.Text.Encoding.UTF8.GetBytes(storedCredential ?? string.Empty);
		return System.Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(bytes));
	}

	public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
	{
		if (context.Principal is null)
		{
			return;
		}

		var isValid = await IsSessionValidAsync(
			context.Principal,
			context.HttpContext.RequestServices,
			context.HttpContext.RequestAborted);

		if (isValid)
		{
			return;
		}

		var logger = context.HttpContext.RequestServices
			.GetRequiredService<ILoggerFactory>()
			.CreateLogger(typeof(SessionValidator).FullName!);

		// No claim value is logged: only the fact that a session was ended.
		logger.LogInformation("An authentication cookie was rejected because the account or its credential changed.");

		context.RejectPrincipal();
		await context.HttpContext.SignOutAsync(context.Scheme.Name);
	}

	public static async Task<bool> IsSessionValidAsync(
		ClaimsPrincipal principal,
		IServiceProvider services,
		CancellationToken cancellationToken)
	{
		var stamp = principal.FindFirst(CredentialStampClaimType)?.Value;

		if (string.IsNullOrEmpty(stamp))
		{
			return false;
		}

		var accountKind = principal.FindFirst(Tenancy.CompanyAccessService.AccountKindClaimType)?.Value;

		if (string.Equals(accountKind, "Infrastructure", StringComparison.Ordinal))
		{
			var settings = services.GetRequiredService<Settings.ApplicationSettings>();

			return !string.IsNullOrWhiteSpace(settings.MasterUsername)
				&& !string.IsNullOrWhiteSpace(settings.MasterPassword)
				&& string.Equals(principal.Identity?.Name, settings.MasterUsername, StringComparison.OrdinalIgnoreCase)
				&& FixedTimeEquals(stamp, ComputeCredentialStamp(settings.MasterPassword));
		}

		if (!string.Equals(accountKind, Tenancy.CompanyAccessService.UserAccountKind, StringComparison.Ordinal))
		{
			return false;
		}

		var userId = Tenancy.CompanyAccessService.GetUserId(principal);
		var databaseContext = services.GetService<Data.DatabaseContext>();

		if (userId is null || databaseContext is null)
		{
			return false;
		}

		// D-069: the SessionId claim of the cookie is the identifier of one server-side
		// session row. A cookie without the claim was issued by an older release (or was
		// tampered with) and has to sign in again.
		var sessionIdValue = principal.FindFirst(Tenancy.WorkContextCookie.SessionIdClaimType)?.Value;

		if (!System.Guid.TryParse(sessionIdValue, out var sessionId) || sessionId == System.Guid.Empty)
		{
			return false;
		}

		// One round trip: besides the account itself, the same query reports whether the
		// account is the initial administrator of a provisioning request whose one-time
		// enrollment is not completed (see ProvisioningRequestClaim.MustChangePassword).
		var account = await databaseContext.Users
			.AsNoTracking()
			.Where(user => user.Id == userId.Value)
			.Select(user => new
			{
				user.IsActive,
				user.Password,
				EnrollmentPending = databaseContext.ProvisioningRequestClaims
					.Any(claim => claim.AdminUserId == user.Id && claim.MustChangePassword),
			})
			.FirstOrDefaultAsync(cancellationToken);

		if (account is null
			|| !account.IsActive
			|| account.EnrollmentPending
			|| !FixedTimeEquals(stamp, ComputeCredentialStamp(account.Password)))
		{
			return false;
		}

		// The session row must exist, belong to this very user, not be revoked and not be
		// idle-expired; anything else fails closed (D-069). The row is loaded tracked on
		// purpose: the non-relational fallback below refreshes it through SaveChanges,
		// while the relational path uses an untracked set-based update and never mutates
		// the tracked instance, so no extra write can happen.
		var session = await databaseContext.UserSessions
			.FirstOrDefaultAsync(current => current.Id == sessionId, cancellationToken);

		if (session is null || session.UserId != userId.Value || session.RevokedAtUtc is not null)
		{
			return false;
		}

		var nowUtc = System.DateTime.UtcNow;

		if (session.LastSeenAtUtc < nowUtc.Subtract(IdleWindow))
		{
			return false;
		}

		// Throttled last-seen write: at most one UPDATE per interval per session. The
		// relational update is set-based and untracked, so a concurrent revocation can
		// never surface as a concurrency exception inside cookie validation.
		if (session.LastSeenAtUtc < nowUtc.Subtract(LastSeenWriteInterval))
		{
			if (databaseContext.Database.IsRelational())
			{
				await databaseContext.UserSessions
					.Where(current => current.Id == sessionId && current.RevokedAtUtc == null)
					.ExecuteUpdateAsync(
						update => update.SetProperty(current => current.LastSeenAtUtc, nowUtc),
						cancellationToken);
			}
			else
			{
				// The InMemory provider of the unit tests does not support ExecuteUpdate;
				// the tracked write has the same outcome there.
				session.LastSeenAtUtc = nowUtc;
				await databaseContext.SaveChangesAsync(cancellationToken);
			}
		}

		return true;
	}

	private static bool FixedTimeEquals(string left, string right)
	{
		return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
			System.Text.Encoding.UTF8.GetBytes(left),
			System.Text.Encoding.UTF8.GetBytes(right));
	}
}
