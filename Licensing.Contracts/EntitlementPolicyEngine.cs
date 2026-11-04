namespace Faranou.Licensing.Contracts;

/// <summary>
/// Pure, side-effect-free decisions over a validated <see cref="LicensePayload"/>.
/// Callers must already have performed signature, installation-binding and revocation checks
/// (via <c>ICustomerLicenseRuntimeEvaluator</c> / envelope validators). This engine never
/// grants rights when data is missing or ambiguous (fail-closed).
/// </summary>
public static class EntitlementPolicyEngine
{
	public sealed record Decision(bool Allowed, string? DenyCode)
	{
		public static Decision Allow() => new(true, null);
		public static Decision Deny(string code) => new(false, code);
	}

	public sealed record SeatDecision(bool Allowed, int ActiveCount, int MaxUsers, string? DenyCode)
	{
		public static SeatDecision Allow(int active, int max) => new(true, active, max, null);
		public static SeatDecision Deny(int active, int max, string code) => new(false, active, max, code);
	}

	/// <summary>
	/// Resolves the company entitlement entry. Returns null when the payload is not v2,
	/// the company list is absent, or the reference is not present (fail-closed for v2).
	/// For legacy v1 payloads, company-scoped checks are not supported and return null.
	/// </summary>
	public static CompanyLicenseEntitlement? ResolveCompany(
		LicensePayload payload,
		string? companyReference)
	{
		if (payload is null)
			return null;

		if (payload.FormatVersion != LicensePayload.CurrentFormatVersion)
			return null; // v1 has no company list; do not invent rights

		if (string.IsNullOrWhiteSpace(companyReference))
			return null;

		var list = payload.CompanyEntitlements;
		if (list is null || list.Count == 0)
			return null;

		foreach (var item in list)
		{
			if (item is null)
				continue;
			if (string.Equals(item.CompanyReference, companyReference, StringComparison.Ordinal))
				return item;
		}

		return null;
	}

	/// <summary>
	/// Company must appear in the v2 entitlement list. Absence is deny.
	/// </summary>
	public static Decision EvaluateCompany(LicensePayload payload, string? companyReference)
	{
		var company = ResolveCompany(payload, companyReference);
		if (company is null)
			return Decision.Deny("company_not_entitled");
		return Decision.Allow();
	}

	/// <summary>
	/// Module must be listed for the company. Unknown or absent module is deny.
	/// TRADE_IMPORT is accepted only when present in the company modules list (v2).
	/// </summary>
	public static Decision EvaluateModule(
		LicensePayload payload,
		string? companyReference,
		string? moduleCode)
	{
		if (string.IsNullOrWhiteSpace(moduleCode))
			return Decision.Deny("module_code_invalid");

		if (!LicenseSubsystemCodes.IsAllowedCompanyModule(moduleCode))
			return Decision.Deny("module_code_unknown");

		var company = ResolveCompany(payload, companyReference);
		if (company is null)
			return Decision.Deny("company_not_entitled");

		var modules = company.Modules;
		if (modules is null || modules.Count == 0)
			return Decision.Deny("module_not_entitled");

		foreach (var m in modules)
		{
			if (string.Equals(m, moduleCode, StringComparison.Ordinal))
				return Decision.Allow();
		}

		return Decision.Deny("module_not_entitled");
	}

	/// <summary>
	/// Feature must be present and valid for the company. Type mismatch or missing value is deny.
	/// </summary>
	public static Decision EvaluateFeature(
		LicensePayload payload,
		string? companyReference,
		string? featureCode,
		int? requestedCount = null)
	{
		if (string.IsNullOrWhiteSpace(featureCode))
			return Decision.Deny("feature_code_invalid");

		if (!LicenseFeatureCatalog.TryGetDefinition(featureCode, out var definition) || definition is null)
			return Decision.Deny("feature_code_unknown");

		// Feature requires its owning module to be entitled.
		var moduleDecision = EvaluateModule(payload, companyReference, definition.RequiredModule);
		if (!moduleDecision.Allowed)
			return Decision.Deny(moduleDecision.DenyCode ?? "module_not_entitled");

		var company = ResolveCompany(payload, companyReference);
		if (company is null)
			return Decision.Deny("company_not_entitled");

		var features = company.Features;
		if (features is null || features.Count == 0)
			return Decision.Deny("feature_not_entitled");

		LicenseFeatureEntitlement? match = null;
		foreach (var f in features)
		{
			if (f is null)
				continue;
			if (string.Equals(f.Code, featureCode, StringComparison.Ordinal))
			{
				match = f;
				break;
			}
		}

		if (match is null)
			return Decision.Deny("feature_not_entitled");

		if (!string.Equals(match.Type, definition.ValueType, StringComparison.Ordinal))
			return Decision.Deny("feature_type_mismatch");

		return definition.ValueType switch
		{
			LicenseFeatureValueTypes.Enabled =>
				match.Enabled == true
					? Decision.Allow()
					: Decision.Deny("feature_disabled"),

			LicenseFeatureValueTypes.MaxCount =>
				EvaluateMaxCount(match.MaxCount, requestedCount),

			LicenseFeatureValueTypes.Enum =>
				// Enum values are reserved until product-approved list exists.
				Decision.Deny("feature_enum_not_supported"),

			_ => Decision.Deny("feature_type_unknown")
		};
	}

	static Decision EvaluateMaxCount(int? maxCount, int? requestedCount)
	{
		if (maxCount is null || maxCount < 0)
			return Decision.Deny("feature_limit_invalid");

		if (requestedCount is null)
			return Decision.Allow(); // presence check only

		if (requestedCount < 0)
			return Decision.Deny("feature_request_invalid");

		return requestedCount <= maxCount
			? Decision.Allow()
			: Decision.Deny("feature_limit_exceeded");
	}

	/// <summary>
	/// Seat capacity for a company. <paramref name="activeHumanMembershipCount"/> must be the
	/// count of active human memberships already resolved from the database by the caller.
	/// Policy is block-new: when active >= MaxUsers the decision is deny.
	/// MaxUsers <= 0 is treated as deny (no seats granted).
	/// </summary>
	public static SeatDecision EvaluateSeatCapacity(
		LicensePayload payload,
		string? companyReference,
		int activeHumanMembershipCount)
	{
		if (activeHumanMembershipCount < 0)
			return SeatDecision.Deny(0, 0, "seat_count_invalid");

		var company = ResolveCompany(payload, companyReference);
		if (company is null)
			return SeatDecision.Deny(activeHumanMembershipCount, 0, "company_not_entitled");

		var max = company.MaxUsers;
		if (max <= 0)
			return SeatDecision.Deny(activeHumanMembershipCount, max, "seat_limit_zero");

		if (activeHumanMembershipCount >= max)
			return SeatDecision.Deny(activeHumanMembershipCount, max, "seat_limit_reached");

		return SeatDecision.Allow(activeHumanMembershipCount, max);
	}

	/// <summary>
	/// Legacy v1 installation-wide subsystem allowlist. Used only when FormatVersion == 1.
	/// Does not grant company-scoped or Trade rights.
	/// </summary>
	public static Decision EvaluateLegacySubsystem(LicensePayload payload, string? subsystemCode)
	{
		if (payload is null || payload.FormatVersion != LicensePayload.LegacyFormatVersion)
			return Decision.Deny("legacy_payload_required");

		if (string.IsNullOrWhiteSpace(subsystemCode))
			return Decision.Deny("module_code_invalid");

		if (!LicenseSubsystemCodes.IsAllowed(subsystemCode))
			return Decision.Deny("module_code_unknown");

		var list = payload.Limits.Subsystems;
		if (list is null || list.Count == 0)
			return Decision.Deny("module_not_entitled");

		foreach (var s in list)
		{
			if (string.Equals(s, subsystemCode, StringComparison.Ordinal))
				return Decision.Allow();
		}

		return Decision.Deny("module_not_entitled");
	}
}
