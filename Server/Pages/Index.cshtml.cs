namespace Server.Pages
{
	public class IndexModel : Infrastructure.BasePageModel
	{
		public IndexModel() : base()
		{
		}

		public Microsoft.AspNetCore.Mvc.IActionResult OnGet()
		{
			// Send unauthenticated users to the login page when they open the site root.
			if (User.Identity?.IsAuthenticated != true)
			{
				return RedirectToPage(pageName: "/Account/Login");
			}

			return Page();
		}
	}
}
