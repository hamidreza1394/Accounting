using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Infrastructure.Security;

/// <summary>
/// Receives Content-Security-Policy violation reports (<c>POST /csp-report</c>).
/// </summary>
/// <remarks>
/// The endpoint is anonymous by nature (browsers send reports without credentials), so it is
/// built to be harmless:
/// <list type="bullet">
/// <item>it answers 404 unless <c>SecurityHeaders:CspReportingEnabled</c> is true;</item>
/// <item>only CSP report media types and at most <see cref="MaxBodyBytes" /> bytes are read;</item>
/// <item>it is rate limited per client address (policy <c>csp-report</c>);</item>
/// <item>only a few fields are logged, reduced to safe forms (directive name, the scheme and
/// host of the blocked resource, the path of the page without query string), truncated and
/// without control characters; nothing is stored and nothing is echoed back.</item>
/// </list>
/// </remarks>
public static class CspReportEndpoint
{
	public const string Path = "/csp-report";
	public const int MaxBodyBytes = 8 * 1024;
	private const int MaxFieldLength = 120;

	private static readonly string[] AcceptedMediaTypes =
		["application/csp-report", "application/json", "application/reports+json"];

	public static IEndpointRouteBuilder MapCspReports(this IEndpointRouteBuilder endpoints)
	{
		endpoints.MapPost(Path, ReceiveAsync)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.CspReportPolicy)
			.DisableAntiforgery();

		return endpoints;
	}

	public static async Task<IResult> ReceiveAsync(
		HttpContext context,
		IOptionsMonitor<SecurityHeadersSettings> settings,
		ILoggerFactory loggerFactory)
	{
		if (!settings.CurrentValue.CspReportingEnabled)
		{
			return Results.NotFound();
		}

		var mediaType = context.Request.ContentType?.Split(';')[0].Trim();

		if (mediaType is null || !AcceptedMediaTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
		{
			return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
		}

		if (context.Request.ContentLength > MaxBodyBytes)
		{
			return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
		}

		var buffer = new byte[MaxBodyBytes + 1];
		var length = 0;

		while (length < buffer.Length)
		{
			var read = await context.Request.Body.ReadAsync(buffer.AsMemory(length), context.RequestAborted);

			if (read == 0)
			{
				break;
			}

			length += read;
		}

		if (length > MaxBodyBytes)
		{
			return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
		}

		IReadOnlyList<CspViolation> violations;

		try
		{
			violations = Parse(buffer.AsSpan(0, length));
		}
		catch (JsonException)
		{
			return Results.BadRequest();
		}

		var logger = loggerFactory.CreateLogger(typeof(CspReportEndpoint).FullName!);

		foreach (var violation in violations.Take(5))
		{
			logger.LogWarning(
				"CSP violation: directive {Directive}, blocked {Blocked}, page {Page}.",
				violation.Directive, violation.Blocked, violation.Page);
		}

		return Results.NoContent();
	}

	public sealed record CspViolation(string Directive, string Blocked, string Page);

	/// <summary>
	/// Reads the legacy <c>{"csp-report": {...}}</c> format and the Reporting API format
	/// (<c>[{"type":"csp-violation","body":{...}}]</c>) and reduces every field to a safe form.
	/// </summary>
	public static IReadOnlyList<CspViolation> Parse(ReadOnlySpan<byte> json)
	{
		using var document = JsonDocument.Parse(json.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
		var result = new List<CspViolation>();

		if (document.RootElement.ValueKind == JsonValueKind.Object &&
			document.RootElement.TryGetProperty("csp-report", out var legacy) &&
			legacy.ValueKind == JsonValueKind.Object)
		{
			result.Add(Reduce(
				Read(legacy, "effective-directive") ?? Read(legacy, "violated-directive"),
				Read(legacy, "blocked-uri"),
				Read(legacy, "document-uri")));
		}
		else if (document.RootElement.ValueKind == JsonValueKind.Array)
		{
			foreach (var report in document.RootElement.EnumerateArray())
			{
				if (report.ValueKind == JsonValueKind.Object &&
					report.TryGetProperty("body", out var body) &&
					body.ValueKind == JsonValueKind.Object)
				{
					result.Add(Reduce(
						Read(body, "effectiveDirective"),
						Read(body, "blockedURL"),
						Read(body, "documentURL")));
				}
			}
		}
		else
		{
			throw new JsonException("Not a CSP report.");
		}

		return result;
	}

	private static string? Read(JsonElement element, string name)
	{
		return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;
	}

	private static CspViolation Reduce(string? directive, string? blocked, string? page)
	{
		return new CspViolation(
			Clean(directive?.Split(' ')[0]),
			Clean(ReduceUrl(blocked, keepPath: false)),
			Clean(ReduceUrl(page, keepPath: true)));
	}

	/// <summary>Keeps keywords (inline, eval, data, ...) and scheme://host[/path]; drops query and fragment.</summary>
	private static string? ReduceUrl(string? value, bool keepPath)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
		{
			// Keywords such as "inline", "eval", "data", "blob".
			return value.Split(':', '?', '#', '/')[0];
		}

		if (uri.Scheme is "data" or "blob" or "about")
		{
			return uri.Scheme;
		}

		return keepPath
			? $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}"
			: $"{uri.Scheme}://{uri.Authority}";
	}

	private static string Clean(string? value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return "-";
		}

		var cleaned = new string(value.Where(character => !char.IsControl(character)).ToArray());
		return cleaned.Length <= MaxFieldLength ? cleaned : cleaned[..MaxFieldLength];
	}
}
