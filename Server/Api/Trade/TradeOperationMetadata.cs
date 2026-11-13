using Faranou.Modules.Trade.Application.Authorization;

namespace Server.Api.Trade;

/// <summary>Endpoint metadata: the Trade permission and read/write intent the gate enforces.</summary>
public sealed record TradeOperationMetadata(string PermissionCode, TradeAccessIntent Intent)
{
	/// <summary>True only for tenant-level master data writes that stay allowed in a closed period (D-063 item 3).</summary>
	public bool PeriodIndependent { get; init; }

	/// <summary>Further Read permissions the caller must also hold (for example view for update).</summary>
	public IReadOnlyList<string> AdditionalReadPermissions { get; init; } = [];
}

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
		TradeAccessIntent intent,
		bool periodIndependent = false,
		IReadOnlyList<string>? alsoRequireRead = null)
		where TBuilder : IEndpointConventionBuilder
	{
		ArgumentNullException.ThrowIfNull(builder);
		if (!TradePermissionCodes.IsAccessIntentCompatible(permissionCode, intent))
		{
			throw new InvalidOperationException(
				$"Trade permission '{permissionCode}' is not compatible with intent {intent}.");
		}

		if (periodIndependent && intent != TradeAccessIntent.Write)
		{
			throw new InvalidOperationException("Only a Write operation can be period-independent.");
		}

		foreach (var additional in alsoRequireRead ?? [])
		{
			if (!TradePermissionCodes.IsAccessIntentCompatible(additional, TradeAccessIntent.Read))
			{
				throw new InvalidOperationException($"Additional permission '{additional}' is not a Read permission.");
			}
		}

		return builder.WithMetadata(new TradeOperationMetadata(permissionCode, intent)
		{
			PeriodIndependent = periodIndependent,
			AdditionalReadPermissions = alsoRequireRead ?? [],
		});
	}
}
