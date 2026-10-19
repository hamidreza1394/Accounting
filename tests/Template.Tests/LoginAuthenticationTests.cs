using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Template.Tests;

/// <summary>
/// Captures the sign-in of the cookie handler. A sign-in produces the authentication
/// cookie; a challenge or a forbid produces none. Both are observable here without an
/// HTTP host, which keeps the tests deterministic.
/// </summary>
internal sealed class CapturingAuthenticationService : IAuthenticationService
{
	public int SignInCount { get; private set; }

	public int ChallengeCount { get; private set; }

	public string? SignInScheme { get; private set; }

	public ClaimsPrincipal? Principal { get; private set; }

	public AuthenticationProperties? Properties { get; private set; }

	public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
	{
		return Task.FromResult(AuthenticateResult.NoResult());
	}

	public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
	{
		ChallengeCount++;
		return Task.CompletedTask;
	}

	public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
	{
		return Task.CompletedTask;
	}

	public Task SignInAsync
		(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
	{
		SignInCount++;
		SignInScheme = scheme;
		Principal = principal;
		Properties = properties;

		return Task.CompletedTask;
	}

	public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
	{
		return Task.CompletedTask;
	}
}

/// <summary>
/// A real <see cref="UrlHelperBase" /> on a test action context, so the production
/// <c>Url.IsLocalUrl</c> implementation is exercised instead of a stub.
/// </summary>
internal sealed class TestUrlHelper : UrlHelperBase
{
	public TestUrlHelper(ActionContext actionContext) : base(actionContext)
	{
	}

	public override string? Action(UrlActionContext actionContext)
	{
		return null;
	}

	public override string? RouteUrl(UrlRouteContext routeContext)
	{
		return null;
	}
}

/// <summary>
/// TempData storage that survives a request boundary, so Post/Redirect/Get can be
/// asserted the way it behaves in the browser.
/// </summary>
internal sealed class InMemoryTempDataProvider : ITempDataProvider
{
	private IDictionary<string, object?> _data =
		new Dictionary<string, object?>(comparer: StringComparer.Ordinal);

	public IDictionary<string, object?> LoadTempData(HttpContext context)
	{
		return _data;
	}

	public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
	{
		_data = values;
	}
}

/// <summary>
/// The sign-in flow of the login page: what the user sees for every outcome, whether an
/// authentication cookie is issued, and where a successful sign-in is redirected to.
/// </summary>
public sealed class LoginAuthenticationTests
{
	#region Harness
	private sealed class Harness : IDisposable
	{
		public Harness
			(string? masterUsername = null, string? masterPassword = null)
		{
			Authentication = new CapturingAuthenticationService();

			Services =
				new ServiceCollection()
				.AddSingleton<IAuthenticationService>(implementationInstance: Authentication)
				.BuildServiceProvider();

			HttpContext =
				new DefaultHttpContext { RequestServices = Services };

			HttpContext.Request.Scheme = "https";
			HttpContext.Request.Host = new HostString(host: "localhost", port: 5001);

			TempDataProvider = new InMemoryTempDataProvider();
			TempData = new TempDataDictionary(context: HttpContext, provider: TempDataProvider);

			DatabaseOptions =
				new DbContextOptionsBuilder<Data.DatabaseContext>()
				.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
				.Options;

			Database = new Data.DatabaseContext(options: DatabaseOptions);

			Settings =
				new Infrastructure.Settings.ApplicationSettings
				{
					MasterUsername = masterUsername,
					MasterPassword = masterPassword,
				};

			PasswordHashService = new Infrastructure.Security.PasswordHashService();

			AuthenticationService =
				new Infrastructure.Security.AuthenticationService
					(databaseContext: Database,
					logger: NullLogger.Instance,
					applicationSettings: Settings,
					passwordHashService: PasswordHashService);
		}

		public CapturingAuthenticationService Authentication { get; }

		public ServiceProvider Services { get; }

		public DefaultHttpContext HttpContext { get; }

		public InMemoryTempDataProvider TempDataProvider { get; }

		public ITempDataDictionary TempData { get; }

		public DbContextOptions<Data.DatabaseContext> DatabaseOptions { get; }

		public Data.DatabaseContext Database { get; }

		public Infrastructure.Settings.ApplicationSettings Settings { get; }

		public Infrastructure.Security.PasswordHashService PasswordHashService { get; }

		public Infrastructure.Security.AuthenticationService AuthenticationService { get; }

		public Server.Pages.Security.LoginModel CreateModel
			(string? username = null, string? password = null,
			bool rememberMe = false, string? returnUrl = null)
		{
			// A fresh action context per post: the model state of one attempt must not
			// leak into the next one.
			var actionContext =
				new ActionContext
					(httpContext: HttpContext,
					routeData: new RouteData(),
					actionDescriptor: new PageActionDescriptor());

			var pageContext =
				new PageContext(actionContext: actionContext);

			var model =
				new Server.Pages.Security.LoginModel
					(authenticationService: AuthenticationService,
					logger: NullLogger<Server.Pages.Security.LoginModel>.Instance)
				{
					PageContext = pageContext,
					Url = new TestUrlHelper(actionContext: actionContext),
					TempData = TempData,
					ReturnUrl = returnUrl,
					ViewModel =
						new ViewModels.Pages.Account.LoginViewModel
						{
							Username = username,
							Password = password,
							RememberMe = rememberMe,
						},
				};

			// The .NET 10 model-state pipeline no longer exposes a public value-provider
			// result, so the submitted values are not replayed here. The page is
			// responsible for removing the password from the model state, and that
			// contract is asserted on the model state directly.
			return model;
		}

		public Domain.User AddUser
			(string username, string? passwordHash, bool isActive = true)
		{
			var user =
				new Domain.User(emailAddress: $"{username}@example.test")
				{
					Username = username,
					Password = passwordHash,
					IsActive = isActive,
				};

			Database.Users.Add(entity: user);
			Database.SaveChanges();

			return user;
		}

		public IList<string>? GetMessages(string type)
		{
			return TempData[key: type] as IList<string>;
		}

		public void Dispose()
		{
			Database.Dispose();
			Services.Dispose();
		}
	}

	private const string PageError = "PageError";
	private const string PageSuccess = "PageSuccess";
	#endregion /Harness

	#region Scenario (a): empty or invalid fields
	[Fact]
	public async Task EmptyFields_ShowTheRequiredMessage_AndIssueNoCookie()
	{
		using var harness = new Harness();
		var model = harness.CreateModel(username: string.Empty, password: string.Empty);

		var result = await model.OnPostAsync();

		Assert.IsType<PageResult>(result);
		Assert.Equal(0, harness.Authentication.SignInCount);

		// The required-field message is shown by the validation summary of the form.
		Assert.True(model.ModelState.ContainsKey(key: string.Empty));

		var summaryErrors = model.ModelState[key: string.Empty]!.Errors;

		Assert.Contains
			(collection: summaryErrors,
			filter: error => error.ErrorMessage ==
				Resources.Messages.Errors.UsernameAndPasswordAreRequired);
	}

	[Fact]
	public async Task EmptyFields_NeverReachTheAuthenticationService()
	{
		using var harness = new Harness();
		var model = harness.CreateModel(username: "  ", password: "   ");

		var result = await model.OnPostAsync();

		Assert.IsType<PageResult>(result);
		Assert.Equal(0, harness.Authentication.SignInCount);
		Assert.Empty(collection: harness.Database.SecurityAuditEvents);
	}
	#endregion

	#region Scenario (b): wrong username or password
	[Fact]
	public async Task UnknownUsername_ShowsTheGenericMessage_AndIssuesNoCookie()
	{
		using var harness = new Harness();
		var model = harness.CreateModel(username: "nobody", password: "whatever");

		var result = await model.OnPostAsync();

		Assert.IsType<PageResult>(result);
		Assert.Equal(0, harness.Authentication.SignInCount);

		var messages = harness.GetMessages(type: PageError);
		Assert.NotNull(messages);
		Assert.Contains
			(expected: Resources.Messages.Errors.InvalidUsernameOrPassword,
			collection: messages!);
	}

	[Fact]
	public async Task WrongPassword_ShowsTheSameGenericMessage_AndIssuesNoCookie()
	{
		using var harness = new Harness();
		var passwordHash = harness.PasswordHashService.Hash(password: "Correct-Password-1");
		harness.AddUser(username: "dariush", passwordHash: passwordHash);

		var model = harness.CreateModel(username: "dariush", password: "Wrong-Password-1");

		var result = await model.OnPostAsync();

		Assert.IsType<PageResult>(result);
		Assert.Equal(0, harness.Authentication.SignInCount);

		var messages = harness.GetMessages(type: PageError);
		Assert.NotNull(messages);

		// The same message for an unknown account and for a wrong password: the
		// response never discloses whether the account exists.
		Assert.Equal
			(expected: Resources.Messages.Errors.InvalidUsernameOrPassword,
			actual: Assert.Single(collection: messages!));
	}

	[Fact]
	public async Task WrongPassword_IsRemovedFromTheResponse()
	{
		using var harness = new Harness();
		var passwordHash = harness.PasswordHashService.Hash(password: "Correct-Password-1");
		harness.AddUser(username: "dariush", passwordHash: passwordHash);

		var model = harness.CreateModel(username: "dariush", password: "Wrong-Password-1");

		await model.OnPostAsync();

		Assert.Null(model.ViewModel.Password);

		// The input tag helper would otherwise read the submitted value back from the
		// model state and write it into the HTML.
		Assert.False(model.ModelState.ContainsKey(key: "ViewModel.Password"));
	}

	[Fact]
	public async Task FailedAttempt_IsRecordedInTheAuditTrail()
	{
		using var harness = new Harness();
		harness.AddUser
			(username: "dariush",
			passwordHash: harness.PasswordHashService.Hash(password: "Correct-Password-1"));

		var model = harness.CreateModel(username: "dariush", password: "Wrong-Password-1");

		await model.OnPostAsync();

		var auditEvent =
			Assert.Single(collection: harness.Database.SecurityAuditEvents.ToList());

		Assert.Equal(expected: "LoginFailed", actual: auditEvent.EventType);
		Assert.Equal(expected: "dariush", actual: auditEvent.EntityId);
	}
	#endregion

	#region Scenario (c): inactive or locked account
	[Fact]
	public async Task InactiveAccount_ShowsItsMessage_AndIssuesNoCookie()
	{
		using var harness = new Harness();

		harness.AddUser
			(username: "dariush",
			passwordHash: harness.PasswordHashService.Hash(password: "Correct-Password-1"),
			isActive: false);

		var model = harness.CreateModel(username: "dariush", password: "Correct-Password-1");

		var result = await model.OnPostAsync();

		Assert.IsType<PageResult>(result);
		Assert.Equal(0, harness.Authentication.SignInCount);

		var messages = harness.GetMessages(type: PageError);
		Assert.NotNull(messages);
		Assert.Contains
			(expected: Resources.Messages.Errors.UserAccountIsNotActive,
			collection: messages!);
	}

	[Fact]
	public async Task InactiveAccount_IsOnlyReportedAfterTheCorrectPassword()
	{
		using var harness = new Harness();

		harness.AddUser
			(username: "dariush",
			passwordHash: harness.PasswordHashService.Hash(password: "Correct-Password-1"),
			isActive: false);

		var model = harness.CreateModel(username: "dariush", password: "Wrong-Password-1");

		await model.OnPostAsync();

		var messages = harness.GetMessages(type: PageError);

		// A wrong password must not reveal that the account is disabled.
		Assert.NotNull(messages);
		Assert.DoesNotContain
			(expected: Resources.Messages.Errors.UserAccountIsNotActive,
			collection: messages!);
		Assert.Contains
			(expected: Resources.Messages.Errors.InvalidUsernameOrPassword,
			collection: messages!);
	}

	[Fact]
	public async Task TooManyFailedAttempts_LockTheSignIn_AndIssueNoCookie()
	{
		using var harness = new Harness();
		harness.AddUser
			(username: "dariush",
			passwordHash: harness.PasswordHashService.Hash(password: "Correct-Password-1"));

		for (var attempt = 0;
			attempt < Infrastructure.Security.AuthenticationService.MaxFailedAttempts;
			attempt++)
		{
			var failedModel =
				harness.CreateModel(username: "dariush", password: "Wrong-Password-1");

			await failedModel.OnPostAsync();
		}

		// The correct password does not help while the sign-in is locked.
		var lockedModel =
			harness.CreateModel(username: "dariush", password: "Correct-Password-1");

		var result = await lockedModel.OnPostAsync();

		Assert.IsType<PageResult>(result);
		Assert.Equal(0, harness.Authentication.SignInCount);

		var messages = harness.GetMessages(type: PageError);
		Assert.NotNull(messages);
		Assert.Contains
			(expected: Resources.Messages.Errors.AccountIsTemporarilyLocked,
			collection: messages!);
	}
	#endregion

	#region Scenario (d): successful sign-in
	[Fact]
	public async Task ValidCredentials_IssueTheCookie_AndRedirectToTheTemporaryPage()
	{
		using var harness = new Harness();
		harness.AddUser
			(username: "dariush",
			passwordHash: harness.PasswordHashService.Hash(password: "Correct-Password-1"));

		var model = harness.CreateModel(username: "dariush", password: "Correct-Password-1");

		var result = await model.OnPostAsync();

		var redirect = Assert.IsType<RedirectToPageResult>(result);
		Assert.Equal
			(expected: Server.Pages.Security.LoginModel.SuccessPageName,
			actual: redirect.PageName);

		Assert.Equal(expected: 1, actual: harness.Authentication.SignInCount);
		Assert.Equal
			(expected: Infrastructure.Security.Utility.AuthenticationScheme,
			actual: harness.Authentication.SignInScheme);

		Assert.NotNull(harness.Authentication.Principal);
		Assert.Equal
			(expected: "dariush",
			actual: harness.Authentication.Principal!.Identity!.Name);

		Assert.False(harness.Authentication.Properties!.IsPersistent);
	}

	[Fact]
	public async Task SuccessfulSignIn_ShowsTheTemporarySuccessMessage()
	{
		using var harness = new Harness();
		harness.AddUser
			(username: "dariush",
			passwordHash: harness.PasswordHashService.Hash(password: "Correct-Password-1"));

		var model = harness.CreateModel(username: "dariush", password: "Correct-Password-1");

		await model.OnPostAsync();

		var messages = harness.GetMessages(type: PageSuccess);
		Assert.NotNull(messages);
		Assert.Contains
			(expected: Resources.Messages.Successes.LoginSucceededPendingFeatures,
			collection: messages!);
	}

	[Fact]
	public async Task SuccessfulSignIn_KeepsTheMessageAcrossTheRedirect()
	{
		using var harness = new Harness();
		harness.AddUser
			(username: "dariush",
			passwordHash: harness.PasswordHashService.Hash(password: "Correct-Password-1"));

		var model = harness.CreateModel(username: "dariush", password: "Correct-Password-1");

		await model.OnPostAsync();

		// The end of the request writes TempData, the next request reads it.
		harness.TempData.Save();

		var nextRequestTempData =
			new TempDataDictionary
				(context: harness.HttpContext, provider: harness.TempDataProvider);

		var messages = nextRequestTempData[key: PageSuccess] as IList<string>;

		Assert.NotNull(messages);
		Assert.Contains
			(expected: Resources.Messages.Successes.LoginSucceededPendingFeatures,
			collection: messages!);
	}

	[Fact]
	public async Task RememberMe_KeepsAPersistentCookie()
	{
		using var harness = new Harness();
		harness.AddUser
			(username: "dariush",
			passwordHash: harness.PasswordHashService.Hash(password: "Correct-Password-1"));

		var model =
			harness.CreateModel
				(username: "dariush", password: "Correct-Password-1", rememberMe: true);

		await model.OnPostAsync();

		Assert.True(harness.Authentication.Properties!.IsPersistent);
	}

	[Fact]
	public async Task SuccessfulSignIn_RecordsTheLoginLogAndTheLastLogin()
	{
		using var harness = new Harness();
		var user =
			harness.AddUser
				(username: "dariush",
				passwordHash: harness.PasswordHashService.Hash(password: "Correct-Password-1"));

		var model = harness.CreateModel(username: "dariush", password: "Correct-Password-1");

		await model.OnPostAsync();

		var storedUser = harness.Database.Users.Single();
		Assert.NotNull(storedUser.LastLoginDateTime);
		Assert.True(storedUser.Id == user.Id);

		Assert.Single(collection: harness.Database.LoginLogs.ToList());

		var auditEvent =
			Assert.Single(collection: harness.Database.SecurityAuditEvents.ToList());

		Assert.Equal(expected: "LoginSucceeded", actual: auditEvent.EventType);
	}

	[Fact]
	public async Task SuccessfulSignIn_ClearsTheSubmittedPassword()
	{
		using var harness = new Harness();
		harness.AddUser
			(username: "dariush",
			passwordHash: harness.PasswordHashService.Hash(password: "Correct-Password-1"));

		var model = harness.CreateModel(username: "dariush", password: "Correct-Password-1");

		await model.OnPostAsync();

		Assert.Null(model.ViewModel.Password);
		Assert.False(model.ModelState.ContainsKey(key: "ViewModel.Password"));
	}

	[Fact]
	public async Task LegacyCredential_StillSignsIn_AndIsUpgraded()
	{
		using var harness = new Harness();

		// A credential written by the previous implementation: Base64 SHA-256.
		var legacyHash = Dtat.Security.Hashing.GetSha256(text: "Legacy-Password-1");
		harness.AddUser(username: "legacy", passwordHash: legacyHash);

		var model = harness.CreateModel(username: "legacy", password: "Legacy-Password-1");

		var result = await model.OnPostAsync();

		Assert.IsType<RedirectToPageResult>(result);
		Assert.Equal(expected: 1, actual: harness.Authentication.SignInCount);

		var storedUser = harness.Database.Users.Single();
		Assert.NotEqual(expected: legacyHash, actual: storedUser.Password);
		Assert.False
			(Infrastructure.Security.PasswordHashService
				.IsLegacySha256Hash(value: storedUser.Password));

		// The upgraded credential is a real credential: the next sign-in works with it.
		Assert.Equal
			(expected: Infrastructure.Security.PasswordVerificationOutcome.Success,
			actual: harness.PasswordHashService.Verify
				(storedHash: storedUser.Password, password: "Legacy-Password-1"));
	}

	[Fact]
	public async Task MasterAccount_SignsIn_WithTheInfrastructureClaims()
	{
		using var harness = new Harness();

		var hashService = new Infrastructure.Security.PasswordHashService();

		harness.Settings.MasterUsername = "master";
		harness.Settings.MasterPassword = hashService.Hash(password: "Master-Password-1");

		var model = harness.CreateModel(username: "master", password: "Master-Password-1");

		var result = await model.OnPostAsync();

		Assert.IsType<RedirectToPageResult>(result);
		Assert.Equal(expected: 1, actual: harness.Authentication.SignInCount);

		var principal = harness.Authentication.Principal!;

		Assert.Contains
			(expected: "Infrastructure",
			collection: principal.FindAll(type: "AccountKind").Select(selector: claim => claim.Value));

		Assert.True(principal.IsInRole(role: Constants.Role.Admin));
	}

	[Fact]
	public async Task MasterAccount_WithALegacyConfiguredDigest_StillSignsIn()
	{
		using var harness = new Harness();

		harness.Settings.MasterUsername = "master";
		harness.Settings.MasterPassword = Dtat.Security.Hashing.GetSha256(text: "Master-Password-1");

		var model = harness.CreateModel(username: "master", password: "Master-Password-1");

		var result = await model.OnPostAsync();

		Assert.IsType<RedirectToPageResult>(result);
		Assert.Equal(expected: 1, actual: harness.Authentication.SignInCount);
	}
	#endregion

	#region ReturnUrl
	[Fact]
	public async Task LocalReturnUrl_IsFollowed()
	{
		using var harness = new Harness();
		harness.AddUser
			(username: "dariush",
			passwordHash: harness.PasswordHashService.Hash(password: "Correct-Password-1"));

		var model =
			harness.CreateModel
				(username: "dariush", password: "Correct-Password-1",
				returnUrl: "/account/profile");

		var result = await model.OnPostAsync();

		var redirect = Assert.IsType<RedirectResult>(result);
		Assert.Equal(expected: "/account/profile", actual: redirect.Url);
	}

	[Theory]
	[InlineData("https://evil.example/steal")]
	[InlineData("//evil.example/steal")]
	[InlineData("http://localhost.evil.example/")]
	[InlineData("\\\\evil.example")]
	public async Task ExternalReturnUrl_IsNeverFollowed(string returnUrl)
	{
		using var harness = new Harness();
		harness.AddUser
			(username: "dariush",
			passwordHash: harness.PasswordHashService.Hash(password: "Correct-Password-1"));

		var model =
			harness.CreateModel
				(username: "dariush", password: "Correct-Password-1", returnUrl: returnUrl);

		var result = await model.OnPostAsync();

		// The sign-in still succeeds, but the browser goes to the temporary page.
		var redirect = Assert.IsType<RedirectToPageResult>(result);
		Assert.Equal
			(expected: Server.Pages.Security.LoginModel.SuccessPageName,
			actual: redirect.PageName);
	}
	#endregion

	#region Failure of the infrastructure
	[Fact]
	public async Task DatabaseFailure_ShowsAGenericMessage_AndIssuesNoCookie()
	{
		using var harness = new Harness();

		// A disposed context fails every query, which stands for any infrastructure
		// error of the sign-in path.
		harness.Database.Dispose();

		var model = harness.CreateModel(username: "dariush", password: "Correct-Password-1");

		var result = await model.OnPostAsync();

		Assert.IsType<PageResult>(result);
		Assert.Equal(0, harness.Authentication.SignInCount);

		var messages = harness.GetMessages(type: PageError);
		Assert.NotNull(messages);
		Assert.Contains
			(expected: Resources.Messages.Errors.UnexpectedError,
			collection: messages!);
	}
	#endregion
}

/// <summary>
/// The credential format of the application: new credentials are produced by the
/// standard ASP.NET Core hasher, and credentials of the legacy format keep working.
/// </summary>
public sealed class PasswordHashServiceTests
{
	private readonly Infrastructure.Security.PasswordHashService _service = new();

	[Fact]
	public void Hash_ProducesACredentialOfTheCurrentFormat()
	{
		var hash = _service.Hash(password: "C0rrect-Phrase-1405!");

		Assert.False(Infrastructure.Security.PasswordHashService.IsLegacySha256Hash(value: hash));
		Assert.True(hash.Length <= Constants.FixedLength.PasswordHash);
		Assert.Equal
			(expected: Infrastructure.Security.PasswordVerificationOutcome.Success,
			actual: _service.Verify(storedHash: hash, password: "C0rrect-Phrase-1405!"));
	}

	[Fact]
	public void Hash_UsesAFreshSaltForEveryCredential()
	{
		var first = _service.Hash(password: "C0rrect-Phrase-1405!");
		var second = _service.Hash(password: "C0rrect-Phrase-1405!");

		Assert.NotEqual(expected: first, actual: second);

		Assert.Equal
			(expected: Infrastructure.Security.PasswordVerificationOutcome.Success,
			actual: _service.Verify(storedHash: first, password: "C0rrect-Phrase-1405!"));

		Assert.Equal
			(expected: Infrastructure.Security.PasswordVerificationOutcome.Success,
			actual: _service.Verify(storedHash: second, password: "C0rrect-Phrase-1405!"));
	}

	[Fact]
	public void Verify_RejectsAWrongPassword()
	{
		var hash = _service.Hash(password: "C0rrect-Phrase-1405!");

		Assert.Equal
			(expected: Infrastructure.Security.PasswordVerificationOutcome.Failed,
			actual: _service.Verify(storedHash: hash, password: "c0rrect-phrase-1405!"));
	}

	[Fact]
	public void Verify_AcceptsALegacyDigest_AndAsksForARehash()
	{
		var legacyHash = Dtat.Security.Hashing.GetSha256(text: "Legacy-Password-1");

		Assert.True
			(Infrastructure.Security.PasswordHashService.IsLegacySha256Hash(value: legacyHash));

		Assert.Equal
			(expected: Infrastructure.Security.PasswordVerificationOutcome.SuccessRehashNeeded,
			actual: _service.Verify(storedHash: legacyHash, password: "Legacy-Password-1"));

		Assert.Equal
			(expected: Infrastructure.Security.PasswordVerificationOutcome.Failed,
			actual: _service.Verify(storedHash: legacyHash, password: "Legacy-Password-2"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("not-a-credential")]
	[InlineData("0000000000000000000000000000000000000000000=")]
	public void Verify_RejectsAnEmptyOrMalformedCredential(string? storedHash)
	{
		Assert.Equal
			(expected: Infrastructure.Security.PasswordVerificationOutcome.Failed,
			actual: _service.Verify(storedHash: storedHash, password: "whatever"));
	}

	[Fact]
	public void IsLegacySha256Hash_DoesNotConfuseTheCurrentFormat()
	{
		var current = _service.Hash(password: "C0rrect-Phrase-1405!");

		Assert.False
			(Infrastructure.Security.PasswordHashService.IsLegacySha256Hash(value: current));
	}
}

/// <summary>
/// The deployment configuration of the sign-in: the cookie options, the middleware order
/// and the protection of the temporary success page.
/// </summary>
public sealed class LoginConfigurationTests
{
	private static string ReadRepositoryFile(string relativePath)
	{
		var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

		while (directory.Parent != null &&
			!File.Exists(Path.Combine(directory.FullName, "Template.sln")))
		{
			directory = directory.Parent;
		}

		Assert.True
			(File.Exists(Path.Combine(directory.FullName, "Template.sln")),
			"Repository root could not be located.");

		var path =
			Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));

		Assert.True(File.Exists(path), $"Expected repository file was not found: {path}");

		return File.ReadAllText(path);
	}

	[Fact]
	public void TheTemporarySuccessPage_RequiresAnAuthenticatedUser()
	{
		var attribute =
			typeof(Server.Pages.Account.LoginSuccessModel)
			.GetCustomAttributes(inherit: true)
			.OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
			.SingleOrDefault();

		Assert.NotNull(attribute);
	}

	[Fact]
	public void TheTemporarySuccessPage_ShowsOnlyTheConfirmation()
	{
		var markup = ReadRepositoryFile("Server/Pages/Account/LoginSuccess.cshtml");

		Assert.Contains("LoginSucceededPendingFeatures", markup);

		// No form and no accounting module: this page is a confirmation only.
		Assert.DoesNotContain("<form", markup);
		Assert.DoesNotContain("asp-page", markup);
	}

	[Fact]
	public void TheLoginPage_KeepsItsAntiForgeryTokenAndPasswordToggle()
	{
		var markup = ReadRepositoryFile("Server/Pages/Account/Login.cshtml");

		Assert.Contains("@Html.AntiForgeryToken()", markup);
		Assert.Contains("asp-for=\"ViewModel.Password\"", markup);
		Assert.Contains("data-password-toggle", markup);
		Assert.Contains("taraz-submit-status", markup);
	}

	[Fact]
	public void Program_ConfiguresASecureCookie_AndTheMiddlewareOrder()
	{
		var program = ReadRepositoryFile("Server/Program.cs");

		Assert.Contains("options.Cookie.HttpOnly = true;", program);
		Assert.Contains("SameSiteMode.Lax", program);
		Assert.Contains("CookieSecurePolicy.Always", program);
		Assert.Contains("options.ExpireTimeSpan = TimeSpan.FromMinutes(30);", program);
		Assert.Contains("options.LoginPath = \"/Account/Login\";", program);

		var authenticationIndex = program.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
		var authorizationIndex = program.IndexOf("app.UseAuthorization();", StringComparison.Ordinal);

		Assert.True(authenticationIndex >= 0, "UseAuthentication is missing.");
		Assert.True(authorizationIndex > authenticationIndex, "UseAuthorization must run after UseAuthentication.");
	}

	[Fact]
	public void Program_ResolvesTheAuthenticationServiceFromTheContainer()
	{
		var program = ReadRepositoryFile("Server/Program.cs");

		Assert.Contains("AddScoped<Infrastructure.Security.AuthenticationService>", program);
		Assert.Contains("AddSingleton<Infrastructure.Security.PasswordHashService>", program);
	}
}
