using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.Tenancy;

/// <summary>
/// Stores the chosen company and fiscal period of one sign-in session in a protected cookie.
/// </summary>
/// <remarks>
/// <para>
/// The cookie is a <em>reference</em>, never an authority: it carries only the identifiers the user
/// chose. Every request that uses it re-validates them against the database through
/// <see cref="WorkContextService" />, so a revoked membership, a deactivated company or a closed
/// period takes effect immediately. The payload is encrypted and time-limited with ASP.NET Core
/// Data Protection, is bound to the signed-in user and to the identifier of the current sign-in
/// (<see cref="SessionIdClaimType" />), is HttpOnly and has no expiry (it ends with the browser
/// session). A new sign-in issues a new session identifier, so a choice made in an earlier
/// sign-in can never be restored; the cookie is also deleted at sign-in and sign-out.
/// </para>
/// </remarks>
public sealed class WorkContextCookie
{
	/// <summary>The claim holding the random identifier of one sign-in (issued by the sign-in page).</summary>
	public const string SessionIdClaimType = "SessionId";

	public const string CookieName = ".Faranou.WorkContext";

	private const string ProtectorPurpose = "Faranou.WorkContext.v1";

	/// <summary>An upper bound of the cookie's validity, independent of the sign-in lifetime.</summary>
	public static readonly TimeSpan MaxAge = TimeSpan.FromHours(12);

	public WorkContextCookie(IDataProtectionProvider dataProtectionProvider, TimeProvider timeProvider)
	{
		Protector = dataProtectionProvider.CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
		TimeProvider = timeProvider;
	}

	private ITimeLimitedDataProtector Protector { get; }

	private TimeProvider TimeProvider { get; }

	/// <summary>The identifier of the current sign-in, or <c>null</c> for a cookie issued before it existed.</summary>
	public static Guid? GetSessionId(ClaimsPrincipal? principal)
	{
		var value = principal?.FindFirst(SessionIdClaimType)?.Value;

		return Guid.TryParse(value, out var sessionId) && sessionId != Guid.Empty ? sessionId : null;
	}

	/// <summary>Returns the stored identifiers only when they belong to this user and this sign-in.</summary>
	public SelectedIds? Read(HttpContext httpContext, Guid userId, Guid sessionId)
	{
		if (!httpContext.Request.Cookies.TryGetValue(CookieName, out var protectedValue)
			|| string.IsNullOrEmpty(protectedValue))
		{
			return null;
		}

		try
		{
			var json = Protector.Unprotect(protectedValue);
			var payload = JsonSerializer.Deserialize<Payload>(json);

			if (payload is null
				|| payload.UserId != userId
				|| payload.SessionId != sessionId
				|| payload.CompanyId == Guid.Empty
				|| payload.FiscalPeriodId == Guid.Empty)
			{
				return null;
			}

			return new SelectedIds(payload.CompanyId, payload.FiscalPeriodId);
		}
		catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException
			or JsonException
			or ArgumentException
			or FormatException)
		{
			// A tampered, expired or foreign cookie is treated as "nothing selected". No cookie
			// value is logged.
			return null;
		}
	}

	public void Write(HttpContext httpContext, Guid userId, Guid sessionId, Guid companyId, Guid fiscalPeriodId)
	{
		var payload = new Payload(userId, sessionId, companyId, fiscalPeriodId);
		var protectedValue = Protector.Protect(
			JsonSerializer.Serialize(payload),
			TimeProvider.GetUtcNow().Add(MaxAge));

		// No Expires/MaxAge: the cookie ends with the browser session.
		httpContext.Response.Cookies.Append(CookieName, protectedValue, CreateOptions(httpContext));
	}

	public void Delete(HttpContext httpContext)
	{
		httpContext.Response.Cookies.Delete(CookieName, CreateOptions(httpContext));
	}

	private static CookieOptions CreateOptions(HttpContext httpContext)
	{
		return new CookieOptions
		{
			HttpOnly = true,
			IsEssential = true,
			Path = "/",
			SameSite = SameSiteMode.Lax,
			// Same rule as the authentication cookie: HTTPS only outside development.
			Secure = !httpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment(),
		};
	}

	public readonly record struct SelectedIds(Guid CompanyId, Guid FiscalPeriodId);

	private sealed record Payload(Guid UserId, Guid SessionId, Guid CompanyId, Guid FiscalPeriodId);
}
