using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// The "remember username" preference of the sign-in page (Issue #66 / PR #67): what is stored, with
/// which cookie attributes, when it is restored, removed or ignored. The password is never stored
/// by the application. The pipeline is the real one of Program.cs; the database follows
/// <see cref="IntegrationDatabase" />.
/// </summary>
public sealed class RememberedUsernameTests : IClassFixture<FaranouApplicationFactory>
{
	private const string CookieName = ".Faranou.RememberedUsername";

	public RememberedUsernameTests(FaranouApplicationFactory factory)
	{
		Factory = factory;
	}

	private FaranouApplicationFactory Factory { get; }

	private static string? FindRemembered(HttpResponseMessage response)
	{
		return response.Headers.TryGetValues("Set-Cookie", out var values)
			? values.SingleOrDefault(value => value.StartsWith(CookieName + "=", StringComparison.Ordinal))
			: null;
	}

	private static async Task<string> GetLoginPageAsync(HttpClient client, string? cookie = null)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
		if (cookie is not null)
		{
			request.Headers.Add("Cookie", cookie);
		}

		using var response = await client.SendAsync(request);
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		return await response.Content.ReadAsStringAsync();
	}

	// Attribute order of the rendered input is not part of the contract: find the tag by its id.
	private static string InputTag(string html, string id)
	{
		var match = System.Text.RegularExpressions.Regex.Match(html, $"<input[^>]*\\bid=\"{id}\"[^>]*>");
		Assert.True(match.Success, $"No input with id {id}.");
		return match.Value;
	}

	private static bool HasValue(string tag)
	{
		return System.Text.RegularExpressions.Regex.IsMatch(tag, "\\bvalue=\"[^\"]");
	}

	private HttpClient CreateCookieLessBrowser()
	{
		return Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
		{
			BaseAddress = new Uri("https://localhost"),
			AllowAutoRedirect = false,
			HandleCookies = false,
		});
	}

	[Fact]
	public async Task ASuccessfulSignInWithTheOption_StoresOnlyTheUsername_InAHardenedCookie()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();

		var response = await Browser.PostLoginAsync(client, user.Username, user.Password, rememberMe: true);

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		var cookie = FindRemembered(response);
		Assert.NotNull(cookie);
		Assert.StartsWith($"{CookieName}={Uri.EscapeDataString(user.Username)};", cookie, StringComparison.Ordinal);
		Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);

		// No Set-Cookie header of the response carries the password.
		Assert.DoesNotContain(
			response.Headers.GetValues("Set-Cookie"),
			value => value.Contains(user.Password, StringComparison.Ordinal) ||
				value.Contains(Uri.EscapeDataString(user.Password), StringComparison.Ordinal));
	}

	[Fact]
	public async Task ASignInWithoutTheOption_DoesNotStoreTheUsername()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();

		var response = await Browser.PostLoginAsync(client, user.Username, user.Password, rememberMe: false);

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Null(FindRemembered(response));
	}

	[Fact]
	public async Task AFailedSignInWithTheOption_DoesNotStoreTheUsername()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();

		var response = await Browser.PostLoginAsync(client, user.Username, user.Password + "-wrong", rememberMe: true);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Null(FindRemembered(response));
	}

	[Fact]
	public async Task TheSignInPage_RestoresTheUsernameAndTheOption_ButNeverThePassword()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = CreateCookieLessBrowser();

		var html = await GetLoginPageAsync(client, $"{CookieName}={Uri.EscapeDataString(user.Username)}");

		Assert.Contains($"value=\"{user.Username}\"", InputTag(html, "ViewModel_Username"), StringComparison.Ordinal);
		Assert.Contains("checked=\"checked\"", InputTag(html, "ViewModel_RememberMe"), StringComparison.Ordinal);
		Assert.False(HasValue(InputTag(html, "ViewModel_Password")));
	}

	[Fact]
	public async Task TheSignInPage_WithoutTheCookie_ShowsAnEmptyUsernameAndAnUncheckedOption()
	{
		using var client = CreateCookieLessBrowser();

		var html = await GetLoginPageAsync(client);

		Assert.False(HasValue(InputTag(html, "ViewModel_Username")));
		Assert.DoesNotContain("checked=", InputTag(html, "ViewModel_RememberMe"), StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("this-user-name-is-far-too-long-for-the-field")]
	[InlineData("bad%0D%0Aname")]
	[InlineData("%3Cscript%3Ealert(1)%3C%2Fscript%3E")]
	public async Task ASuspiciousCookieValue_IsNotRestored_AndIsNeverRenderedUnescaped(string cookieValue)
	{
		using var client = CreateCookieLessBrowser();

		var html = await GetLoginPageAsync(client, $"{CookieName}={cookieValue}");

		Assert.DoesNotContain("<script>alert(1)", html, StringComparison.Ordinal);
		Assert.DoesNotContain("this-user-name-is-far-too-long", html, StringComparison.Ordinal);
		Assert.False(HasValue(InputTag(html, "ViewModel_Username")));
	}

	[Fact]
	public async Task TurningTheOptionOff_RemovesTheRememberedUsername_EvenWhenTheSignInFails()
	{
		var user = await Factory.WithDatabaseAsync(database => TestData.AddUserAsync(database));
		using var client = Factory.CreateBrowser();

		// Remember, then sign in again with the option off and a wrong password.
		var first = await Browser.PostLoginAsync(client, user.Username, user.Password, rememberMe: true);
		Assert.NotNull(FindRemembered(first));

		var second = await Browser.PostLoginAsync(client, user.Username, user.Password + "-wrong", rememberMe: false);

		Assert.Equal(HttpStatusCode.OK, second.StatusCode);
		var deletion = FindRemembered(second);
		Assert.NotNull(deletion);
		Assert.Contains("expires=Thu, 01 Jan 1970", deletion, StringComparison.OrdinalIgnoreCase);

		// The page now comes without a remembered username.
		var html = await GetLoginPageAsync(client);
		Assert.False(HasValue(InputTag(html, "ViewModel_Username")));
	}

	[Fact]
	public async Task ARestoredUsername_WithHtmlCharacters_IsRenderedEncoded()
	{
		using var client = CreateCookieLessBrowser();

		// 8 characters (within the limit), so it is restored and the output encoding is what protects the page.
		var html = await GetLoginPageAsync(client, $"{CookieName}=%22%3E%3Cb%3Ex%3C");

		Assert.DoesNotContain("\"><b>x<", html, StringComparison.Ordinal);
		Assert.Contains("&lt;b&gt;x&lt;", InputTag(html, "ViewModel_Username"), StringComparison.Ordinal);
	}
}
