using Faranou.Modules.Trade.Application.Authorization;
using Infrastructure.Tenancy;
using Microsoft.AspNetCore.Antiforgery;

namespace Server.Api.Trade;

/// <summary>
/// Fail-closed gate for every endpoint of the /trade group. An endpoint without explicit Trade
/// metadata is denied. The tenant and period come only from the server-validated work context.
/// </summary>
public sealed partial class TradeAccessFilter(
	TradeAccessGate gate,
	IAntiforgery antiforgery,
	ILogger<TradeAccessFilter> logger) : IEndpointFilter
{
	public const string AccessContextItemKey = "Faranou.Trade.AllowedAccessContext";

	public async ValueTask<object?> InvokeAsync(
		EndpointFilterInvocationContext context,
		EndpointFilterDelegate next)
	{
		var httpContext = context.HttpContext;
		var cancellationToken = httpContext.RequestAborted;
		var endpoint = httpContext.GetEndpoint();
		var operation = endpoint?.Metadata.GetMetadata<TradeOperationMetadata>();
		var isCapabilities = endpoint?.Metadata.GetMetadata<TradeCapabilitiesEndpointMetadata>() is not null;

		if ((operation is null) == !isCapabilities)
		{
			LogMissingMetadata(logger, endpoint?.DisplayName);
			return Problem(StatusCodes.Status403Forbidden, "trade_forbidden");
		}

		// The HTTP verb, not only the declared intent, decides whether the request mutates state.
		// A state-changing verb declared as Read (or the capabilities probe on a non-GET verb) is a
		// wiring error and is denied rather than served without CSRF protection.
		var unsafeVerb = !HttpMethods.IsGet(httpContext.Request.Method) &&
			!HttpMethods.IsHead(httpContext.Request.Method) &&
			!HttpMethods.IsOptions(httpContext.Request.Method) &&
			!HttpMethods.IsTrace(httpContext.Request.Method);
		var declaredWrite = operation?.Intent == TradeAccessIntent.Write;
		if (unsafeVerb && !declaredWrite)
		{
			LogMissingMetadata(logger, endpoint?.DisplayName);
			return Problem(StatusCodes.Status403Forbidden, "trade_forbidden");
		}

		var mutating = declaredWrite;
		if (mutating && !await antiforgery.IsRequestValidAsync(httpContext))
		{
			return TypedResults.Problem(
				title: "درخواست نامعتبر است. صفحه را دوباره بارگذاری کنید.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		var userId = CompanyAccessService.GetUserId(httpContext.User);
		if (userId is null)
		{
			return TypedResults.Unauthorized();
		}

		if (isCapabilities)
		{
			var capabilities = await gate.GetCapabilitiesAsync(userId.Value, cancellationToken);
			LogFault(capabilities.Fault);
			if (capabilities.Outcome is TradeAccessOutcome.ContextRequired or TradeAccessOutcome.Unauthenticated)
			{
				return Map(capabilities.Outcome);
			}

			httpContext.Items[CapabilitiesItemKey] = capabilities;
			return await next(context);
		}

		var decision = await gate.AuthorizeAsync(
			userId.Value, operation!.PermissionCode, operation.Intent, cancellationToken);
		LogFault(decision.Fault);

		if (decision.Outcome != TradeAccessOutcome.Allowed || decision.Context is null)
		{
			return Map(decision.Outcome);
		}

		httpContext.Items[AccessContextItemKey] = decision.Context;
		return await next(context);
	}

	public const string CapabilitiesItemKey = "Faranou.Trade.Capabilities";

	[LoggerMessage(Level = LogLevel.Error, Message = "Trade endpoint {Endpoint} has no (or ambiguous) Trade access metadata; request denied.")]
	private static partial void LogMissingMetadata(ILogger logger, string? endpoint);

	[LoggerMessage(Level = LogLevel.Error, Message = "Trade module entitlement check failed; access denied (fail closed).")]
	private static partial void LogEntitlementFailure(ILogger logger, Exception exception);

	private void LogFault(Exception? fault)
	{
		if (fault is not null)
		{
			LogEntitlementFailure(logger, fault);
		}
	}

	private static IResult Map(TradeAccessOutcome outcome) => outcome switch
	{
		TradeAccessOutcome.Unauthenticated => TypedResults.Unauthorized(),
		TradeAccessOutcome.ContextRequired => Problem(StatusCodes.Status409Conflict, "trade_context_required"),
		TradeAccessOutcome.PeriodReadOnly => Problem(StatusCodes.Status409Conflict, "trade_period_read_only"),
		TradeAccessOutcome.ModuleUnavailable => Problem(StatusCodes.Status403Forbidden, "trade_module_unavailable"),
		_ => Problem(StatusCodes.Status403Forbidden, "trade_forbidden"),
	};

	private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Problem(int status, string code) =>
		TypedResults.Problem(title: "دسترسی به ماژول بازرگانی ممکن نیست.", detail: code, statusCode: status);
}
