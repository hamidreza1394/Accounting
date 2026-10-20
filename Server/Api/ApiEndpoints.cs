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
			.AddEndpointFilter<ApiResponseFilter>();

		api.MapGet("/session", GetSession);
		api.MapPost("/session/logout", LogoutAsync);
		api.MapGet("/antiforgery/token", GetAntiforgeryToken);
		api.MapGet("/companies", GetCompaniesAsync);
		api.MapGet("/companies/{companyId:guid}/overview", GetCompanyOverviewAsync);

		return endpoints;
	}

	public static bool IsApiRequest(PathString path)
	{
		return path.StartsWithSegments(RoutePrefix, StringComparison.OrdinalIgnoreCase);
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
