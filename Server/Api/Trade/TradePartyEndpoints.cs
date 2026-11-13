using Faranou.Modules.Trade.Application.Authorization;
using Faranou.Modules.Trade.Application.Parties;
using Microsoft.AspNetCore.Mvc;

namespace Server.Api.Trade;

/// <summary>
/// Party master data endpoints (list, detail, create, update). Deliberately no delete, activate or
/// deactivate endpoint (D-083 is not decided). The tenant and user come only from the access context
/// stored by <see cref="TradeAccessFilter" />; nothing tenant-related is read from the request.
/// </summary>
internal static class TradePartyEndpoints
{
	private const string DuplicateCode = "trade_party_code_duplicate";
	private const string ConcurrencyConflict = "trade_concurrency_conflict";

	public static void MapTradePartyEndpoints(this RouteGroupBuilder trade)
	{
		var parties = trade.MapGroup("/parties");

		parties.MapGet("/", ListAsync)
			.RequireTradePermission(TradePermissionCodes.FormView("party"), TradeAccessIntent.Read);
		parties.MapGet("/{id:guid}", GetAsync)
			.RequireTradePermission(TradePermissionCodes.FormView("party"), TradeAccessIntent.Read);

		// Party is tenant-level master data (D-063 item 3), so writes stay allowed in a closed fiscal period.
		// Create and update also require view: both answer with the record, and create's duplicate-code 409
		// would otherwise let a user without view probe which codes exist.
		IReadOnlyList<string> alsoView = [TradePermissionCodes.FormView("party")];
		parties.MapPost("/", CreateAsync)
			.RequireTradePermission(TradePermissionCodes.FormCreate("party"), TradeAccessIntent.Write,
				periodIndependent: true, alsoRequireRead: alsoView);
		parties.MapPut("/{id:guid}", UpdateAsync)
			.RequireTradePermission(TradePermissionCodes.FormUpdate("party"), TradeAccessIntent.Write,
				periodIndependent: true, alsoRequireRead: alsoView);
	}

	private static TradeAccessContext Context(HttpContext httpContext) =>
		(TradeAccessContext)httpContext.Items[TradeAccessFilter.AccessContextItemKey]!;

	private static async Task<IResult> ListAsync(
		HttpContext httpContext,
		[FromServices] PartyService service,
		string? search = null,
		string? role = null,
		bool? isActive = null,
		int page = 1,
		int pageSize = PartyService.DefaultPageSize)
	{
		var result = await service.ListAsync(
			Context(httpContext),
			new PartyListQuery(search, role, isActive, page, pageSize),
			httpContext.RequestAborted);
		return Map(result);
	}

	private static async Task<IResult> GetAsync(
		Guid id,
		HttpContext httpContext,
		[FromServices] PartyService service) =>
		Map(await service.GetAsync(Context(httpContext), id, httpContext.RequestAborted));

	private static async Task<IResult> CreateAsync(
		HttpContext httpContext,
		[FromServices] PartyService service)
	{
		var (request, error) = await TradeJsonBody.ReadAsync<CreatePartyRequest>(httpContext);
		if (error is not null)
		{
			return error;
		}

		var result = await service.CreateAsync(Context(httpContext), request!, httpContext.RequestAborted);
		return result.Outcome == PartyOutcome.Success
			? TypedResults.Created($"/api/v1/trade/parties/{result.Value!.Id}", result.Value)
			: Map(result);
	}

	private static async Task<IResult> UpdateAsync(
		Guid id,
		HttpContext httpContext,
		[FromServices] PartyService service)
	{
		var (request, error) = await TradeJsonBody.ReadAsync<UpdatePartyRequest>(httpContext);
		if (error is not null)
		{
			return error;
		}

		return Map(await service.UpdateAsync(Context(httpContext), id, request!, httpContext.RequestAborted));
	}

	private static IResult Map<T>(PartyResult<T> result) => result.Outcome switch
	{
		PartyOutcome.Success => TypedResults.Ok(result.Value),
		PartyOutcome.ValidationFailed => TypedResults.ValidationProblem(result.Errors!),
		PartyOutcome.NotFound => TypedResults.NotFound(),
		PartyOutcome.DuplicateCode => Problem(DuplicateCode, "کد طرف‌حساب تکراری است."),
		PartyOutcome.ConcurrencyConflict => Problem(ConcurrencyConflict, "طرف‌حساب در این فاصله تغییر کرده است. صفحه را دوباره بارگذاری کنید."),
		_ => throw new InvalidOperationException("Unknown party outcome."),
	};

	private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Problem(string code, string title) =>
		TypedResults.Problem(title: title, detail: code, statusCode: StatusCodes.Status409Conflict);
}
