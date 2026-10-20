using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Server.Api;

/// <summary>
/// Makes the cookie handler answer API requests with status codes instead of redirects.
/// </summary>
/// <remarks>
/// A browser page that is not signed in is still redirected to <c>/Account/Login</c>, exactly
/// as before. A request to <c>/api/...</c> receives <c>401</c> (not signed in) or <c>403</c>
/// (signed in, not allowed), so the React client can react to it instead of receiving the
/// HTML of the login page with status 200.
/// </remarks>
public static class ApiCookieAuthenticationEvents
{
	private static readonly CookieAuthenticationEvents DefaultEvents = new();

	public static Task RedirectToLoginAsync(RedirectContext<CookieAuthenticationOptions> context)
	{
		if (ApiEndpoints.IsApiRequest(context.Request.Path))
		{
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			return Task.CompletedTask;
		}

		return DefaultEvents.OnRedirectToLogin(context);
	}

	public static Task RedirectToAccessDeniedAsync(RedirectContext<CookieAuthenticationOptions> context)
	{
		if (ApiEndpoints.IsApiRequest(context.Request.Path))
		{
			context.Response.StatusCode = StatusCodes.Status403Forbidden;
			return Task.CompletedTask;
		}

		return DefaultEvents.OnRedirectToAccessDenied(context);
	}
}
