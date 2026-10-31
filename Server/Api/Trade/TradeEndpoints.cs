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
			.AddEndpointFilter<TradeAccessFilter>()
			.WithMetadata(new TradeGateMarker());

		group.MapGet("/capabilities", GetCapabilities)
			.WithMetadata(new TradeCapabilitiesEndpointMetadata());

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
