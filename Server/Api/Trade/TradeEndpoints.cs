using Faranou.Modules.Trade.Application.Authorization;

namespace Server.Api.Trade;

/// <summary>
/// The Trade API group. Every endpoint added here is gated by <see cref="TradeAccessFilter" />
/// (identity, work context, period state, module entitlement, then user permission).
/// </summary>
public static class TradeEndpoints
{
	public static RouteGroupBuilder MapTradeEndpoints(this RouteGroupBuilder api)
	{
		var group = api.MapGroup("/trade")
			.AddEndpointFilter(async (context, next) =>
			{
				// Registered before the gate so that gate rejections are never cached either.
				context.HttpContext.Response.Headers.CacheControl = "no-store";
				context.HttpContext.Response.Headers.Pragma = "no-cache";
				return await next(context);
			})
			.AddEndpointFilter<TradeAccessFilter>()
			.WithMetadata(new TradeGateMarker());

		group.MapGet("/capabilities", GetCapabilities)
			.WithMetadata(new TradeCapabilitiesEndpointMetadata());

		group.MapTradePartyEndpoints();
		group.MapTradeReferenceEndpoints();

		return api;
	}

	private static Microsoft.AspNetCore.Http.HttpResults.Ok<TradeCapabilitiesResponse> GetCapabilities(HttpContext httpContext)
	{
		httpContext.Response.Headers.CacheControl = "no-store";
		var capabilities = (TradeCapabilities)httpContext.Items[TradeAccessFilter.CapabilitiesItemKey]!;

		return TypedResults.Ok(new TradeCapabilitiesResponse(
			Available: capabilities.Available,
			AccessMode: capabilities.AccessMode.ToString(),
			Permissions: capabilities.Available ? capabilities.Permissions : []));
	}
}

public sealed record TradeCapabilitiesResponse(
	bool Available,
	string AccessMode,
	IReadOnlyList<string> Permissions);
