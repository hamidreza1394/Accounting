using Microsoft.AspNetCore.Authentication;

namespace Server.Pages.Security
{
	/// <summary>
	/// The sign-in page of Faranou. The login button is connected to the real
	/// authentication of the project (<see cref="Infrastructure.Security.AuthenticationService" />):
	/// credentials are verified against the user store (or the configured
	/// infrastructure account), failures are throttled and recorded in the security
	/// audit trail, and the redirect target is restricted to local URLs.
	/// </summary>
	public class LoginModel : Infrastructure.BasePageModelWithDatabaseContext
	{
		#region Constructor
		public LoginModel
			(Data.DatabaseContext databaseContext,
			Microsoft.Extensions.Logging.ILogger<LoginModel> logger,
			Infrastructure.Settings.ApplicationSettings applicationSettings) :
			base(databaseContext: databaseContext)
		{
			ViewModel = new();
			Logger = logger;

			AuthenticationService =
				new Infrastructure.Security.AuthenticationService
				(databaseContext: databaseContext, logger: logger,
				applicationSettings: applicationSettings);
		}
		#endregion /Constructor

		#region Properties
		[Microsoft.AspNetCore.Mvc.BindProperty]
		public string? ReturnUrl { get; set; }

		[Microsoft.AspNetCore.Mvc.BindProperty]
		public ViewModels.Pages.Account.LoginViewModel ViewModel { get; set; }

		private Microsoft.Extensions.Logging.ILogger<LoginModel> Logger { get; }

		private Infrastructure.Security.AuthenticationService AuthenticationService { get; }
		#endregion /Properties

		#region Methods
		public void OnGet(string? returnUrl)
		{
			ReturnUrl = returnUrl;
		}

		public async System.Threading.Tasks.Task
			<Microsoft.AspNetCore.Mvc.IActionResult> OnPostAsync()
		{
			// Scenario (a): the required fields are empty. The field messages are
			// produced by the model validation; one summary message is added as well.
			if (string.IsNullOrWhiteSpace(value: ViewModel.Username) ||
				string.IsNullOrWhiteSpace(value: ViewModel.Password))
			{
				ModelState.AddModelError
					(key: string.Empty,
					errorMessage: Resources.Messages.Errors.UsernameAndPasswordAreRequired);

				ViewModel.Password = null;
				return Page();
			}

			if (ModelState.IsValid == false)
			{
				ViewModel.Password = null;
				return Page();
			}

			try
			{
				var userIP =
					HttpContext.Connection.RemoteIpAddress?.ToString();

				var loginResult = await AuthenticationService
					.AuthenticateAsync
					(username: ViewModel.Username, password: ViewModel.Password,
					userIP: userIP);

				switch (loginResult.Outcome)
				{
					// **************************************************
					// Scenario (c): too many failed attempts.
					// **************************************************
					case Infrastructure.Security.LoginOutcome.TemporarilyLocked:
					{
						AddPageError(message:
							Resources.Messages.Errors.AccountIsTemporarilyLocked);

						ViewModel.Password = null;
						return Page();
					}

					// **************************************************
					// Scenario (c): the account is disabled. This state is only
					// reported after the correct password is supplied.
					// **************************************************
					case Infrastructure.Security.LoginOutcome.AccountIsNotActive:
					{
						AddPageError(message:
							Resources.Messages.Errors.UserAccountIsNotActive);

						ViewModel.Password = null;
						return Page();
					}

					// **************************************************
					// Scenario (b): wrong username or password - one generic and
					// secure message for both cases.
					// **************************************************
					case Infrastructure.Security.LoginOutcome.InvalidCredentials:
					{
						AddPageError(message:
							Resources.Messages.Errors.InvalidUsernameOrPassword);

						ViewModel.Password = null;
						return Page();
					}

					// **************************************************
					// Scenario (d): the credential is verified. The principal is
					// built from the verified account only; company identifiers come
					// from the validated memberships and never from client input.
					// **************************************************
					case Infrastructure.Security.LoginOutcome.Succeeded:
					{
						var claimsPrincipal = BuildClaimsPrincipal
							(loginResult: loginResult);

						var authenticationProperties =
							new Microsoft.AspNetCore.Authentication.AuthenticationProperties
							{
								IsPersistent = ViewModel.RememberMe,
							};

						// SignInAsync -> using Microsoft.AspNetCore.Authentication;
						await HttpContext.SignInAsync
							(scheme: Infrastructure.Security.Utility.AuthenticationScheme,
							principal: claimsPrincipal,
							properties: authenticationProperties);

						AddPageSuccess(message:
							Resources.Messages.Successes.LoginSucceeded);

						// Only redirect to local URLs to prevent an open-redirect.
						if (!string.IsNullOrWhiteSpace(ReturnUrl) &&
							Url.IsLocalUrl(ReturnUrl))
						{
							return Redirect(url: ReturnUrl);
						}

						return RedirectToPage(pageName: "/Index");
					}
				}

				// Unreachable in practice; kept fail-closed.
				AddPageError(message: Resources.Messages.Errors.UnexpectedError);
				ViewModel.Password = null;
				return Page();
			}
			catch (System.Exception ex)
			{
				// Scenario (e): unexpected server error. The technical details stay
				// in the log; the user only sees a generic message.
				Logger.LogError
					(exception: ex, message: "Sign-in failed with an unexpected error.");

				AddPageError(message:
					Resources.Messages.Errors.UnexpectedError);

				ViewModel.Password = null;
				return Page();
			}
			finally
			{
				await DisposeDatabaseContextAsync();
			}
		}

		#region BuildClaimsPrincipal
		private System.Security.Claims.ClaimsPrincipal BuildClaimsPrincipal
			(Infrastructure.Security.LoginResult loginResult)
		{
			var claims =
				new System.Collections.Generic.List<System.Security.Claims.Claim>();

			if (loginResult.IsInfrastructureMaster)
			{
				// The infrastructure (master) account stays distinct from company
				// users and keeps its administrator role.
				claims.Add(item: new System.Security.Claims.Claim
					(type: System.Security.Claims.ClaimTypes.Name, value: ViewModel.Username!));

				claims.Add(item: new System.Security.Claims.Claim
					(type: "FullName", value: ViewModel.Username!));

				claims.Add(item: new System.Security.Claims.Claim
					(type: System.Security.Claims.ClaimTypes.Role, value: Constants.Role.Admin));

				claims.Add(item: new System.Security.Claims.Claim
					(type: "AccountKind", value: "Infrastructure"));

				var claimsIdentity =
					new System.Security.Claims.ClaimsIdentity
					(claims: claims,
					authenticationType: Infrastructure.Security.Utility.AuthenticationScheme);

				return new System.Security.Claims.ClaimsPrincipal(identity: claimsIdentity);
			}

			var user = loginResult.User!;

			claims.Add(item: new System.Security.Claims.Claim
				(type: System.Security.Claims.ClaimTypes.Name, value: user.Username!));

			var fullName =
				$"{user.FirstName} {user.LastName}".Trim();

			if (string.IsNullOrWhiteSpace(value: fullName))
			{
				fullName = user.Username!;
			}

			claims.Add(item: new System.Security.Claims.Claim
				(type: "FullName", value: fullName));

			if (!string.IsNullOrWhiteSpace(value: user.Role?.Name))
			{
				claims.Add(item: new System.Security.Claims.Claim
					(type: System.Security.Claims.ClaimTypes.Role, value: user.Role!.Name!));
			}

			claims.Add(item: new System.Security.Claims.Claim
				(type: "AccountKind", value: "User"));

			// Company access is derived from active memberships only. A role in one
			// company never implies a role in another company; the role of each
			// membership is resolved per company when a company context is chosen.
			foreach (var tenantId in loginResult.ActiveCompanyIds)
			{
				claims.Add(item: new System.Security.Claims.Claim
					(type: "TenantId", value: tenantId.ToString()));
			}

			var userClaimsIdentity =
				new System.Security.Claims.ClaimsIdentity
				(claims: claims,
				authenticationType: Infrastructure.Security.Utility.AuthenticationScheme);

			return new System.Security.Claims.ClaimsPrincipal(identity: userClaimsIdentity);
		}
		#endregion /BuildClaimsPrincipal

		#endregion /Methods
	}
}
