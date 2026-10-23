using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Infrastructure.DatabaseSetup;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Template.Tests;

/// <summary>
/// The real first-run host (Program.cs) started in SETUP mode: no connection string and a temp
/// settings folder. The in-memory configuration sets <c>ConnectionStrings:ConnectionString</c> to an
/// empty value on purpose, so the process environment variable that the other integration factory
/// (<c>FaranouApplicationFactory</c>) sets can never switch this host out of setup mode, and this
/// factory does not change any process-wide state itself.
/// </summary>
internal sealed class SetupModeFactory : WebApplicationFactory<Program>
{
	private readonly string settingsDirectory;

	public SetupModeFactory(string settingsDirectory)
	{
		this.settingsDirectory = settingsDirectory;
	}

	public CapturingLoggerProvider Logs { get; } = new();

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("Testing");

		// Program.cs reads these two values while the builder is created, i.e. BEFORE the callbacks of
		// ConfigureAppConfiguration are applied (verified: the settings folder was ignored). Host
		// settings are in place by then, and they win over the process environment variables.
		builder.UseSetting("ConnectionStrings:ConnectionString", string.Empty);
		builder.UseSetting(FaranoSettingsNames.SettingsDirectoryKey, settingsDirectory);
		builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
		{
			["ConnectionStrings:ConnectionString"] = string.Empty,
			[FaranoSettingsNames.SettingsDirectoryKey] = settingsDirectory,
		}));
		builder.ConfigureLogging(logging => logging.AddProvider(Logs));
	}
}

/// <summary>Captures every log entry of every category (to prove that no secret reaches a log).</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
	private readonly List<string> entries = [];

	public string All
	{
		get
		{
			lock (entries)
			{
				return string.Join("\n", entries);
			}
		}
	}

	public ILogger CreateLogger(string categoryName) => new Capture(this, categoryName);

	public void Dispose()
	{
	}

	private sealed class Capture(CapturingLoggerProvider owner, string category) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			var text = $"[{category}] {logLevel}: {formatter(state, exception)}";
			if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
			{
				text += " " + string.Join(" ", pairs.Select(pair => pair.Key + "=" + pair.Value));
			}

			if (exception is not null)
			{
				text += " EX:" + exception;
			}

			lock (owner.entries)
			{
				owner.entries.Add(text);
			}
		}
	}
}

/// <summary>A started setup host, its temp settings folder and a browser-like client.</summary>
internal sealed partial class SetupPage : IDisposable
{
	private readonly TempFolder folder = new();

	private SetupPage(Action<string>? prepare)
	{
		SettingsDirectory = folder.Combine("settings");
		Directory.CreateDirectory(SettingsDirectory);
		prepare?.Invoke(SettingsDirectory);

		Factory = new SetupModeFactory(SettingsDirectory);
		Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
		{
			BaseAddress = new Uri("https://localhost"),
			AllowAutoRedirect = false,
			HandleCookies = true,
		});
	}

	public static SetupPage Start(Action<string>? prepare = null) => new(prepare);

	public string SettingsDirectory { get; }

	public SetupModeFactory Factory { get; }

	public HttpClient Client { get; }

	public string SettingsFile => Path.Combine(SettingsDirectory, FaranoSettingsNames.SettingsFileName);

	public string TokenFile => Path.Combine(SettingsDirectory, FaranoSettingsNames.SetupTokenFileName);

	public string ReadSetupToken() => File.ReadAllText(TokenFile);

	public void Dispose()
	{
		Client.Dispose();
		Factory.Dispose();
		folder.Dispose();
	}

	// ----------------------------------------------------------------- HTTP

	public async Task<PageResponse> GetAsync()
	{
		var response = await Client.GetAsync("/DatabaseSetup", TestContext.Current.CancellationToken);
		return await PageResponse.CreateAsync(response);
	}

	/// <summary>Posts the setup form with a real anti-forgery token (from a fresh GET) and cookie.</summary>
	public async Task<PageResponse> PostAsync(IEnumerable<KeyValuePair<string, string>> fields, string? antiforgeryToken = null)
	{
		antiforgeryToken ??= (await GetAsync()).AntiforgeryToken;
		return await PostRawAsync(fields, antiforgeryToken);
	}

	public async Task<PageResponse> PostRawAsync(IEnumerable<KeyValuePair<string, string>> fields, string? antiforgeryToken)
	{
		var all = fields.ToList();
		if (antiforgeryToken is not null)
		{
			all.Add(new("__RequestVerificationToken", antiforgeryToken));
		}

		var response = await Client.PostAsync("/DatabaseSetup", new FormUrlEncodedContent(all), TestContext.Current.CancellationToken);
		return await PageResponse.CreateAsync(response);
	}

	/// <summary>
	/// A POST for pages that offer no form (so no token can be scraped): the token and cookie are
	/// issued by the host's own anti-forgery service, sent by a client with its own cookie handling.
	/// </summary>
	public async Task<PageResponse> PostWithIssuedTokenAsync(IEnumerable<KeyValuePair<string, string>> fields)
	{
		var antiforgery = Factory.Services.GetRequiredService<IAntiforgery>();
		var context = new DefaultHttpContext { RequestServices = Factory.Services };
		context.Request.Scheme = "https";
		var tokens = antiforgery.GetAndStoreTokens(context);
		var cookie = context.Response.Headers.SetCookie.ToString().Split(';')[0];

		using var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
		{
			BaseAddress = new Uri("https://localhost"),
			AllowAutoRedirect = false,
			HandleCookies = false,
		});
		var all = fields.ToList();
		all.Add(new("__RequestVerificationToken", tokens.RequestToken!));
		using var request = new HttpRequestMessage(HttpMethod.Post, "/DatabaseSetup") { Content = new FormUrlEncodedContent(all) };
		request.Headers.Add("Cookie", cookie);
		var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
		return await PageResponse.CreateAsync(response);
	}

	// ----------------------------------------------------------------- form data

	public static List<KeyValuePair<string, string>> Form(
		string token,
		string server = "127.0.0.1,1",
		string database = "Farano",
		string mode = "Windows",
		string? userName = null,
		string? password = null,
		bool trustServerCertificate = false,
		bool create = true,
		bool confirmReplace = false)
	{
		var fields = new List<KeyValuePair<string, string>>
		{
			new("Input.SetupToken", token),
			new("Input.Server", server),
			new("Input.Database", database),
			new("Input.AuthenticationMode", mode),
			new("Input.UserName", userName ?? string.Empty),
			new("Input.Password", password ?? string.Empty),
			new("Input.TrustServerCertificate", trustServerCertificate ? "true" : "false"),
			new("Input.CreateDatabaseIfMissing", create ? "true" : "false"),
			new("Input.ContinuePartialMigrations", "false"),
			new("Input.ConfirmReplace", confirmReplace ? "true" : "false"),
		};
		return fields;
	}
}

/// <summary>A response with the raw HTML and the HTML-decoded text (Razor encodes Persian text).</summary>
internal sealed partial class PageResponse
{
	private PageResponse(HttpResponseMessage response, string html)
	{
		Status = response.StatusCode;
		Headers = response;
		Html = html;
		Text = WebUtility.HtmlDecode(html);
	}

	public HttpStatusCode Status { get; }

	public HttpResponseMessage Headers { get; }

	public string Html { get; }

	public string Text { get; }

	public static async Task<PageResponse> CreateAsync(HttpResponseMessage response)
	{
		var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		return new PageResponse(response, html);
	}

	[GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"|<input[^>]*value=\"([^\"]+)\"[^>]*name=\"__RequestVerificationToken\"")]
	private static partial Regex TokenPattern();

	public string AntiforgeryToken
	{
		get
		{
			var match = TokenPattern().Match(Html);
			Assert.True(match.Success, $"No anti-forgery token on the page (HTTP {(int)Status}).");
			return WebUtility.HtmlDecode(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
		}
	}

	/// <summary>The start tag of the element (input, select, ...) with the given id, or null.</summary>
	public string? Tag(string id)
	{
		var match = Regex.Match(Html, "<(?:input|select|textarea)\\b[^>]*\\bid=\"" + Regex.Escape(id) + "\"[^>]*>", RegexOptions.Singleline);
		return match.Success ? match.Value : null;
	}

	public static string? Attribute(string tag, string name)
	{
		var match = Regex.Match(tag, "\\s" + Regex.Escape(name) + "=\"([^\"]*)\"");
		return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : null;
	}

	public string? Value(string id) => Tag(id) is { } tag ? Attribute(tag, "value") : null;

	/// <summary>The value of the selected option of a select, whatever the attribute order.</summary>
	public string? SelectedOption(string selectId)
	{
		var block = Regex.Match(Html, "<select\\b[^>]*\\bid=\"" + Regex.Escape(selectId) + "\"[^>]*>(.*?)</select>", RegexOptions.Singleline);
		if (!block.Success)
		{
			return null;
		}

		foreach (Match option in Regex.Matches(block.Groups[1].Value, "<option\\b[^>]*>"))
		{
			if (Regex.IsMatch(option.Value, "\\sselected\\b"))
			{
				return Attribute(option.Value, "value");
			}
		}

		return null;
	}

	/// <summary>True when the control is labelled by for=id or sits inside a label element.</summary>
	public bool IsLabelled(string controlTag, string id)
	{
		if (Regex.IsMatch(Html, "<label\\b[^>]*\\bfor=\"" + Regex.Escape(id) + "\""))
		{
			return true;
		}

		return Regex.Matches(Html, "<label\\b[^>]*>(.*?)</label>", RegexOptions.Singleline)
			.Any(label => label.Groups[1].Value.Contains(controlTag, StringComparison.Ordinal));
	}

	public bool HasForm => Regex.IsMatch(Html, "<form\\b", RegexOptions.IgnoreCase);

	/// <summary>The inline error slot next to a field: (exists, visible, text).</summary>
	public (bool Exists, bool Visible, string Text) FieldError(string field)
	{
		var match = Regex.Match(
			Html,
			"<span\\b([^>]*)data-error-for=\"" + Regex.Escape(field) + "\"([^>]*)>(.*?)</span>",
			RegexOptions.Singleline);
		if (!match.Success)
		{
			return (false, false, string.Empty);
		}

		var attributes = match.Groups[1].Value + match.Groups[2].Value;
		var text = WebUtility.HtmlDecode(match.Groups[3].Value).Trim();
		return (true, !Regex.IsMatch(attributes, "\\bhidden\\b") && text.Length > 0, text);
	}
}

/// <summary>
/// HTTP tests of the first-run page /DatabaseSetup through the real pipeline of Program.cs, started in
/// setup mode with a temp settings folder. Nothing here needs SQL Server: connections go to a refused
/// port. The successful path against a real SQL Server is in <c>DatabaseSetupPageSqlServerTests</c>.
/// </summary>
public sealed partial class DatabaseSetupPageTests
{
	private const string GenericFormError = "اطلاعات فرم را بررسی کنید";
	private const string FieldCountError = "مورد در فرم نیاز به اصلاح";
	private const string Sentinel = "Sentinel-Pw!7f3a9c-Ünï";

	private static readonly DatabaseSetupErrorCode[] UnreachableFamily =
		[DatabaseSetupErrorCode.ServerUnreachable, DatabaseSetupErrorCode.InstanceNotFound, DatabaseSetupErrorCode.Timeout];

	private static string Serialized(string folder) => LegacyFixture.Snapshot(folder);

	// ----------------------------------------------------------------- a) GET

	[Fact]
	public async Task Get_ReturnsTheLocalisedRtlFormWithLocalAssetsOnly()
	{
		using var page = SetupPage.Start();

		var response = await page.GetAsync();

		Assert.Equal(HttpStatusCode.OK, response.Status);
		Assert.Contains("<html lang=\"fa\" dir=\"rtl\">", response.Html);

		var title = Regex.Match(response.Html, "<title>(.*?)</title>", RegexOptions.Singleline).Groups[1].Value;
		title = WebUtility.HtmlDecode(title);
		Assert.Contains("فرانو", title);
		Assert.DoesNotContain("تراز آوا", response.Text);

		// CSP-friendly: no inline script bodies, no style attribute / element, no on*= handler attributes.
		foreach (Match script in Regex.Matches(response.Html, "<script\\b[^>]*>(.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
		{
			Assert.True(string.IsNullOrWhiteSpace(script.Groups[1].Value), "Inline script body found: " + script.Value);
		}

		Assert.DoesNotMatch(new Regex("<style\\b", RegexOptions.IgnoreCase), response.Html);
		Assert.DoesNotMatch(new Regex("<[^>]*\\sstyle\\s*=", RegexOptions.IgnoreCase), response.Html);
		Assert.DoesNotMatch(new Regex("<[^>]*\\son[a-z]+\\s*=", RegexOptions.IgnoreCase), response.Html);

		// Local stylesheet and script only: no external URL in any href/src/action.
		var stylesheets = Regex.Matches(response.Html, "<link\\b[^>]*rel=\"stylesheet\"[^>]*href=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
		var scripts = Regex.Matches(response.Html, "<script\\b[^>]*\\bsrc=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
		Assert.NotEmpty(stylesheets);
		Assert.NotEmpty(scripts);
		Assert.All(stylesheets.Concat(scripts), url => Assert.StartsWith("/", url));
		Assert.DoesNotMatch(new Regex("(href|src|action)=\"(https?:)?//", RegexOptions.IgnoreCase), response.Html);
		Assert.DoesNotMatch(new Regex("url\\(\\s*['\"]?https?://", RegexOptions.IgnoreCase), response.Html);

		// The local assets are really served (not just referenced).
		foreach (var url in stylesheets.Concat(scripts))
		{
			var asset = await page.Client.GetAsync(url, TestContext.Current.CancellationToken);
			Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
		}
	}

	[Fact]
	public async Task Get_EveryInputHasAMatchingLabel_AndDefaultsAreRight()
	{
		using var page = SetupPage.Start();

		var response = await page.GetAsync();

		var labelTargets = Regex.Matches(response.Html, "<label\\b[^>]*\\bfor=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
		var controls = Regex.Matches(response.Html, "<(?:input|select|textarea)\\b[^>]*>", RegexOptions.Singleline)
			.Select(m => m.Value)
			.Where(tag => !Regex.IsMatch(tag, "type=\"hidden\""))
			.ToList();
		Assert.NotEmpty(controls);

		foreach (var tag in controls)
		{
			var id = PageResponse.Attribute(tag, "id");
			Assert.False(string.IsNullOrEmpty(id), "A visible control has no id: " + tag);
			Assert.True(response.IsLabelled(tag, id!), "No label for control " + id);
		}

		// Every label points to an existing control.
		var ids = Regex.Matches(response.Html, "\\sid=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
		Assert.All(labelTargets, target => Assert.Contains(target, ids));

		Assert.Equal("password", PageResponse.Attribute(response.Tag("Input_Password")!, "type"));
		Assert.Equal(string.Empty, response.Value("Input_Server"));
		Assert.Equal("Farano", response.Value("Input_Database"));
		Assert.Contains("data-error-for=\"Server\"", response.Html);
	}

	[Fact]
	public async Task Get_InSetupMode_CreatesTheTokenFileOnlyInTheTempFolder_AndRedirectsOtherPages()
	{
		using var page = SetupPage.Start();

		var response = await page.GetAsync();
		Assert.Equal(HttpStatusCode.OK, response.Status);

		Assert.True(File.Exists(page.TokenFile));
		Assert.False(string.IsNullOrWhiteSpace(page.ReadSetupToken()));
		Assert.False(File.Exists(page.SettingsFile));
		Assert.DoesNotContain(page.ReadSetupToken(), response.Html);

		var other = await page.Client.GetAsync("/", TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.Redirect, other.StatusCode);
		Assert.Equal("/DatabaseSetup", other.Headers.Location!.OriginalString, ignoreCase: true);
	}

	// ----------------------------------------------------------------- b) connection failure

	[Fact]
	public async Task Post_WindowsAuth_ToARefusedPort_ShowsAConnectionFailure_NotTheGenericFormError()
	{
		using var page = SetupPage.Start();
		var token = (await page.GetAsync()).AntiforgeryToken;

		// Regression: the Master form is a second [BindProperty] whose [Required] fields are never posted
		// here; they used to make ModelState invalid on every POST of this form.
		var response = await page.PostAsync(SetupPage.Form(page.ReadSetupToken()), token);

		Assert.Equal(HttpStatusCode.OK, response.Status);
		Assert.DoesNotContain(GenericFormError, response.Text);
		Assert.DoesNotContain(FieldCountError, response.Text);

		var expected = UnreachableFamily.Select(code => DatabaseSetupErrorCatalog.Describe(code).Message).ToList();
		Assert.Contains(expected, message => response.Text.Contains(message, StringComparison.Ordinal));
		Assert.Matches("شناسه پیگیری: [0-9a-f]{12}", response.Text);

		// The stage list shows what was reached and which stage failed.
		Assert.Contains("<ol class=\"steps\"", response.Html);
		Assert.Contains("data-state=\"ok\"", response.Html);
		Assert.Contains("data-state=\"failed\"", response.Html);
		Assert.Contains("آزمایش اتصال: ناموفق", response.Text);

		// Typed values are preserved.
		Assert.Equal("127.0.0.1,1", response.Value("Input_Server"));
		Assert.Equal("Farano", response.Value("Input_Database"));
		Assert.Equal("Windows", response.SelectedOption("Input_AuthenticationMode"));

		Assert.False(File.Exists(page.SettingsFile));
		Assert.True(File.Exists(page.TokenFile), "The setup token must survive a failed attempt.");
	}

	[Fact]
	public async Task Post_SqlAuth_ToARefusedPort_KeepsInputs_ButNeverEchoesOrLogsThePassword()
	{
		using var page = SetupPage.Start();

		var response = await page.PostAsync(SetupPage.Form(
			page.ReadSetupToken(), mode: "Sql", userName: "farano_user", password: Sentinel, trustServerCertificate: true));

		Assert.Equal(HttpStatusCode.OK, response.Status);
		Assert.DoesNotContain(GenericFormError, response.Text);
		Assert.Contains(
			UnreachableFamily.Select(code => DatabaseSetupErrorCatalog.Describe(code).Message),
			message => response.Text.Contains(message, StringComparison.Ordinal));

		Assert.Equal("farano_user", response.Value("Input_UserName"));
		Assert.Equal("Sql", response.SelectedOption("Input_AuthenticationMode"));
		Assert.Contains("type=\"password\"", response.Tag("Input_Password")!);
		var passwordValue = response.Value("Input_Password");
		Assert.True(string.IsNullOrEmpty(passwordValue), "The password input must not be pre-filled.");

		Assert.DoesNotContain(Sentinel, response.Html);
		Assert.DoesNotContain(Sentinel, response.Text);
		Assert.DoesNotContain(WebUtility.HtmlEncode(Sentinel), response.Html);
		Assert.DoesNotContain(Uri.EscapeDataString(Sentinel), response.Html);
		Assert.DoesNotContain(Sentinel, page.Factory.Logs.All);
		Assert.False(File.Exists(page.SettingsFile));
	}

	// ----------------------------------------------------------------- c) all field errors at once

	[Fact]
	public async Task Post_WithSeveralInvalidFields_ShowsEveryMessageNextToItsOwnField_InOneResponse()
	{
		using var page = SetupPage.Start();

		var response = await page.PostAsync(SetupPage.Form(
			"definitely-not-the-token", server: string.Empty, database: new string('d', 129), mode: "Sql"));

		Assert.Equal(HttpStatusCode.OK, response.Status);
		Assert.DoesNotContain(GenericFormError, response.Text);

		var expectedMessages = new Dictionary<string, string>
		{
			["SetupToken"] = "کد راه‌اندازی معتبر نیست",
			["Server"] = "نام یا نشانی سرور الزامی است",
			["Database"] = "نام دیتابیس حداکثر ۱۲۳ نویسه است",
			["UserName"] = "نام کاربری لازم است",
			["Password"] = "رمز عبور لازم است",
		};

		foreach (var (field, expected) in expectedMessages)
		{
			var (exists, visible, text) = response.FieldError(field);
			Assert.True(exists, $"No error slot for {field}.");
			Assert.True(visible, $"The error slot of {field} is hidden or empty.");
			Assert.Contains(expected, text);
		}

		// No English framework message in any visible error slot (the data-val-* attributes are inert: novalidate).
		foreach (var field in expectedMessages.Keys)
		{
			Assert.DoesNotContain("is required", response.FieldError(field).Text, StringComparison.OrdinalIgnoreCase);
		}

		Assert.False(response.FieldError("AuthenticationMode").Visible);

		var summary = $"{expectedMessages.Count} {FieldCountError}";
		Assert.Contains(summary, response.Text);

		// Typed values survive; nothing was attempted.
		Assert.Equal(new string('d', 129), response.Value("Input_Database"));
		Assert.False(File.Exists(page.SettingsFile));
		Assert.DoesNotContain("Database setup", page.Factory.Logs.All);
	}

	// ----------------------------------------------------------------- d) anti-forgery and token

	[Fact]
	public async Task Post_WithoutAntiforgeryToken_Is400_AndChangesNothing()
	{
		using var page = SetupPage.Start();
		var before = Serialized(page.SettingsDirectory);

		var response = await page.PostRawAsync(SetupPage.Form(page.ReadSetupToken()), antiforgeryToken: null);

		Assert.Equal(HttpStatusCode.BadRequest, response.Status);
		Assert.Equal(before, Serialized(page.SettingsDirectory));
		Assert.DoesNotContain("Database setup", page.Factory.Logs.All);
	}

	[Fact]
	public async Task Post_WithAWrongSetupToken_FailsOnThatField_WritesNothing_AndNeverConnects()
	{
		using var page = SetupPage.Start();
		var before = Serialized(page.SettingsDirectory);

		var response = await page.PostAsync(SetupPage.Form("wrong-token-value"));

		Assert.Equal(HttpStatusCode.OK, response.Status);
		var (_, visible, text) = response.FieldError("SetupToken");
		Assert.True(visible);
		Assert.Contains("کد راه‌اندازی معتبر نیست", text);
		Assert.DoesNotContain(GenericFormError, response.Text);
		Assert.False(response.FieldError("Server").Visible);

		Assert.DoesNotContain("<ol class=\"steps\"", response.Html);
		Assert.DoesNotContain("شناسه پیگیری", response.Text);
		Assert.False(File.Exists(page.SettingsFile));
		Assert.Equal(before, Serialized(page.SettingsDirectory));
		Assert.DoesNotContain("Database setup", page.Factory.Logs.All);
	}

	// ----------------------------------------------------------------- e) crafted request

	[Fact]
	public async Task Post_CraftedDirectly_WithAnUnknownModeAndControlCharacters_ReturnsFieldErrors_WithoutAnException()
	{
		using var page = SetupPage.Start();

		var response = await page.PostAsync(SetupPage.Form(
			page.ReadSetupToken(), server: "host\tname\r\nX", database: "db\u0001name", mode: "Hax"));

		Assert.Equal(HttpStatusCode.OK, response.Status);
		Assert.DoesNotContain(GenericFormError, response.Text);

		Assert.Contains("نویسه کنترلی", response.FieldError("Server").Text);
		Assert.Contains("فقط می‌تواند حروف", response.FieldError("Database").Text);
		Assert.Contains("«Windows» یا «Sql»", response.FieldError("AuthenticationMode").Text);
		Assert.DoesNotContain("<ol class=\"steps\"", response.Html);

		Assert.False(File.Exists(page.SettingsFile));
		Assert.DoesNotContain("Unhandled", page.Factory.Logs.All);
		Assert.DoesNotContain("Database setup", page.Factory.Logs.All);
	}

	// ----------------------------------------------------------------- f) corrupt settings file

	private static void WriteCorruptSettings(string directory) =>
		File.WriteAllText(Path.Combine(directory, FaranoSettingsNames.SettingsFileName), "{ this is not json");

	[Fact]
	public async Task Get_WithACorruptSettingsFile_ShowsTheWarningAndTheReplaceConfirmation()
	{
		using var page = SetupPage.Start(WriteCorruptSettings);

		var response = await page.GetAsync();

		Assert.Equal(HttpStatusCode.OK, response.Status);
		Assert.Contains("فایل تنظیمات اتصال خراب است", response.Text);
		Assert.Contains("class=\"alert warning\"", response.Html);
		Assert.Contains(page.SettingsDirectory, response.Text);
		Assert.NotNull(response.Tag("Input_ConfirmReplace"));
		Assert.Contains("جایگزینی آن را تأیید می‌کنم", response.Text);
		Assert.True(response.HasForm);
		Assert.NotNull(response.Tag("Input_Server"));
	}

	[Fact]
	public async Task Post_ReplacingACorruptFile_WithoutConfirmation_FailsOnThatField_AndLeavesTheFileByteForByteUnchanged()
	{
		using var page = SetupPage.Start(WriteCorruptSettings);
		var bytesBefore = await File.ReadAllBytesAsync(page.SettingsFile, TestContext.Current.CancellationToken);
		var snapshotBefore = Serialized(page.SettingsDirectory);

		var response = await page.PostAsync(SetupPage.Form(page.ReadSetupToken(), confirmReplace: false));

		Assert.Equal(HttpStatusCode.OK, response.Status);
		var (_, visible, text) = response.FieldError("ConfirmReplace");
		Assert.True(visible);
		Assert.Contains("تیک تأیید جایگزینی", text);
		Assert.DoesNotContain(GenericFormError, response.Text);
		Assert.DoesNotContain("<ol class=\"steps\"", response.Html);

		Assert.Equal(bytesBefore, await File.ReadAllBytesAsync(page.SettingsFile, TestContext.Current.CancellationToken));
		Assert.Equal(snapshotBefore, Serialized(page.SettingsDirectory));
		Assert.False(Directory.Exists(Path.Combine(page.SettingsDirectory, FaranoSettingsNames.BackupsFolderName)));
		Assert.DoesNotContain("Database setup", page.Factory.Logs.All);
	}

	[Fact]
	public async Task Post_ReplacingACorruptFile_WithConfirmation_ButAFailedConnection_StillLeavesTheFileUnchanged()
	{
		using var page = SetupPage.Start(WriteCorruptSettings);
		var bytesBefore = await File.ReadAllBytesAsync(page.SettingsFile, TestContext.Current.CancellationToken);

		var response = await page.PostAsync(SetupPage.Form(page.ReadSetupToken(), confirmReplace: true));

		Assert.Equal(HttpStatusCode.OK, response.Status);
		Assert.False(response.FieldError("ConfirmReplace").Visible);
		Assert.Contains(
			UnreachableFamily.Select(code => DatabaseSetupErrorCatalog.Describe(code).Message),
			message => response.Text.Contains(message, StringComparison.Ordinal));
		Assert.Equal(bytesBefore, await File.ReadAllBytesAsync(page.SettingsFile, TestContext.Current.CancellationToken));
	}

	// ----------------------------------------------------------------- g) blocked state

	private static void WriteFutureVersionSettings(string directory) =>
		File.WriteAllText(
			Path.Combine(directory, FaranoSettingsNames.SettingsFileName),
			"{\"version\":99,\"protectedConnectionString\":\"opaque\"}");

	[Fact]
	public async Task Get_WithASettingsFileFromANewerVersion_IsBlocked_ShowsNoForm_ButTheFolderAndTheRemedy()
	{
		using var page = SetupPage.Start(WriteFutureVersionSettings);

		var response = await page.GetAsync();

		Assert.Equal(HttpStatusCode.OK, response.Status);
		Assert.False(response.HasForm, "A blocked page must not offer a form.");
		Assert.Contains("فایل تنظیمات با نسخه جدیدتری از فرانو ساخته شده است", response.Text);
		Assert.Contains("برنامه را به نسخه جدیدتر ارتقا دهید", response.Text);
		Assert.Contains(page.SettingsDirectory, response.Text);
		Assert.Contains("class=\"alert error\"", response.Html);
		Assert.Null(response.Tag("Input_Server"));
		Assert.False(File.Exists(page.TokenFile), "No setup token may be issued in the blocked state.");
	}

	[Fact]
	public async Task Post_InTheBlockedState_IsIgnored_AndChangesNothing()
	{
		using var page = SetupPage.Start(WriteFutureVersionSettings);
		var bytesBefore = await File.ReadAllBytesAsync(page.SettingsFile, TestContext.Current.CancellationToken);
		var snapshotBefore = Serialized(page.SettingsDirectory);

		var response = await page.PostWithIssuedTokenAsync(SetupPage.Form("anything"));

		Assert.Equal(HttpStatusCode.OK, response.Status);
		Assert.False(response.HasForm);
		Assert.Contains("فایل تنظیمات با نسخه جدیدتری از فرانو ساخته شده است", response.Text);
		Assert.DoesNotContain("<ol class=\"steps\"", response.Html);
		Assert.Equal(bytesBefore, await File.ReadAllBytesAsync(page.SettingsFile, TestContext.Current.CancellationToken));
		Assert.Equal(snapshotBefore, Serialized(page.SettingsDirectory));
		Assert.DoesNotContain("Database setup", page.Factory.Logs.All);
	}

	// ----------------------------------------------------------------- configuration is already present

	[Fact]
	public void TheSetupFactory_DoesNotChangeTheProcessEnvironment()
	{
		var before = Environment.GetEnvironmentVariable("ConnectionStrings__ConnectionString");

		using var page = SetupPage.Start();
		_ = page.Client.GetAsync("/DatabaseSetup", TestContext.Current.CancellationToken).GetAwaiter().GetResult();

		Assert.Equal(before, Environment.GetEnvironmentVariable("ConnectionStrings__ConnectionString"));
		Assert.Equal(
			string.Empty,
			Convert.ToString(page.Factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("ConnectionString")));
	}
}
