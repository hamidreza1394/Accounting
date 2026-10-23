using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
		api.MapPost("/context/default-company", SetDefaultCompanyAsync);

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
		[FromServices] Data.DatabaseContext databaseContext,
		CancellationToken cancellationToken)
	{
		var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(user);

		if (userId is null)
		{
			// The infrastructure account has no companies; a company user holding a cookie from
			// before the identifier existed has to sign in again.
			return IsCompanyUser(user)
				? TypedResults.Unauthorized()
				: TypedResults.Ok(ToResponse(null, EmptyOptions(), null, new Dictionary<Guid, IReadOnlyList<string>>()));
		}

		var options = await workContextService.GetOptionsAsync(userId.Value, cancellationToken);
		var sessionId = Infrastructure.Tenancy.WorkContextCookie.GetSessionId(user);

		// A session without a sign-in identifier cannot hold a context (it signs in again to
		// choose); the choices are still listed.
		var current = sessionId is null
			? null
			: await workContextService.GetCurrentAsync(httpContext, userId.Value, sessionId.Value, cancellationToken);

		var defaultCompanyId = await databaseContext.Users
			.AsNoTracking()
			.Where(account => account.Id == userId.Value && account.IsActive)
			.Select(account => account.DefaultCompanyId)
			.SingleOrDefaultAsync(cancellationToken);

		// A stale preference never grants access and is not exposed if the membership was revoked.
		if (defaultCompanyId is { } preferred &&
			!options.Companies.Any(item => item.Company.CompanyId == preferred))
		{
			defaultCompanyId = null;
		}

		var relationshipTypes = await GetCompanyRelationshipTypesAsync(databaseContext, options.Companies.Select(item => item.Company.CompanyId).ToArray(), cancellationToken);
		return TypedResults.Ok(ToResponse(current, options, defaultCompanyId, relationshipTypes));
	}

	/// <summary>Stores a user-specific company preference without changing the current sign-in context.</summary>
	public static async Task<Results<NoContent, NotFound, ProblemHttpResult, UnauthorizedHttpResult>>
		SetDefaultCompanyAsync(
			HttpContext httpContext,
			ClaimsPrincipal user,
			[FromBody] SetDefaultCompanyRequest? request,
			[FromServices] IAntiforgery antiforgery,
			[FromServices] Data.DatabaseContext databaseContext,
			[FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccessService,
			CancellationToken cancellationToken)
	{
		if (!await antiforgery.IsRequestValidAsync(httpContext))
		{
			return TypedResults.Problem(title: "درخواست نامعتبر است. صفحه را دوباره بارگذاری کنید.", statusCode: StatusCodes.Status400BadRequest);
		}

		var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(user);
		if (userId is null)
		{
			return IsCompanyUser(user) ? TypedResults.Unauthorized() : TypedResults.NotFound();
		}

		if (request is null || request.CompanyId == Guid.Empty)
		{
			return TypedResults.Problem(title: "درخواست تنظیم شرکت پیش‌فرض معتبر نیست.", statusCode: StatusCodes.Status400BadRequest);
		}

		var account = await databaseContext.Users.SingleOrDefaultAsync(item => item.Id == userId.Value && item.IsActive, cancellationToken);
		if (account is null)
		{
			return TypedResults.Unauthorized();
		}

		if (request.CompanyId is { } companyId &&
			await companyAccessService.FindAccessibleCompanyAsync(userId.Value, companyId, cancellationToken) is null)
		{
			return TypedResults.NotFound();
		}

		account.DefaultCompanyId = request.CompanyId;
		await databaseContext.SaveChangesAsync(cancellationToken);
		return TypedResults.NoContent();
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

	private static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetCompanyRelationshipTypesAsync(
		Data.DatabaseContext databaseContext,
		Guid[] companyIds,
		CancellationToken cancellationToken)
	{
		if (companyIds.Length == 0)
		{
			return new Dictionary<Guid, IReadOnlyList<string>>();
		}

		var boundaries = await databaseContext.Companies
			.AsNoTracking()
			.Where(company => companyIds.Contains(company.Id))
			.Select(company => new { company.Id, company.CustomerBoundaryId })
			.ToDictionaryAsync(company => company.Id, company => company.CustomerBoundaryId, cancellationToken);
		var today = DateOnly.FromDateTime(DateTime.UtcNow);
		var relationships = await databaseContext.OwnershipRelationships
			.AsNoTracking()
			.Where(relationship => companyIds.Contains(relationship.ParentCompanyId) || companyIds.Contains(relationship.SubsidiaryCompanyId))
			.Where(relationship => relationship.ValidFrom <= today && (relationship.ValidTo == null || relationship.ValidTo >= today))
			.Select(relationship => new
			{
				relationship.ParentCompanyId,
				relationship.SubsidiaryCompanyId,
				relationship.ControlBasis,
				ParentBoundaryId = relationship.ParentCompany.CustomerBoundaryId,
				SubsidiaryBoundaryId = relationship.SubsidiaryCompany.CustomerBoundaryId,
			})
			.ToListAsync(cancellationToken);

		var result = new Dictionary<Guid, IReadOnlyList<string>>();
		foreach (var companyId in companyIds)
		{
			if (!boundaries.TryGetValue(companyId, out var boundaryId) || boundaryId is null)
			{
				result[companyId] = ["unknown"];
				continue;
			}

			var related = relationships.Where(relationship => relationship.ParentCompanyId == companyId || relationship.SubsidiaryCompanyId == companyId).ToList();
			var unresolved = related.Any(relationship => relationship.ParentBoundaryId is null ||
				relationship.SubsidiaryBoundaryId is null || relationship.ParentBoundaryId != relationship.SubsidiaryBoundaryId ||
				relationship.ParentBoundaryId != boundaryId);
			var valid = related.Where(relationship => relationship.ParentBoundaryId == boundaryId &&
				relationship.SubsidiaryBoundaryId == boundaryId).ToList();
			var types = new HashSet<string>(StringComparer.Ordinal);
			foreach (var relationship in valid)
			{
				if (relationship.ControlBasis == Domain.ControlBasis.Control)
				{
					types.Add(relationship.ParentCompanyId == companyId ? "parent" : "subsidiary");
				}
				else if (relationship.ControlBasis == Domain.ControlBasis.SignificantInfluence)
				{
					types.Add(relationship.ParentCompanyId == companyId ? "investor" : "associate");
				}
				else if (relationship.ControlBasis == Domain.ControlBasis.JointControl)
				{
					types.Add("jointVenture");
				}
				else
				{
					types.Add(relationship.ParentCompanyId == companyId ? "investor" : "investment");
				}
			}

			if (unresolved)
			{
				types.Add("unknown");
			}
			else if (types.Count == 0)
			{
				types.Add("independent");
			}
			result[companyId] = types.OrderBy(type => type, StringComparer.Ordinal).ToArray();
		}

		return result;
	}
	private static WorkContextResponse ToResponse(
		Infrastructure.Tenancy.WorkContext? current,
		Infrastructure.Tenancy.WorkContextOptions options,
		Guid? defaultCompanyId,
		IReadOnlyDictionary<Guid, IReadOnlyList<string>> relationshipTypes)
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
			DefaultCompanyId: defaultCompanyId,
			Companies: options.Companies
				.Select(item => new CompanyChoiceResponse(
					Id: item.Company.CompanyId,
					Name: item.Company.CompanyName,
					NationalId: item.Company.NationalId,
					RoleName: item.Company.RoleName,
					RelationshipTypes: relationshipTypes.TryGetValue(item.Company.CompanyId, out var types) ? types : ["unknown"],
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
			IsSelectable: period.IsSelectable,
			AccessMode: period.AccessMode switch
			{
				Infrastructure.Tenancy.WorkAccessMode.ReadWrite => "readWrite",
				Infrastructure.Tenancy.WorkAccessMode.ReadOnly => "readOnly",
				_ => null,
			});
	}
}

public sealed record SelectContextRequest(Guid CompanyId, Guid FiscalPeriodId);

public sealed record SetDefaultCompanyRequest(Guid? CompanyId);

/// <summary>
/// A fiscal period offered for selection; <c>IsSelectable</c> and <c>AccessMode</c>
/// (<c>readWrite</c>, <c>readOnly</c> or <c>null</c>) are decided by the server.
/// </summary>
public sealed record FiscalPeriodChoiceResponse(
	Guid Id,
	string Title,
	DateOnly StartDate,
	DateOnly EndDate,
	string Status,
	bool IsSelectable,
	string? AccessMode);

public sealed record CompanyChoiceResponse(
	Guid Id,
	string Name,
	string? NationalId,
	string RoleName,
	IReadOnlyList<string> RelationshipTypes,
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
	Guid? DefaultCompanyId,
	IReadOnlyList<CompanyChoiceResponse> Companies);
