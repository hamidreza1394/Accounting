using Faranou.Modules.Trade.Application.Authorization;
using Faranou.Modules.Trade.Application.Parties;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Server.Api.Trade;

/// <summary>
/// Party master data endpoints (list, detail, create, update). Deliberately no delete, activate or
/// deactivate endpoint (D-083 is not decided). The tenant and user come only from the access context
/// stored by <see cref="TradeAccessFilter" />; nothing tenant-related is read from the request.
/// </summary>
internal static class TradePartyEndpoints
{
	/// <summary>Largest accepted POST/PUT body. Enforced by <see cref="ReadBodyAsync{T}" /> on every host.</summary>
	public const int MaxBodyBytes = 16 * 1024;
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
		var (request, error) = await ReadBodyAsync<CreatePartyRequest>(httpContext);
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
		var (request, error) = await ReadBodyAsync<UpdatePartyRequest>(httpContext);
		if (error is not null)
		{
			return error;
		}

		return Map(await service.UpdateAsync(Context(httpContext), id, request!, httpContext.RequestAborted));
	}

	/// <summary>
	/// Reads the JSON body with a hard size cap before anything is parsed or any service is called. Done by hand
	/// because endpoint size-limit metadata is only honoured by Kestrel: this works for a declared Content-Length
	/// and for chunked/unknown-length bodies on every host. It runs after the gate (identity, Content-Type check,
	/// anti-forgery, permission). The gate has already insisted on application/json, so anti-forgery never sees a
	/// form body; an unauthorised caller's JSON body is never read by this method.
	/// </summary>
	private static async Task<(T? Value, IResult? Error)> ReadBodyAsync<T>(HttpContext httpContext)
		where T : class
	{
		var request = httpContext.Request;
		if (request.ContentLength > MaxBodyBytes)
		{
			return (null, TooLarge());
		}

		using var buffer = new MemoryStream();
		var chunk = new byte[4096];
		int read;
		while ((read = await request.Body.ReadAsync(chunk, httpContext.RequestAborted)) > 0)
		{
			if (buffer.Length + read > MaxBodyBytes)
			{
				return (null, TooLarge());
			}

			buffer.Write(chunk, 0, read);
		}

		try
		{
			var options = httpContext.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
			var value = JsonSerializer.Deserialize<T>(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), options);
			return value is null ? (null, InvalidBody()) : (value, null);
		}
		catch (JsonException)
		{
			return (null, InvalidBody());
		}
	}

	private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult TooLarge() =>
		TypedResults.Problem(
			title: "حجم درخواست بیش از حد مجاز است.",
			detail: "trade_request_too_large",
			statusCode: StatusCodes.Status413PayloadTooLarge);

	private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult InvalidBody() =>
		TypedResults.Problem(
			title: "بدنهٔ درخواست معتبر نیست.",
			detail: "trade_invalid_request_body",
			statusCode: StatusCodes.Status400BadRequest);

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
