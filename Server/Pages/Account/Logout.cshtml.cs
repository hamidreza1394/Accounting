using Microsoft.AspNetCore.Authentication;

namespace Server.Pages.Account
{
	/// <summary>
	/// Signs the current user out.
	/// </summary>
	/// <remarks>
	/// Signing out changes the state of the session, so it only happens on a POST that
	/// carries a valid anti-forgery token (Razor Pages validate it automatically for
	/// unsafe methods). A GET only shows the confirmation form; a link or an image on
	/// another site can therefore no longer end the session of the user.
	/// </remarks>
	[Microsoft.AspNetCore.Authorization.Authorize]
	public class LogoutModel : Infrastructure.BasePageModel
	{
		public LogoutModel() : base()
		{
		}

		public void OnGet()
		{
		}

		public async System.Threading.Tasks.Task
			<Microsoft.AspNetCore.Mvc.IActionResult> OnPostAsync()
		{
			// D-069: the explicit logout also ends the server-side session row, so this
			// device disappears from the session list. A failure must not block the logout.
			var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(User);
			var sessionIdValue = User.FindFirst(Infrastructure.Tenancy.WorkContextCookie.SessionIdClaimType)?.Value;

			if (userId is not null &&
				System.Guid.TryParse(sessionIdValue, out var sessionId) &&
				sessionId != System.Guid.Empty)
			{
				var sessionService = HttpContext.RequestServices
					.GetService<Infrastructure.Security.UserSessionService>();

				if (sessionService is not null)
				{
					try
					{
						await sessionService.RevokeAsync(
							userId: userId.Value,
							sessionId: sessionId,
							revokedByUserId: userId.Value);
					}
					catch (System.Exception)
					{
						// The cookie is cleared regardless; the idle window or the next
						// validation closes the row's effect anyway. Nothing sensitive is logged.
					}
				}
			}

			// SignOutAsync -> using Microsoft.AspNetCore.Authentication;
			await HttpContext.SignOutAsync
				(scheme: Infrastructure.Security.Utility.AuthenticationScheme);

			HttpContext.RequestServices
				.GetService<Infrastructure.Tenancy.WorkContextCookie>()
				?.Delete(httpContext: HttpContext);

			return RedirectToPage(pageName: "/Account/Login");
		}
	}
}
