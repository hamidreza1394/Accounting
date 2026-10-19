namespace Server.Pages.Account
{
	/// <summary>
	/// The temporary destination of a successful sign-in.
	/// </summary>
	/// <remarks>
	/// This step of the project does not contain any accounting module, so the signed-in
	/// user is sent to this small, protected page instead of a dashboard. The page only
	/// confirms the sign-in; the next steps can replace it with the real application shell.
	/// It is protected on the server: an anonymous request is challenged by the cookie
	/// authentication handler before the page runs.
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
		public void OnGet()
		{
		}
		#endregion /Methods
	}
}
