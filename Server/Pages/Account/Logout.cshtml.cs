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
