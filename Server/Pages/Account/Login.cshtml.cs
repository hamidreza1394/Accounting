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
	/// <remarks>
	/// The page only coordinates the flow: validation, the authentication decision and
	/// the sign-in itself are separate steps. The authentication service is resolved from
	/// dependency injection (it owns the scoped database context) and is never created with
	/// <c>new</c> here.
	/// </remarks>
	public class LoginModel : Infrastructure.BasePageModel
	{
		#region Constants
		/// <summary>
		/// The React client. It asks <c>GET /api/v1/context</c> and shows the company and
		/// fiscal period selection (or enters directly / shows the status page), see D-031.
		/// </summary>
		public const string WorkContextAppPath = "/app/";

		/// <summary>
		/// The Razor home of the infrastructure (master) account. That account has no
		/// company, so the company selection is not meaningful for it (D-015).
		/// </summary>
		public const string InfrastructureHomePath = "/";
		#endregion /Constants

		#region Constructor
		public LoginModel
			(Infrastructure.Security.AuthenticationService authenticationService,
			Microsoft.Extensions.Logging.ILogger<LoginModel> logger) : base()
		{
			ViewModel = new();
			Logger = logger;
			AuthenticationService = authenticationService;
		}
		#endregion /Constructor

		#region GetDefaultDestination
		/// <summary>
		/// The destination of a signed-in browser that has no (safe) return URL: the company and
		/// fiscal period selection for company users, the Razor home for the infrastructure account.
		/// </summary>
		public static string GetDefaultDestination
			(System.Security.Claims.ClaimsPrincipal? principal)
		{
			var isInfrastructure =
				string.Equals
				(a: principal?.FindFirst(type: Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType)?.Value,
				b: "Infrastructure",
				comparisonType: System.StringComparison.Ordinal);

			return isInfrastructure ? InfrastructureHomePath : WorkContextAppPath;
		}
		#endregion /GetDefaultDestination

		#region Properties
		[Microsoft.AspNetCore.Mvc.BindProperty]
		public string? ReturnUrl { get; set; }

		[Microsoft.AspNetCore.Mvc.BindProperty]
		public ViewModels.Pages.Account.LoginViewModel ViewModel { get; set; }

		private Microsoft.Extensions.Logging.ILogger<LoginModel> Logger { get; }

		private Infrastructure.Security.AuthenticationService AuthenticationService { get; }
		#endregion /Properties

		#region Methods
		private const string RememberedUsernameCookieName = ".Faranou.RememberedUsername";

		public void OnGet(string? returnUrl)
		{
			ReturnUrl = returnUrl;

			if (HttpContext.Request.Cookies.TryGetValue(RememberedUsernameCookieName, out var rememberedUsername) &&
				string.IsNullOrWhiteSpace(rememberedUsername) == false)
			{
				ViewModel.Username = rememberedUsername;
				ViewModel.RememberMe = true;
			}
		}

		public async System.Threading.Tasks.Task
			<Microsoft.AspNetCore.Mvc.IActionResult> OnPostAsync()
		{
			// Opting out immediately removes the remembered username, including when
			// authentication subsequently fails. Passwords are never stored by this app.
			if (ViewModel.RememberMe == false)
			{
				DeleteRememberedUsername();
			}
			// **************************************************
			// Scenario (a): the required fields are empty. The field messages are
			// produced by the model validation; one summary message is added as well.
			// **************************************************
			if (string.IsNullOrWhiteSpace(value: ViewModel.Username) ||
				string.IsNullOrWhiteSpace(value: ViewModel.Password))
			{
				ModelState.AddModelError
					(key: string.Empty,
					errorMessage: Resources.Messages.Errors.UsernameAndPasswordAreRequired);

				ClearPassword();

				return Page();
			}

			if (ModelState.IsValid == false)
			{
				ClearPassword();

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

						ClearPassword();

						return Page();
					}

					// **************************************************
					// Scenario (c): the account is disabled. This state is only
					// reported after the correct password is supplied, so it cannot
					// be used to find out whether an account exists.
					// **************************************************
					case Infrastructure.Security.LoginOutcome.AccountIsNotActive:
					{
						AddPageError(message:
							Resources.Messages.Errors.UserAccountIsNotActive);

						ClearPassword();

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

						ClearPassword();

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
								// Remember Me keeps the behaviour of the previous
								// implementation: a persistent cookie instead of a
								// session cookie.
								IsPersistent = ViewModel.RememberMe,
							};

						// SignInAsync -> using Microsoft.AspNetCore.Authentication;
						await HttpContext.SignInAsync
							(scheme: Infrastructure.Security.Utility.AuthenticationScheme,
							principal: claimsPrincipal,
							properties: authenticationProperties);

						if (ViewModel.RememberMe)
						{
							RememberUsername(ViewModel.Username);
						}

						// The previous choice of company and fiscal period never survives a new sign-in.
						HttpContext.RequestServices
							.GetService<Infrastructure.Tenancy.WorkContextCookie>()
							?.Delete(httpContext: HttpContext);

						ClearPassword();

						// **************************************************
						// Post/Redirect/Get: the signed-in browser is redirected, so a
						// refresh of the destination never posts the form again and
						// never shows the "resubmit" dialog of the browser.
						// **************************************************
						var localReturnUrl = GetLocalReturnUrl();

						if (localReturnUrl != null)
						{
							return Redirect(url: localReturnUrl);
						}

						// No return URL: the work environment is chosen first. The server
						// validates the company and the period again when one is chosen.
						return Redirect(url: GetDefaultDestination(principal: claimsPrincipal));
					}
				}

				// Unreachable in practice; kept fail-closed.
				AddPageError(message: Resources.Messages.Errors.UnexpectedError);
				ClearPassword();

				return Page();
			}
			catch (System.Exception ex)
			{
				// Scenario (e): unexpected server error. The technical details stay
				// in the log; the user only sees a generic message. The exception is
				// logged without any credential, cookie or token.
				Logger.LogError
					(exception: ex, message: "Sign-in failed with an unexpected error.");

				AddPageError(message:
					Resources.Messages.Errors.UnexpectedError);

				ClearPassword();

				return Page();
			}
		}

		private void RememberUsername(string username)
		{
			var options = new Microsoft.AspNetCore.Http.CookieOptions
			{
				HttpOnly = true,
				Secure = HttpContext.Request.IsHttps,
				SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
				Path = "/",
				IsEssential = true,
				Expires = DateTimeOffset.UtcNow.AddDays(365),
			};

			HttpContext.Response.Cookies.Append(
				RememberedUsernameCookieName,
				username.Trim(),
				options);
		}

		private void DeleteRememberedUsername()
		{
			HttpContext.Response.Cookies.Delete(
				RememberedUsernameCookieName,
				new Microsoft.AspNetCore.Http.CookieOptions
				{
					HttpOnly = true,
					Secure = HttpContext.Request.IsHttps,
					SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
					Path = "/",
				});
		}

		#region GetLocalReturnUrl
		/// <summary>
		/// Returns the requested return URL only when it is a local URL of this
		/// application. Anything else - an absolute URL, a protocol-relative URL or a
		/// malformed value - yields <c>null</c>, so the caller falls back to the default
		/// destination and an open redirect is not possible. A return URL that points back
		/// to the sign-in pages is ignored as well, so the user never ends up in a loop.
		/// </summary>
		private string? GetLocalReturnUrl()
		{
			if (string.IsNullOrWhiteSpace(value: ReturnUrl))
			{
				return null;
			}

			if (Url.IsLocalUrl(url: ReturnUrl!) == false)
			{
				return null;
			}

			var path = ReturnUrl!.Split(separator: new[] { '?', '#' }, count: 2)[0];

			foreach (var signInPath in new[] { "/Account/Login", "/Account/LoginSuccess" })
			{
				// Equal to the page itself or below it ("/Account/Login/..."), nothing else.
				if (path.Equals(value: signInPath, comparisonType: System.StringComparison.OrdinalIgnoreCase) ||
					path.StartsWith
					(value: signInPath + "/", comparisonType: System.StringComparison.OrdinalIgnoreCase))
				{
					return null;
				}
			}

			return ReturnUrl;
		}
		#endregion /GetLocalReturnUrl

		#region ClearPassword
		/// <summary>
		/// Removes the supplied password from the page model and from the model state,
		/// without losing the validation errors that belong to the field.
		/// </summary>
		/// <remarks>
		/// Clearing only the property is not enough: the input tag helper takes the value
		/// of a failed post from the model state, so the password would otherwise be
		/// written back into the HTML of the response. Removing the whole model state entry
		/// is not right either, because it also drops the errors of the field - a password
		/// that violates the maximum length would then come back as a form without any
		/// message at all. The entry is therefore removed and its messages are re-added
		/// without a value.
		/// </remarks>
		private void ClearPassword()
		{
			ViewModel.Password = null;

			var modelStateKey =
				$"{nameof(ViewModel)}.{nameof(ViewModel.Password)}";

			if (ModelState.TryGetValue
				(key: modelStateKey, out var modelStateEntry) == false)
			{
				return;
			}

			var errorMessages =
				new System.Collections.Generic.List<string>();

			foreach (var error in modelStateEntry.Errors)
			{
				if (string.IsNullOrEmpty(value: error.ErrorMessage) == false)
				{
					errorMessages.Add(item: error.ErrorMessage);
				}
			}

			ModelState.Remove(key: modelStateKey);

			foreach (var errorMessage in errorMessages)
			{
				// Re-added without a value: the message stays, the credential does not.
				ModelState.AddModelError
					(key: modelStateKey, errorMessage: errorMessage);
			}
		}
		#endregion /ClearPassword

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

				claims.Add(item: new System.Security.Claims.Claim
					(type: Infrastructure.Security.SessionValidator.CredentialStampClaimType,
					value: loginResult.CredentialStamp ?? string.Empty));

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

			// A random identifier of THIS sign-in. The company and fiscal period a user chooses
			// are bound to it (Infrastructure.Tenancy.WorkContextCookie), so a choice made in an
			// earlier sign-in is never restored: every new sign-in asks again.
			claims.Add(item: new System.Security.Claims.Claim
				(type: Infrastructure.Tenancy.WorkContextCookie.SessionIdClaimType,
				value: System.Guid.NewGuid().ToString()));

			// The stable identifier of the verified account. Every server-side access
			// decision (for example the company access of the API) resolves the
			// memberships of this identifier from the database on each request.
			claims.Add(item: new System.Security.Claims.Claim
				(type: System.Security.Claims.ClaimTypes.NameIdentifier,
				value: user.Id.ToString()));

			// Re-validated on every request (Infrastructure.Security.SessionValidator): a changed
			// password or a deactivated account ends this session on the next request.
			claims.Add(item: new System.Security.Claims.Claim
				(type: Infrastructure.Security.SessionValidator.CredentialStampClaimType,
				value: loginResult.CredentialStamp ?? string.Empty));

			// Company identifiers are intentionally NOT copied into the cookie. A claim
			// would outlive a revoked membership until the cookie expires; the company
			// access is therefore always read from the active memberships in the database
			// (Infrastructure.Tenancy.CompanyAccessService).

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
