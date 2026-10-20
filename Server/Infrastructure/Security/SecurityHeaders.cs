namespace Infrastructure.Security;

/// <summary>Settings of <see cref="SecurityHeadersMiddleware" /> (section <c>SecurityHeaders</c>).</summary>
public sealed class SecurityHeadersSettings
{
	public const string SectionName = "SecurityHeaders";

	/// <summary>
	/// When false (default), the strict policy is sent for the Razor pages as
	/// <c>Content-Security-Policy-Report-Only</c>: the browser reports violations in its console
	/// but nothing is blocked. Set to true once the inline scripts/styles of the legacy pages
	/// (see docs/SECURITY.md) are moved to files.
	/// </summary>
	public bool EnforceRazorContentSecurityPolicy { get; set; }
}

/// <summary>
/// Adds the HTTP security headers of the application.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The React client (<c>/app</c>) has no inline script or style and gets an enforced,
/// strict Content-Security-Policy without <c>unsafe-inline</c> or <c>unsafe-eval</c>.</item>
/// <item>The JSON API (<c>/api</c>) never renders a document and gets <c>default-src 'none'</c>.</item>
/// <item>The Razor pages still contain inline scripts/styles (toasts, admin pages, database
/// setup). They get an enforced baseline that does not break them (objects, base URI, form
/// targets, framing) plus the strict policy in report-only mode.</item>
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

	public SecurityHeadersMiddleware(RequestDelegate next)
	{
		Next = next;
	}

	private RequestDelegate Next { get; }

	public Task InvokeAsync(
		HttpContext context,
		Microsoft.Extensions.Options.IOptionsMonitor<SecurityHeadersSettings> settings)
	{
		var enforceRazorPolicy = settings.CurrentValue.EnforceRazorContentSecurityPolicy;

		context.Response.OnStarting(() =>
		{
			ApplyHeaders(context.Request.Path, context.Response.Headers, enforceRazorPolicy);
			return Task.CompletedTask;
		});

		return Next(context);
	}

	public static void ApplyHeaders(PathString path, IHeaderDictionary headers, bool enforceRazorPolicy)
	{
		headers.XContentTypeOptions = "nosniff";
		headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
		headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
		headers["Cross-Origin-Opener-Policy"] = "same-origin";

		if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
		{
			headers.ContentSecurityPolicy = ApiPolicy;
			return;
		}

		if (path.StartsWithSegments("/app", StringComparison.OrdinalIgnoreCase))
		{
			headers.ContentSecurityPolicy = ClientPolicy;
			return;
		}

		if (enforceRazorPolicy)
		{
			headers.ContentSecurityPolicy = RazorStrictPolicy;
			return;
		}

		headers.ContentSecurityPolicy = RazorBaselinePolicy;
		headers["Content-Security-Policy-Report-Only"] = RazorStrictPolicy;
	}
}
