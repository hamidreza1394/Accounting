using Faranou.Modules.Trade.Application.Authorization;

namespace Server.Api.Trade;

/// <summary>Endpoint metadata: the Trade permission and read/write intent the gate enforces.</summary>
public sealed record TradeOperationMetadata(string PermissionCode, TradeAccessIntent Intent);

/// <summary>Marker on the /trade group: every endpoint below it passes through <see cref="TradeAccessFilter" />.</summary>
public sealed class TradeGateMarker;

/// <summary>Marker reserved for GET /capabilities, which runs only identity, context and entitlement.</summary>
public sealed class TradeCapabilitiesEndpointMetadata;

public static class TradeEndpointConventions
{
	/// <summary>Declares the permission and intent of a Trade endpoint; throws at startup when they disagree.</summary>
	public static TBuilder RequireTradePermission<TBuilder>(
		this TBuilder builder,
		string permissionCode,
		TradeAccessIntent intent)
		where TBuilder : IEndpointConventionBuilder
	{
		ArgumentNullException.ThrowIfNull(builder);
		if (!TradePermissionCodes.IsAccessIntentCompatible(permissionCode, intent))
		{
			throw new InvalidOperationException(
				$"Trade permission '{permissionCode}' is not compatible with intent {intent}.");
		}

		return builder.WithMetadata(new TradeOperationMetadata(permissionCode, intent));
	}
}
