using Microsoft.Extensions.Logging;

namespace Server.Pages.Security
{
	public class RegisterModel : Infrastructure.BasePageModelWithDatabaseContext
	{
		public RegisterModel
			(Data.DatabaseContext databaseContext,
			Microsoft.Extensions.Logging.ILogger<RegisterModel> logger) : base(databaseContext: databaseContext)
		{
			Logger = logger;
			ViewModel = new();
		}

		// **********
		[Microsoft.AspNetCore.Mvc.BindProperty]
		public ViewModels.Pages.Account.RegisterViewModel ViewModel { get; set; }
		// **********

		// **********
		private Microsoft.Extensions.Logging.ILogger<RegisterModel> Logger { get; }
		// **********

		public void OnGet()
		{
		}

		public async
			System.Threading.Tasks.Task OnPostAsync()
		{
			// Public self-registration is not implemented: the previous implementation
			// stored nothing but still reported a successful registration. Accounts are
			// created by an administrator until a verified registration flow (unique
			// username/e-mail, password policy, e-mail verification and company
			// membership approval) exists. The user is told so honestly.
			Logger.LogInformation("A self-registration attempt was rejected because public registration is disabled.");

			AddPageError(message:
				"ثبت‌نام عمومی در این نسخه فعال نیست. حساب کاربری توسط مدیر سیستم ایجاد می‌شود.");

			await DisposeDatabaseContextAsync();
		}
	}
}
