using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Server.Api;

/// <summary>
/// The JSON API (version 1) used by the React client in <c>ClientApp</c>.
/// </summary>
/// <remarks>
/// <para>
/// The API is served from the same origin as the Razor Pages and is authenticated with
/// the same HttpOnly authentication cookie, so no token is ever exposed to JavaScript
/// and no CORS policy is needed. Requests that change state require the anti-forgery
/// request token in the <see cref="AntiforgeryHeaderName" /> header.
/// </para>
/// <para>
/// Every company-scoped endpoint resolves the company through
/// <see cref="Infrastructure.Tenancy.CompanyAccessService" />: the company identifier in the
/// route is accepted only for an active membership of the signed-in user. A company the
/// user may not access yields 404, exactly like a company that does not exist.
/// </para>
/// </remarks>
public static class ApiEndpoints
{
	/// <summary>The route prefix of the API; also used to answer 401/403 instead of redirects.</summary>
	public const string RoutePrefix = "/api";

	/// <summary>The header that carries the anti-forgery request token of the client.</summary>
	public const string AntiforgeryHeaderName = "X-XSRF-TOKEN";

	/// <summary>The upper bound of fiscal periods returned for one company.</summary>
	public const int MaxFiscalPeriods = 50;

	public static IEndpointRouteBuilder MapFaranouApi(this IEndpointRouteBuilder endpoints)
	{
		var api = endpoints
			.MapGroup($"{RoutePrefix}/v1")
			.RequireAuthorization()
			.RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiPolicy)
			.AddEndpointFilter<ApiResponseFilter>();

		api.MapGet("/session", GetSession);
		api.MapPost("/session/logout", LogoutAsync)
			.RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
		api.MapGet("/antiforgery/token", GetAntiforgeryToken)
			.RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
		api.MapGet("/companies", GetCompaniesAsync);
		api.MapGet("/companies/{companyId:guid}/overview", GetCompanyOverviewAsync);
		api.MapWorkContextEndpoints();
		api.MapCustomerBoundaryEndpoints();
		api.MapGet("/provisioning/enrollment/antiforgery", GetAntiforgeryToken)
			.AllowAnonymous()
			.RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
		api.MapPost("/provisioning/enrollment/complete", CompleteProvisioningEnrollmentAsync)
			.AllowAnonymous()
			.RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
		api.MapPost("/provisioning/{requestId:guid}/enrollment/reissue", ReissueProvisioningEnrollmentAsync)
			.RequireAuthorization(policy => policy.RequireClaim("AccountKind", "Infrastructure"))
			.RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
		api.MapPost("/provisioning/apply", ApplyProvisioningAsync)
			.RequireAuthorization(policy => policy.RequireClaim("AccountKind", "Infrastructure"))
			.RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
		api.MapPost("/licensing/import", ImportCustomerLicenseAsync)
			.RequireAuthorization(policy => policy.RequireClaim("AccountKind", "Infrastructure"))
			.RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);

		return endpoints;
	}

	public static bool IsApiRequest(PathString path)
	{
		return path.StartsWithSegments(RoutePrefix, StringComparison.OrdinalIgnoreCase);
	}



	/// <summary>Completes the first administrator credential setup using the one-time enrollment token.</summary>
	public static async Task<IResult> CompleteProvisioningEnrollmentAsync(
		HttpContext httpContext,
		[FromBody] CompleteProvisioningEnrollmentRequest request,
		[FromServices] IAntiforgery antiforgery,
		[FromServices] Infrastructure.Provisioning.CustomerProvisioningApplyService applyService,
		CancellationToken cancellationToken)
	{
		if (!await antiforgery.IsRequestValidAsync(httpContext))
			return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);

		httpContext.Response.Headers.CacheControl = "no-store";
		httpContext.Response.Headers.Pragma = "no-cache";
		var result = await applyService.CompleteEnrollmentAsync(
			request.Username,
			request.EnrollmentToken,
			request.NewPassword,
			httpContext.Connection.RemoteIpAddress?.ToString(),
			cancellationToken);
		httpContext.Response.Headers.CacheControl = "no-store";
		httpContext.Response.Headers.Pragma = "no-cache";

		if (result.Success)
			return TypedResults.NoContent();

		var status = result.ErrorCode switch
		{
			"enrollment_expired" => StatusCodes.Status410Gone,
			"enrollment_rate_limited" => StatusCodes.Status429TooManyRequests,
			"enrollment_busy" => StatusCodes.Status409Conflict,
			_ => StatusCodes.Status400BadRequest
		};
		return TypedResults.Problem(
			title: "فعال‌سازی حساب مدیر انجام نشد.",
			detail: result.ErrorCode ?? "enrollment_invalid",
			statusCode: status);
	}

	/// <summary>Issues a replacement enrollment token to an authenticated infrastructure operator.</summary>
	public static async Task<IResult> ReissueProvisioningEnrollmentAsync(
		Guid requestId,
		HttpContext httpContext,
		[FromBody] ReissueProvisioningEnrollmentRequest request,
		[FromServices] IAntiforgery antiforgery,
		[FromServices] Infrastructure.Provisioning.CustomerProvisioningApplyService applyService,
		CancellationToken cancellationToken)
	{
		if (!await antiforgery.IsRequestValidAsync(httpContext))
			return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);

		httpContext.Response.Headers.CacheControl = "no-store";
		httpContext.Response.Headers.Pragma = "no-cache";
		var result = await applyService.ReissueEnrollmentTokenAsync(
			requestId,
			request.ReasonCode ?? string.Empty,
			httpContext.Connection.RemoteIpAddress?.ToString(),
			cancellationToken);
		httpContext.Response.Headers.CacheControl = "no-store";
		httpContext.Response.Headers.Pragma = "no-cache";

		if (!result.Success || result.EnrollmentToken is null)
			return TypedResults.Conflict(new { error = result.ErrorCode ?? "enrollment_reissue_not_allowed" });

		return TypedResults.Ok(new { enrollmentToken = result.EnrollmentToken });
	}

	/// <summary>
	/// POST /api/v1/provisioning/apply — operator-only package application.
	/// The enrollment token is returned only after the database transaction commits.
	/// </summary>
	public static async Task<IResult> ApplyProvisioningAsync(
		HttpContext httpContext,
		[FromBody] Faranou.Licensing.Contracts.CustomerProvisioningPackage package,
		[FromServices] IAntiforgery antiforgery,
		[FromServices] Infrastructure.Provisioning.CustomerProvisioningApplyService applyService,
		CancellationToken cancellationToken)
	{
		if (!await antiforgery.IsRequestValidAsync(httpContext))
			return TypedResults.Problem(
				title: "درخواست نامعتبر است.",
				statusCode: StatusCodes.Status400BadRequest);

		httpContext.Response.Headers.CacheControl = "no-store";
		httpContext.Response.Headers.Pragma = "no-cache";
		var result = await applyService.ApplyAsync(package, cancellationToken);

		if (result.Acknowledgement is not null)
		{
			if (result.Success)
				return TypedResults.Ok(new
				{
					result.Acknowledgement,
					result.Replayed,
					EnrollmentToken = result.EnrollmentToken
				});

			return TypedResults.Conflict(new
			{
				Error = result.ErrorCode ?? "provisioning_rejected",
				result.Acknowledgement,
				result.Replayed
			});
		}

		var status = result.ErrorCode switch
		{
			"provisioning_acknowledgement_signer_unavailable" => StatusCodes.Status503ServiceUnavailable,
			"provisioning_acknowledgement_signer_key_untrusted" => StatusCodes.Status503ServiceUnavailable,
			"provisioning_acknowledgement_signature_invalid" => StatusCodes.Status503ServiceUnavailable,
			"provisioning_request_id_conflict" or "provisioning_nonce_replayed" or
				"provisioning_acknowledgement_missing" or "provisioning_apply_conflict" or
				"provisioning_apply_busy" => StatusCodes.Status409Conflict,
			_ => StatusCodes.Status400BadRequest
		};

		return TypedResults.Problem(
			title: "بستهٔ راه‌اندازی قابل اعمال نیست.",
			detail: result.ErrorCode ?? "provisioning_apply_failed",
			statusCode: status);
	}

	/// <summary>
	/// The exception handler of the API branch. It also covers failures that happen before an
	/// endpoint filter runs (for example while a service of the endpoint is created): the
	/// details go to the log only, the client gets a generic problem response.
	/// </summary>
	public static async Task WriteUnexpectedErrorAsync(HttpContext httpContext)
	{
		var exception = httpContext.Features
			.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;

		// Request-body binding failures are client errors, not unexpected server faults.
		// Preserve the framework-selected status while returning only a generic response;
		// malformed or truncated input must not produce an Error-level application log.
		if (exception is BadHttpRequestException badRequestException)
		{
			httpContext.Response.Headers.CacheControl = "no-store";

			await TypedResults.Problem(
				title: "درخواست نامعتبر است.",
				statusCode: badRequestException.StatusCode)
				.ExecuteAsync(httpContext);
			return;
		}

		httpContext.RequestServices
			.GetRequiredService<ILoggerFactory>()
			.CreateLogger(typeof(ApiEndpoints).FullName!)
			.LogError(exception, "API request {Method} {Path} failed with an unexpected error.",
				httpContext.Request.Method, httpContext.Request.Path.Value);

		httpContext.Response.Headers.CacheControl = "no-store";

		await TypedResults.Problem(
				title: "خطای غیرمنتظره‌ای در سرور رخ داد. لطفاً دوباره تلاش کنید.",
				statusCode: StatusCodes.Status500InternalServerError)
			.ExecuteAsync(httpContext);
	}

	/// <summary>GET /api/v1/session — the verified identity of the current session.</summary>
	public static Ok<SessionResponse> GetSession(ClaimsPrincipal user)
	{
		var username = user.Identity?.Name ?? string.Empty;
		var fullName = user.FindFirst("FullName")?.Value;

		return TypedResults.Ok(new SessionResponse(
			Username: username,
			FullName: string.IsNullOrWhiteSpace(fullName) ? username : fullName,
			AccountKind: user.FindFirst(Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType)?.Value
				?? string.Empty,
			IsAdministrator: user.IsInRole(Constants.Role.Admin)));
	}

	/// <summary>
	/// GET /api/v1/antiforgery/token — issues the anti-forgery cookie and returns the
	/// matching request token. The token is bound to the signed-in user.
	/// </summary>
	public static Ok<AntiforgeryTokenResponse> GetAntiforgeryToken(
		HttpContext httpContext,
		[FromServices] IAntiforgery antiforgery)
	{
		httpContext.Response.Headers.CacheControl = "no-store";
		httpContext.Response.Headers.Pragma = "no-cache";
		var tokens = antiforgery.GetAndStoreTokens(httpContext);

		return TypedResults.Ok(new AntiforgeryTokenResponse(
			HeaderName: tokens.HeaderName ?? AntiforgeryHeaderName,
			RequestToken: tokens.RequestToken ?? string.Empty));
	}

	/// <summary>POST /api/v1/session/logout — ends the session (anti-forgery protected).</summary>
	public static async Task<Results<NoContent, ProblemHttpResult>> LogoutAsync(
		HttpContext httpContext,
		[FromServices] IAntiforgery antiforgery)
	{
		if (!await antiforgery.IsRequestValidAsync(httpContext))
		{
			return TypedResults.Problem(
				title: "درخواست نامعتبر است. صفحه را دوباره بارگذاری کنید.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		await httpContext.SignOutAsync(Infrastructure.Security.Utility.AuthenticationScheme);

		httpContext.RequestServices.GetService<Infrastructure.Tenancy.WorkContextCookie>()?.Delete(httpContext);

		return TypedResults.NoContent();
	}

	/// <summary>GET /api/v1/companies — the companies the user may work in.</summary>
	public static async Task<Results<Ok<IReadOnlyList<CompanySummaryResponse>>, UnauthorizedHttpResult>>
		GetCompaniesAsync(
			ClaimsPrincipal user,
			[FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccessService,
			CancellationToken cancellationToken)
	{
		var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(user);

		if (userId is null)
		{
			// The infrastructure account is not a company user and has no companies. A
			// company user without the identifier claim holds a cookie issued by an older
			// version and has to sign in again.
			return IsCompanyUser(user)
				? TypedResults.Unauthorized()
				: TypedResults.Ok<IReadOnlyList<CompanySummaryResponse>>(Array.Empty<CompanySummaryResponse>());
		}

		var companies = await companyAccessService.GetAccessibleCompaniesAsync(userId.Value, cancellationToken);

		return TypedResults.Ok<IReadOnlyList<CompanySummaryResponse>>(
			companies.Select(ToSummary).ToList());
	}

	/// <summary>
	/// GET /api/v1/companies/{companyId}/overview — real data of one company the user may
	/// access. Nothing here is sample data; an empty list means the company has no data yet.
	/// </summary>
	public static async Task<Results<Ok<CompanyOverviewResponse>, NotFound, UnauthorizedHttpResult>>
		GetCompanyOverviewAsync(
			Guid companyId,
			ClaimsPrincipal user,
			[FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccessService,
			[FromServices] Data.DatabaseContext databaseContext,
			CancellationToken cancellationToken)
	{
		var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(user);

		if (userId is null)
		{
			return IsCompanyUser(user) ? TypedResults.Unauthorized() : TypedResults.NotFound();
		}

		var access = await companyAccessService.FindAccessibleCompanyAsync(
			userId.Value, companyId, cancellationToken);

		if (access is null)
		{
			// Same answer for "does not exist" and "belongs to someone else".
			return TypedResults.NotFound();
		}

		// The tenant predicate uses the company identifier verified above, never the raw
		// route value.
		var fiscalPeriods = await databaseContext.FiscalPeriods
			.AsNoTracking()
			.Where(period => period.TenantId == access.CompanyId)
			.OrderByDescending(period => period.StartDate)
			.ThenBy(period => period.Id)
			.Take(MaxFiscalPeriods)
			.Select(period => new
			{
				period.Id,
				period.Title,
				period.StartDate,
				period.EndDate,
				period.Status,
			})
			.ToListAsync(cancellationToken);

		return TypedResults.Ok(new CompanyOverviewResponse(
			Company: ToSummary(access),
			FiscalPeriods: fiscalPeriods
				.Select(period => new FiscalPeriodResponse(
					Id: period.Id,
					Title: period.Title,
					StartDate: period.StartDate,
					EndDate: period.EndDate,
					Status: period.Status.ToString()))
				.ToList()));
	}

	/// <summary>Imports a customer license for the installation configured by the server, never by the request.</summary>
	public static async Task<IResult> ImportCustomerLicenseAsync(
		HttpContext httpContext,
		[FromBody] CustomerLicenseImportRequest request,
		[FromServices] IAntiforgery antiforgery,
		[FromServices] Infrastructure.Licensing.CustomerLicenseImportService importService,
		[FromServices] Microsoft.Extensions.Options.IOptions<Infrastructure.Provisioning.CustomerProvisioningSettings> settings,
		CancellationToken cancellationToken)
	{
		if (!await antiforgery.IsRequestValidAsync(httpContext))
			return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);

		httpContext.Response.Headers.CacheControl = "no-store";
		httpContext.Response.Headers.Pragma = "no-cache";

		var installationId = settings.Value.InstallationId;
		if (string.IsNullOrWhiteSpace(installationId))
			return TypedResults.Problem(title: "هویت نصب پیکربندی نشده است.", statusCode: StatusCodes.Status503ServiceUnavailable);

		var userIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
		if (!Guid.TryParse(userIdValue, out var userId))
			return TypedResults.Unauthorized();

		var result = await importService.ImportAsync(
			request.EnvelopeJson ?? string.Empty,
			installationId,
			userId,
			cancellationToken);

		if (result.Success)
			return TypedResults.Ok(new
			{
				result.AlreadyInstalled,
				Status = result.Status?.ToString()
			});

		var statusCode = result.ErrorCode is "license_audit_unavailable" or
			"installation_identity_proof_unavailable" or "offline_revocation_unavailable"
			? StatusCodes.Status503ServiceUnavailable
			: result.ErrorCode == "license_import_busy"
				? StatusCodes.Status409Conflict
				: StatusCodes.Status400BadRequest;

		return TypedResults.Problem(
			title: "فعال‌سازی مجوز انجام نشد.",
			detail: result.ErrorCode,
			statusCode: statusCode);
	}

	private static bool IsCompanyUser(ClaimsPrincipal user)
	{
		return string.Equals(
			user.FindFirst(Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType)?.Value,
			Infrastructure.Tenancy.CompanyAccessService.UserAccountKind,
			StringComparison.Ordinal);
	}

	private static CompanySummaryResponse ToSummary(Infrastructure.Tenancy.CompanyAccess access)
	{
		return new CompanySummaryResponse(
			Id: access.CompanyId,
			Name: access.CompanyName,
			NationalId: access.NationalId,
			RoleName: access.RoleName);
	}
}

public sealed record SessionResponse(
	string Username,
	string FullName,
	string AccountKind,
	bool IsAdministrator);

public sealed record CompleteProvisioningEnrollmentRequest(
	string? Username, string? EnrollmentToken, string? NewPassword);

public sealed record ReissueProvisioningEnrollmentRequest(string? ReasonCode);\n\npublic sealed record CustomerLicenseImportRequest(string? EnvelopeJson);

public sealed record AntiforgeryTokenResponse(
	string HeaderName,
	string RequestToken);

public sealed record CompanySummaryResponse(
	Guid Id,
	string Name,
	string? NationalId,
	string RoleName);

/// <summary>
/// A fiscal period. The dates are calendar dates (ISO <c>yyyy-MM-dd</c>, Gregorian) as
/// stored in the database; the client converts them to the Persian calendar for display.
/// </summary>
public sealed record FiscalPeriodResponse(
	Guid Id,
	string Title,
	DateOnly StartDate,
	DateOnly EndDate,
	string Status);

public sealed record CompanyOverviewResponse(
	CompanySummaryResponse Company,
	IReadOnlyList<FiscalPeriodResponse> FiscalPeriods);
