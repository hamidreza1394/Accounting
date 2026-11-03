namespace Faranou.Licensing.Contracts;

public static class LicenseFeatureValueTypes
{
	public const string Enabled = "enabled";
	public const string MaxCount = "max_count";
	public const string Enum = "enum";
}

/// <summary>
/// Stable code allowlist. A code's owning module and value type are immutable; semantic changes
/// require a new code. The enum wire type is reserved until product-approved enum values exist.
/// </summary>
public static class LicenseFeatureCatalog
{
	private static readonly IReadOnlyDictionary<string, LicenseFeatureDefinition> Definitions =
		new Dictionary<string, LicenseFeatureDefinition>(StringComparer.Ordinal)
		{
			["finance.analytic_account_levels"] = new("finance", LicenseFeatureValueTypes.MaxCount),
			["finance.cost_centers"] = new("finance", LicenseFeatureValueTypes.MaxCount),
			["finance.budgeting"] = new("finance", LicenseFeatureValueTypes.Enabled),
			["inventory.warehouses"] = new("inventory", LicenseFeatureValueTypes.MaxCount),
			["sales.pos"] = new("sales", LicenseFeatureValueTypes.Enabled)
		};

	public static bool TryGetDefinition(string? code, out LicenseFeatureDefinition? definition)
	{
		if (code is not null && Definitions.TryGetValue(code, out var found))
		{
			definition = found;
			return true;
		}

		definition = null;
		return false;
	}
}

public sealed record LicenseFeatureDefinition(string RequiredModule, string ValueType);
