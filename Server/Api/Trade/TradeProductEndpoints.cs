using Faranou.Modules.Trade.Application.Authorization;
using Faranou.Modules.Trade.Application.Products;
using Microsoft.AspNetCore.Mvc;

namespace Server.Api.Trade;

/// <summary>
/// Product master data endpoints (list, detail, create, update). Deliberately no delete, activate or
/// deactivate endpoint (D-083 is not decided). The tenant and user come only from the access context
/// stored by <see cref="TradeAccessFilter" />; nothing tenant-related is read from the request.
/// </summary>
internal static class TradeProductEndpoints
{
	private const string DuplicateCode = "trade_product_code_duplicate";
	private const string ConcurrencyConflict = "trade_concurrency_conflict";

	public static void MapTradeProductEndpoints(this RouteGroupBuilder trade)
	{
		var products = trade.MapGroup("/products");

		products.MapGet("/", ListAsync)
			.RequireTradePermission(TradePermissionCodes.FormView("product"), TradeAccessIntent.Read);
		products.MapGet("/{id:guid}", GetAsync)
			.RequireTradePermission(TradePermissionCodes.FormView("product"), TradeAccessIntent.Read);

		// Product is tenant-level master data without a fiscal period, so writes stay allowed in a closed period
		// (same opt-in flag as Party). Create and update also require view: both answer with the record, and
		// create's duplicate-code 409 would otherwise let a user without view probe which codes exist.
		IReadOnlyList<string> alsoView = [TradePermissionCodes.FormView("product")];
		products.MapPost("/", CreateAsync)
			.RequireTradePermission(TradePermissionCodes.FormCreate("product"), TradeAccessIntent.Write,
				periodIndependent: true, alsoRequireRead: alsoView);
		products.MapPut("/{id:guid}", UpdateAsync)
			.RequireTradePermission(TradePermissionCodes.FormUpdate("product"), TradeAccessIntent.Write,
				periodIndependent: true, alsoRequireRead: alsoView);
	}

	private static TradeAccessContext Context(HttpContext httpContext) =>
		(TradeAccessContext)httpContext.Items[TradeAccessFilter.AccessContextItemKey]!;

	private static async Task<IResult> ListAsync(
		HttpContext httpContext,
		[FromServices] ProductService service,
		string? search = null,
		string? productGroupCode = null,
		bool? isActive = null,
		int page = 1,
		int pageSize = ProductService.DefaultPageSize) =>
		Map(await service.ListAsync(
			Context(httpContext),
			new ProductListQuery(search, productGroupCode, isActive, page, pageSize),
			httpContext.RequestAborted));

	private static async Task<IResult> GetAsync(
		Guid id,
		HttpContext httpContext,
		[FromServices] ProductService service) =>
		Map(await service.GetAsync(Context(httpContext), id, httpContext.RequestAborted));

	private static async Task<IResult> CreateAsync(
		HttpContext httpContext,
		[FromServices] ProductService service)
	{
		var (request, error) = await TradeJsonBody.ReadAsync<CreateProductRequest>(httpContext);
		if (error is not null)
		{
			return error;
		}

		var result = await service.CreateAsync(Context(httpContext), request!, httpContext.RequestAborted);
		return result.Outcome == ProductOutcome.Success
			? TypedResults.Created($"/api/v1/trade/products/{result.Value!.Id}", result.Value)
			: Map(result);
	}

	private static async Task<IResult> UpdateAsync(
		Guid id,
		HttpContext httpContext,
		[FromServices] ProductService service)
	{
		var (request, error) = await TradeJsonBody.ReadAsync<UpdateProductRequest>(httpContext);
		if (error is not null)
		{
			return error;
		}

		return Map(await service.UpdateAsync(Context(httpContext), id, request!, httpContext.RequestAborted));
	}

	private static IResult Map<T>(ProductResult<T> result) => result.Outcome switch
	{
		ProductOutcome.Success => TypedResults.Ok(result.Value),
		ProductOutcome.ValidationFailed => TypedResults.ValidationProblem(result.Errors!),
		ProductOutcome.NotFound => TypedResults.NotFound(),
		ProductOutcome.DuplicateCode => Problem(DuplicateCode, "کد کالا تکراری است."),
		ProductOutcome.ConcurrencyConflict => Problem(ConcurrencyConflict, "کالا در این فاصله تغییر کرده است. صفحه را دوباره بارگذاری کنید."),
		_ => throw new InvalidOperationException("Unknown product outcome."),
	};

	private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Problem(string code, string title) =>
		TypedResults.Problem(title: title, detail: code, statusCode: StatusCodes.Status409Conflict);
}
