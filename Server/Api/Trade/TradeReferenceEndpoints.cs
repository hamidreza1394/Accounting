using Faranou.Modules.Trade.Application.Authorization;
using Faranou.Modules.Trade.Application.ReferenceData;

namespace Server.Api.Trade;

/// <summary>Read-only reference data for Trade forms; the code-embedded data stays the single source of truth.</summary>
internal static class TradeReferenceEndpoints
{
	// Ordinal order by ISO code: deterministic on every host (no ICU/Persian collation dependency).
	// Clients that need Persian alphabetical order sort by nameFa with their own locale.
	private static readonly CountryRef[] CountriesByCode = [.. TradeReferenceData.Countries.OrderBy(country => country.Code, StringComparer.Ordinal)];

	public static void MapTradeReferenceEndpoints(this RouteGroupBuilder trade)
	{
		trade.MapGet("/reference/countries", () => TypedResults.Ok(CountriesByCode))
			.RequireTradePermission(TradePermissionCodes.FormView("party"), TradeAccessIntent.Read);
	}
}
