namespace Faranou.Licensing.Contracts;

/// <summary>
/// Stable subsystem identifiers. Payload v1 retains its installation-wide allowlist; payload v2
/// uses company-scoped modules and additionally recognizes the approved Trade code.
/// </summary>
public static class LicenseSubsystemCodes
{
	public const string Finance = "finance";
	public const string Sales = "sales";
	public const string Inventory = "inventory";
	public const string Treasury = "treasury";
	public const string TradeImport = "TRADE_IMPORT";

	public static bool IsAllowed(string? code)
		=> code is Finance or Sales or Inventory or Treasury;

	public static bool IsAllowedCompanyModule(string? code)
		=> IsAllowed(code) || string.Equals(code, TradeImport, StringComparison.Ordinal);
}
