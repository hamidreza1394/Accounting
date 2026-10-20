namespace Infrastructure.Security;

/// <summary>Settings of <see cref="SecurityHeadersMiddleware" /> (section <c>SecurityHeaders</c>).</summary>
public sealed class SecurityHeadersSettings
{
	public const string SectionName = "SecurityHeaders";

	/// <summary>
	/// When true, the strict policy is enforced for the Razor pages (they no longer contain
	/// inline scripts, styles or event handlers; a test guards that). When false, it is only
	/// sent as <c>Content-Security-Policy-Report-Only</c>.
	/// </summary>
	public bool EnforceRazorContentSecurityPolicy { get; set; }

	/// <summary>
	/// Razor path prefixes that keep the strict policy in report-only mode even when it is
	/// enforced elsewhere (e.g. pages with a third-party editor that injects inline styles).
	/// </summary>
	public string[] RazorReportOnlyPathPrefixes { get; set; } = [];

	/// <summary>
	/// When true, the policies carry <c>report-uri /csp-report</c> and violations are logged by
	/// <see cref="CspReportEndpoint" /> (sanitised, size- and rate-limited, never stored).
	/// </summary>
	public bool CspReportingEnabled { get; set; }
}

/// <summary>
/// Adds the HTTP security headers of the application.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The React client (<c>/app</c>) gets an enforced, strict Content-Security-Policy without
/// <c>unsafe-inline</c> or <c>unsafe-eval</c>.</item>
/// <item>The JSON API (<c>/api</c>) never renders a document and gets <c>default-src 'none'</c>.</item>
/// <item>The Razor pages get the strict policy enforced when
/// <see cref="SecurityHeadersSettings.EnforceRazorContentSecurityPolicy" /> is set (except the
/// configured report-only prefixes); otherwise a safe baseline is enforced and the strict policy
/// is report-only.</item>
/// </list>
/// </remarks>
public sealed class SecurityHeadersMiddleware
{
	public const string ClientPolicy =
		"default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; " +
		"connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

	public const string ApiPolicy = "default-src 'none'; frame-ancestors 'none'";

	public const string RazorBaselinePolicy =
		"object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'";

	public const string RazorStrictPolicy =
		"default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; " +
		"connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'";

	public const string ReportDirective = "; report-uri " + CspReportEndpoint.Path;

	public SecurityHeadersMiddleware(RequestDelegate next)
	{
		Next = next;
	}

	private RequestDelegate Next { get; }

	public Task InvokeAsync(
		HttpContext context,
		Microsoft.Extensions.Options.IOptionsMonitor<SecurityHeadersSettings> settings)
	{
		var current = settings.CurrentValue;

		context.Response.OnStarting(() =>
		{
			ApplyHeaders(context.Request.Path, context.Response.Headers, current);
			return Task.CompletedTask;
		});

		return Next(context);
	}

	public static void ApplyHeaders(PathString path, IHeaderDictionary headers, SecurityHeadersSettings settings)
	{
		headers.XContentTypeOptions = "nosniff";
		headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
		headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
		headers["Cross-Origin-Opener-Policy"] = "same-origin";

		var report = settings.CspReportingEnabled ? ReportDirective : string.Empty;

		if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
		{
			headers.ContentSecurityPolicy = ApiPolicy;
			return;
		}

		if (path.StartsWithSegments("/app", StringComparison.OrdinalIgnoreCase))
		{
			headers.ContentSecurityPolicy = ClientPolicy + report;
			return;
		}

		var reportOnlyHere = settings.RazorReportOnlyPathPrefixes
			.Any(prefix => !string.IsNullOrWhiteSpace(prefix) &&
				path.StartsWithSegments(prefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));

		if (settings.EnforceRazorContentSecurityPolicy && !reportOnlyHere)
		{
			headers.ContentSecurityPolicy = RazorStrictPolicy + report;
			return;
		}

		headers.ContentSecurityPolicy = RazorBaselinePolicy + report;
		headers["Content-Security-Policy-Report-Only"] = RazorStrictPolicy + report;
	}
}
