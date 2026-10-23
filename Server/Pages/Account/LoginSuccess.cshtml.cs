namespace Server.Pages.Account
{
	/// <summary>
	/// The former temporary destination of a successful sign-in. It shows nothing any more:
	/// the real flow is the company and fiscal period selection of the React client (D-031).
	/// </summary>
	/// <remarks>
	/// The page is kept only for old bookmarks and links. It stays protected on the server
	/// (an anonymous request is challenged before the page runs) and forwards with a
	/// fixed, local destination chosen from the account kind - never from client input.
	/// </remarks>
	[Microsoft.AspNetCore.Authorization.Authorize]
	public class LoginSuccessModel : Infrastructure.BasePageModel
	{
		#region Constructor
		public LoginSuccessModel() : base()
		{
		}
		#endregion /Constructor

		#region Methods
		public Microsoft.AspNetCore.Mvc.IActionResult OnGet()
		{
			return Redirect
				(url: Server.Pages.Security.LoginModel.GetDefaultDestination(principal: User));
		}
		#endregion /Methods
	}
}
