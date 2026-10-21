using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Server.Api;

/// <summary>
/// The endpoints of the working context: which company and fiscal period the signed-in user
/// works in during this sign-in.
/// </summary>
/// <remarks>
/// <para>
/// The server decides everything: which choices exist, whether the user may enter directly, and
/// whether a requested choice is valid. The client only shows the result. A choice is accepted
/// only for an active membership (D-013) and a fiscal period of exactly that company, and it is
/// stored for this sign-in only (D-031), so the user is asked again after every new sign-in.
/// </para>
/// </remarks>
public static class WorkContextEndpoints
{
	public static RouteGroupBuilder MapWorkContextEndpoints(this RouteGroupBuilder api)
	{
		api.MapGet("/context", GetContextAsync);
		api.MapPost("/context", SelectAsync);
		api.MapDelete("/context", ClearAsync);

		return api;
	}

	/// <summary>
	/// GET /api/v1/context — the validated current context (if any), the choices, and the
	/// entry mode decided by the server.
	/// </summary>
	public static async Task<Results<Ok<WorkContextResponse>, UnauthorizedHttpResult>> GetContextAsync(
		HttpContext httpContext,
		ClaimsPrincipal user,
		[FromServices] Infrastructure.Tenancy.WorkContextService workContextService,
		CancellationToken cancellationToken)
	{
		var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(user);

		if (userId is null)
		{
			// The infrastructure account has no companies; a company user holding a cookie from
			// before the identifier existed has to sign in again.
			return IsCompanyUser(user)
				? TypedResults.Unauthorized()
				: TypedResults.Ok(ToResponse(null, EmptyOptions()));
		}

		var options = await workContextService.GetOptionsAsync(userId.Value, cancellationToken);
		var sessionId = Infrastructure.Tenancy.WorkContextCookie.GetSessionId(user);

		// A session without a sign-in identifier cannot hold a context (it signs in again to
		// choose); the choices are still listed.
		var current = sessionId is null
			? null
			: await workContextService.GetCurrentAsync(httpContext, userId.Value, sessionId.Value, cancellationToken);

		return TypedResults.Ok(ToResponse(current, options));
	}

	/// <summary>POST /api/v1/context — chooses a company and one of its fiscal periods.</summary>
	public static async Task<Results<Ok<SelectedContextResponse>, NotFound, Conflict<ProblemDetails>, ProblemHttpResult, UnauthorizedHttpResult>>
		SelectAsync(
			HttpContext httpContext,
			ClaimsPrincipal user,
			[FromBody] SelectContextRequest? request,
			[FromServices] IAntiforgery antiforgery,
			[FromServices] Infrastructure.Tenancy.WorkContextService workContextService,
			CancellationToken cancellationToken)
	{
		if (!await antiforgery.IsRequestValidAsync(httpContext))
		{
			return TypedResults.Problem(
				title: "درخواست نامعتبر است. صفحه را دوباره بارگذاری کنید.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(user);
		var sessionId = Infrastructure.Tenancy.WorkContextCookie.GetSessionId(user);

		if (userId is null || sessionId is null)
		{
			// The infrastructure account has no companies to choose from.
			return IsCompanyUser(user) ? TypedResults.Unauthorized() : TypedResults.NotFound();
		}

		if (request is null || request.CompanyId == Guid.Empty || request.FiscalPeriodId == Guid.Empty)
		{
			return TypedResults.Problem(
				title: "شرکت و دوره مالی باید انتخاب شوند.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		var result = await workContextService.SelectAsync(
			httpContext,
			userId.Value,
			sessionId.Value,
			request.CompanyId,
			request.FiscalPeriodId,
			cancellationToken);

		switch (result.Outcome)
		{
			case Infrastructure.Tenancy.SelectionOutcome.Selected:
				return TypedResults.Ok(new SelectedContextResponse(
					ToCompany(result.Context!.Company),
					ToPeriod(result.Context.FiscalPeriod)));

			case Infrastructure.Tenancy.SelectionOutcome.PeriodNotSelectable:
				return TypedResults.Conflict(new ProblemDetails
				{
					Title = "این دوره مالی برای ورود در دسترس نیست. دوره دیگری را انتخاب کنید.",
					Status = StatusCodes.Status409Conflict,
				});

			default:
				// A company the user may not use, a period of another company and a period that
				// does not exist all receive the same answer.
				return TypedResults.NotFound();
		}
	}

	/// <summary>DELETE /api/v1/context — forgets the choice so the user can choose again.</summary>
	public static async Task<Results<NoContent, ProblemHttpResult>> ClearAsync(
		HttpContext httpContext,
		[FromServices] IAntiforgery antiforgery,
		[FromServices] Infrastructure.Tenancy.WorkContextService workContextService)
	{
		if (!await antiforgery.IsRequestValidAsync(httpContext))
		{
			return TypedResults.Problem(
				title: "درخواست نامعتبر است. صفحه را دوباره بارگذاری کنید.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		workContextService.Clear(httpContext);

		return TypedResults.NoContent();
	}

	private static bool IsCompanyUser(ClaimsPrincipal user)
	{
		return string.Equals(
			user.FindFirst(Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType)?.Value,
			Infrastructure.Tenancy.CompanyAccessService.UserAccountKind,
			StringComparison.Ordinal);
	}

	private static Infrastructure.Tenancy.WorkContextOptions EmptyOptions()
	{
		return new Infrastructure.Tenancy.WorkContextOptions(
			Array.Empty<Infrastructure.Tenancy.CompanyWithPeriods>(),
			Infrastructure.Tenancy.EntryMode.NoCompany,
			null,
			null);
	}

	private static WorkContextResponse ToResponse(
		Infrastructure.Tenancy.WorkContext? current,
		Infrastructure.Tenancy.WorkContextOptions options)
	{
		return new WorkContextResponse(
			Current: current is null
				? null
				: new SelectedContextResponse(ToCompany(current.Company), ToPeriod(current.FiscalPeriod)),
			Entry: options.Entry switch
			{
				Infrastructure.Tenancy.EntryMode.DirectEntry => "directEntry",
				Infrastructure.Tenancy.EntryMode.SelectionRequired => "selectionRequired",
				Infrastructure.Tenancy.EntryMode.NoEligiblePeriod => "noEligiblePeriod",
				_ => "noCompany",
			},
			DirectSelection: options.DirectCompanyId is { } companyId && options.DirectFiscalPeriodId is { } periodId
				? new SelectContextRequest(companyId, periodId)
				: null,
			Companies: options.Companies
				.Select(item => new CompanyChoiceResponse(
					Id: item.Company.CompanyId,
					Name: item.Company.CompanyName,
					NationalId: item.Company.NationalId,
					RoleName: item.Company.RoleName,
					FiscalPeriods: item.FiscalPeriods.Select(ToPeriod).ToList()))
				.ToList());
	}

	private static CompanySummaryResponse ToCompany(Infrastructure.Tenancy.CompanyAccess access)
	{
		return new CompanySummaryResponse(access.CompanyId, access.CompanyName, access.NationalId, access.RoleName);
	}

	private static FiscalPeriodChoiceResponse ToPeriod(Infrastructure.Tenancy.FiscalPeriodInfo period)
	{
		return new FiscalPeriodChoiceResponse(
			Id: period.Id,
			Title: period.Title,
			StartDate: period.StartDate,
			EndDate: period.EndDate,
			Status: period.Status.ToString(),
			IsSelectable: period.IsSelectable);
	}
}

public sealed record SelectContextRequest(Guid CompanyId, Guid FiscalPeriodId);

/// <summary>A fiscal period offered for selection; <c>IsSelectable</c> is decided by the server.</summary>
public sealed record FiscalPeriodChoiceResponse(
	Guid Id,
	string Title,
	DateOnly StartDate,
	DateOnly EndDate,
	string Status,
	bool IsSelectable);

public sealed record CompanyChoiceResponse(
	Guid Id,
	string Name,
	string? NationalId,
	string RoleName,
	IReadOnlyList<FiscalPeriodChoiceResponse> FiscalPeriods);

public sealed record SelectedContextResponse(
	CompanySummaryResponse Company,
	FiscalPeriodChoiceResponse FiscalPeriod);

/// <param name="Current">The validated current choice, or <c>null</c> when nothing is selected.</param>
/// <param name="Entry"><c>directEntry</c>, <c>selectionRequired</c>, <c>noCompany</c> or <c>noEligiblePeriod</c>.</param>
/// <param name="DirectSelection">Set only for <c>directEntry</c>: the single valid choice to confirm.</param>
/// <param name="Companies">The companies the user may work in, with their fiscal periods.</param>
public sealed record WorkContextResponse(
	SelectedContextResponse? Current,
	string Entry,
	SelectContextRequest? DirectSelection,
	IReadOnlyList<CompanyChoiceResponse> Companies);
