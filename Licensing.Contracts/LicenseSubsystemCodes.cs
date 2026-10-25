namespace Faranou.Licensing.Contracts;

/// <summary>
/// Stable subsystem identifiers supported by license format v1 (D-045).
/// These are product entitlement codes, not operational user permissions.
/// </summary>
public static class LicenseSubsystemCodes
{
	public const string Finance = "finance";
	public const string Sales = "sales";
	public const string Inventory = "inventory";
	public const string Treasury = "treasury";

	public static bool IsAllowed(string? code)
		=> code is Finance or Sales or Inventory or Treasury;
}
