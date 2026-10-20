using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Template.Tests;

/// <summary>
/// Regression guard of the enforced Content-Security-Policy of the Razor pages: no page may
/// contain an inline script, a style element, a style attribute, an inline event handler or a
/// javascript: URL. Such code must live in wwwroot/js or wwwroot/css.
/// </summary>
public sealed class RazorInlineCodeTests
{
	private static string RepositoryRoot()
	{
		var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

		while (directory.Parent != null && !File.Exists(Path.Combine(directory.FullName, "Template.sln")))
		{
			directory = directory.Parent;
		}

		return directory.FullName;
	}

	private static readonly (string Name, Regex Pattern)[] Rules =
	[
		("inline <script>", new Regex(@"<script(?![^>]*\bsrc\s*=)[^>]*>", RegexOptions.IgnoreCase)),
		("<style> element", new Regex(@"<style\b", RegexOptions.IgnoreCase)),
		("style attribute", new Regex(@"\sstyle\s*=", RegexOptions.IgnoreCase)),
		("inline event handler", new Regex(@"\son[a-z]+\s*=\s*[""']", RegexOptions.IgnoreCase)),
		("javascript: URL", new Regex(@"javascript:", RegexOptions.IgnoreCase)),
	];

	[Fact]
	public void RazorPages_ContainNoInlineScriptStyleOrHandler()
	{
		var pages = Path.Combine(RepositoryRoot(), "Server", "Pages");
		var findings = new List<string>();

		foreach (var file in Directory.EnumerateFiles(pages, "*.cshtml", SearchOption.AllDirectories))
		{
			// Razor comments are not rendered.
			var markup = Regex.Replace(File.ReadAllText(file), @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);

			foreach (var (name, pattern) in Rules)
			{
				foreach (Match match in pattern.Matches(markup))
				{
					var line = markup.Take(match.Index).Count(character => character == '\n') + 1;
					findings.Add($"{Path.GetRelativePath(pages, file)}:{line}: {name}");
				}
			}
		}

		Assert.True(findings.Count == 0, "Inline code breaks the enforced CSP:\n" + string.Join("\n", findings));
	}

	[Fact]
	public void ApplicationScripts_ContainNoJavascriptUrls()
	{
		var scripts = Path.Combine(RepositoryRoot(), "Server", "wwwroot", "js");

		var offenders = Directory.EnumerateFiles(scripts, "*.js", SearchOption.AllDirectories)
			.Where(file => File.ReadAllText(file).Contains("javascript:", StringComparison.OrdinalIgnoreCase))
			.ToList();

		Assert.Empty(offenders);
	}
}

/// <summary>Parsing and sanitising of CSP violation reports.</summary>
public sealed class CspReportParsingTests
{
	[Fact]
	public void TheLegacyFormat_IsReducedToSafeFields()
	{
		var json = """
			{"csp-report":{"document-uri":"https://faranou.test/Account/Login?ReturnUrl=%2Fsecret&token=abc",
			"violated-directive":"script-src-elem 'self'","effective-directive":"script-src-elem",
			"blocked-uri":"https://evil.example/x.js?session=123","original-policy":"default-src 'self'"}}
			""";

		var violation = Assert.Single(Infrastructure.Security.CspReportEndpoint.Parse(Encoding.UTF8.GetBytes(json)));

		Assert.Equal("script-src-elem", violation.Directive);
		Assert.Equal("https://evil.example", violation.Blocked);
		Assert.Equal("https://faranou.test/Account/Login", violation.Page);
	}

	[Fact]
	public void TheReportingApiFormat_AndKeywords_AreRead()
	{
		var json = """
			[{"type":"csp-violation","body":{"documentURL":"https://faranou.test/","effectiveDirective":"style-src-attr","blockedURL":"inline"}}]
			""";

		var violation = Assert.Single(Infrastructure.Security.CspReportEndpoint.Parse(Encoding.UTF8.GetBytes(json)));

		Assert.Equal("style-src-attr", violation.Directive);
		Assert.Equal("inline", violation.Blocked);
	}

	[Fact]
	public void ControlCharactersAndLongValues_AreRemoved()
	{
		var json = "{\"csp-report\":{\"effective-directive\":\"script-src\\r\\nFAKE LOG LINE" + new string('x', 500) + "\"}}";

		var violation = Assert.Single(Infrastructure.Security.CspReportEndpoint.Parse(Encoding.UTF8.GetBytes(json)));

		Assert.DoesNotContain('\n', violation.Directive);
		Assert.DoesNotContain('\r', violation.Directive);
		Assert.True(violation.Directive.Length <= 120);
	}

	[Theory]
	[InlineData("\"just a string\"")]
	[InlineData("{\"something\":1}")]
	[InlineData("not json")]
	public void SomethingElse_IsRejected(string body)
	{
		Assert.ThrowsAny<System.Text.Json.JsonException>(() =>
			Infrastructure.Security.CspReportEndpoint.Parse(Encoding.UTF8.GetBytes(body)));
	}
}

/// <summary>The report endpoint through the real pipeline, with reporting enabled.</summary>
public sealed class CspReportEndpointTests : IClassFixture<CspReportEndpointTests.Factory>
{
	public sealed class Factory : Integration.FaranouApplicationFactory
	{
		protected override IDictionary<string, string?> Settings
		{
			get
			{
				var settings = base.Settings;
				settings["SecurityHeaders:CspReportingEnabled"] = "true";
				settings["RateLimiting:CspReport:PermitLimit"] = "100000";
				return settings;
			}
		}
	}

	public CspReportEndpointTests(Factory factory)
	{
		ApplicationFactory = factory;
	}

	private Factory ApplicationFactory { get; }

	private static StringContent Report(string json, string mediaType = "application/csp-report") =>
		new(json, Encoding.UTF8, mediaType);

	[Fact]
	public async Task AValidReport_IsAcceptedAnonymously_WithoutABody()
	{
		using var client = ApplicationFactory.CreateBrowser();

		var response = await client.PostAsync("/csp-report",
			Report("{\"csp-report\":{\"effective-directive\":\"script-src\",\"blocked-uri\":\"inline\"}}"));

		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		Assert.Empty(await response.Content.ReadAsByteArrayAsync());
	}

	[Fact]
	public async Task OtherMediaTypes_MalformedAndOversizedBodies_AreRefused()
	{
		using var client = ApplicationFactory.CreateBrowser();

		Assert.Equal(HttpStatusCode.UnsupportedMediaType,
			(await client.PostAsync("/csp-report", Report("{}", "text/plain"))).StatusCode);
		Assert.Equal(HttpStatusCode.BadRequest,
			(await client.PostAsync("/csp-report", Report("not json"))).StatusCode);
		Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
			(await client.PostAsync("/csp-report", Report("{\"csp-report\":{\"x\":\"" + new string('a', 9000) + "\"}}"))).StatusCode);
	}

	[Fact]
	public async Task ThePagesAnnounceTheReportEndpoint()
	{
		using var client = ApplicationFactory.CreateBrowser();

		var response = await client.GetAsync("/Account/Login");

		Assert.EndsWith("; report-uri /csp-report", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
	}
}

/// <summary>The report endpoint is rate limited per client address.</summary>
public sealed class CspReportRateLimitTests : IClassFixture<CspReportRateLimitTests.Factory>
{
	public sealed class Factory : Integration.FaranouApplicationFactory
	{
		protected override IDictionary<string, string?> Settings
		{
			get
			{
				var settings = base.Settings;
				settings["SecurityHeaders:CspReportingEnabled"] = "true";
				settings["RateLimiting:CspReport:PermitLimit"] = "2";
				settings["RateLimiting:CspReport:WindowSeconds"] = "3600";
				return settings;
			}
		}
	}

	public CspReportRateLimitTests(Factory factory)
	{
		ApplicationFactory = factory;
	}

	private Factory ApplicationFactory { get; }

	[Fact]
	public async Task TheThirdReport_IsRejected()
	{
		using var client = ApplicationFactory.CreateBrowser();
		var statuses = new List<HttpStatusCode>();

		for (var attempt = 0; attempt < 3; attempt++)
		{
			statuses.Add((await client.PostAsync("/csp-report",
				new StringContent("{\"csp-report\":{}}", Encoding.UTF8, "application/csp-report"))).StatusCode);
		}

		Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.NoContent, (HttpStatusCode)429 }, statuses);
	}
}

/// <summary>
/// Local resources referenced by the application stylesheets must exist with the exact file-name
/// case: Linux servers are case-sensitive (the fonts of fontiran.css were requested in lower case
/// and answered 404 there, while Windows served them).
/// </summary>
public sealed class StylesheetResourceTests
{
	[Fact]
	public void EveryLocalUrlInTheApplicationStylesheets_ExistsWithTheExactCase()
	{
		var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

		while (directory.Parent != null && !File.Exists(Path.Combine(directory.FullName, "Template.sln")))
		{
			directory = directory.Parent;
		}

		var wwwroot = Path.Combine(directory.FullName, "Server", "wwwroot");
		var missing = new List<string>();
		var urlPattern = new Regex(@"url\(\s*['""]?(/[^'"")?#]+)", RegexOptions.IgnoreCase);

		foreach (var stylesheet in Directory.EnumerateFiles(Path.Combine(wwwroot, "css"), "*.css", SearchOption.AllDirectories))
		{
			foreach (Match match in urlPattern.Matches(File.ReadAllText(stylesheet)))
			{
				var relative = match.Groups[1].Value.TrimStart('/');
				var folder = Path.GetDirectoryName(Path.Combine(wwwroot, relative))!;
				var name = Path.GetFileName(relative);

				var exists = Directory.Exists(folder) &&
					Directory.EnumerateFiles(folder).Any(file => string.Equals(Path.GetFileName(file), name, StringComparison.Ordinal));

				if (!exists)
				{
					missing.Add($"{Path.GetRelativePath(wwwroot, stylesheet)}: {match.Groups[1].Value}");
				}
			}
		}

		Assert.True(missing.Count == 0, "Missing or wrongly cased resources:\n" + string.Join("\n", missing.Distinct()));
	}
}
