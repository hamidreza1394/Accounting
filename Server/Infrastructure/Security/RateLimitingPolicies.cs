using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Infrastructure.Security;

/// <summary>One fixed-window limit.</summary>
public sealed class FixedWindowLimitSettings
{
	public int PermitLimit { get; set; }

	public int WindowSeconds { get; set; } = 60;
}

/// <summary>Settings of the request rate limits (section <c>RateLimiting</c>).</summary>
public sealed class RateLimitingSettings
{
	public const string SectionName = "RateLimiting";

	/// <summary>Every API request, per signed-in user (or per client address when anonymous).</summary>
	public FixedWindowLimitSettings Api { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60 };

	/// <summary>Session endpoints of the API (anti-forgery token, sign-out), per user/address.</summary>
	public FixedWindowLimitSettings ApiSensitive { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };

	/// <summary>
	/// POSTs of the sign-in form, per client address. Independent of, and in addition to, the
	/// per-username lockout of <see cref="AuthenticationService" />.
	/// </summary>
	public FixedWindowLimitSettings Login { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };

	/// <summary>CSP violation reports (<c>POST /csp-report</c>), per client address.</summary>
	public FixedWindowLimitSettings CspReport { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };
}

/// <summary>
/// The rate-limiting policies. The limits are read from configuration when a partition is
/// first used, so they can be changed per environment without code changes.
/// </summary>
/// <remarks>
/// The client address is <see cref="ConnectionInfo.RemoteIpAddress" />. Behind a reverse proxy
/// it is the proxy's address unless the forwarded-headers middleware is configured for the
/// trusted proxy; until then all users behind one proxy share the anonymous/login partition.
/// </remarks>
public static class RateLimitingPolicies
{
	public const string ApiPolicy = "api";
	public const string ApiSensitivePolicy = "api-sensitive";
	public const string LoginPolicy = "login";
	public const string CspReportPolicy = "csp-report";

	public static IServiceCollection AddFaranouRateLimiting(this IServiceCollection services, IConfiguration configuration)
	{
		services.Configure<RateLimitingSettings>(configuration.GetSection(RateLimitingSettings.SectionName));

		services.AddRateLimiter(options =>
		{
			options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
			options.OnRejected = OnRejectedAsync;

			options.AddPolicy(ApiPolicy, context =>
				CreateLimiter(context, ApiPolicy, settings => settings.Api));

			options.AddPolicy(ApiSensitivePolicy, context =>
				CreateLimiter(context, ApiSensitivePolicy, settings => settings.ApiSensitive));

			options.AddPolicy(CspReportPolicy, context =>
				CreateLimiter(context, CspReportPolicy, settings => settings.CspReport, byUser: false));

			options.AddPolicy(LoginPolicy, context =>
			{
				// Only submissions are limited; showing the sign-in page is not.
				if (!HttpMethods.IsPost(context.Request.Method))
				{
					return RateLimitPartition.GetNoLimiter("login-get");
				}

				return CreateLimiter(context, LoginPolicy, settings => settings.Login, byUser: false);
			});
		});

		return services;
	}

	public static string GetPartitionKey(HttpContext context, bool byUser = true)
	{
		if (byUser && context.User.Identity?.IsAuthenticated == true)
		{
			var userKey = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.Identity.Name;

			if (!string.IsNullOrEmpty(userKey))
			{
				return $"user:{userKey}";
			}
		}

		return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
	}

	private static RateLimitPartition<string> CreateLimiter(
		HttpContext context,
		string policy,
		Func<RateLimitingSettings, FixedWindowLimitSettings> select,
		bool byUser = true)
	{
		var settings = select(context.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitingSettings>>().CurrentValue);
		var permitLimit = Math.Max(1, settings.PermitLimit);
		var window = TimeSpan.FromSeconds(Math.Max(1, settings.WindowSeconds));

		return RateLimitPartition.GetFixedWindowLimiter(
			$"{policy}|{GetPartitionKey(context, byUser)}",
			_ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = permitLimit,
				Window = window,
				QueueLimit = 0,
				AutoReplenishment = true,
			});
	}

	private static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
	{
		var response = context.HttpContext.Response;

		if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
		{
			response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
		}

		response.Headers.CacheControl = "no-store";

		const string message = "تعداد درخواست‌ها بیش از حد مجاز است. لطفاً کمی بعد دوباره تلاش کنید.";

		if (Server.Api.ApiEndpoints.IsApiRequest(context.HttpContext.Request.Path))
		{
			await TypedResults.Problem(title: message, statusCode: StatusCodes.Status429TooManyRequests)
				.ExecuteAsync(context.HttpContext);
			return;
		}

		response.ContentType = "text/plain; charset=utf-8";
		await response.WriteAsync(message, cancellationToken);
	}
}
