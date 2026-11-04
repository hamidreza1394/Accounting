using Faranou.Licensing.Contracts;

namespace Faranou.Licensing.Contracts.Tests;

public sealed class EntitlementPolicyEngineTests
{
	static LicensePayload V2Payload(
		IReadOnlyList<CompanyLicenseEntitlement?>? companies = null,
		IReadOnlyList<string>? legacySubsystems = null) =>
		new(
			FormatVersion: LicensePayload.CurrentFormatVersion,
			LicenseId: "lic-1",
			InstallationId: "inst-1",
			ProductCode: "faranou",
			LicenseKind: LicensePayload.KindTerm,
			IssuedAtUtc: DateTimeOffset.UtcNow,
			NotBeforeUtc: null,
			ExpiresAtUtc: DateTimeOffset.UtcNow.AddYears(1),
			GraceDays: 0,
			Sequence: 1,
			Limits: new LicenseLimits(MaxCompanies: 5, Subsystems: legacySubsystems ?? Array.Empty<string>()))
		{
			CompanyEntitlements = companies
		};

	static CompanyLicenseEntitlement Company(
		string reference,
		IReadOnlyList<string>? modules = null,
		int maxUsers = 3,
		IReadOnlyList<LicenseFeatureEntitlement?>? features = null) =>
		new(reference, modules, maxUsers, features);

	[Fact]
	public void Company_present_is_allowed()
	{
		var payload = V2Payload(new[] { Company("co-1") });
		var d = EntitlementPolicyEngine.EvaluateCompany(payload, "co-1");
		Assert.True(d.Allowed);
	}

	[Fact]
	public void Company_absent_is_denied()
	{
		var payload = V2Payload(new[] { Company("co-1") });
		var d = EntitlementPolicyEngine.EvaluateCompany(payload, "co-missing");
		Assert.False(d.Allowed);
		Assert.Equal("company_not_entitled", d.DenyCode);
	}

	[Fact]
	public void Company_on_v1_payload_is_denied()
	{
		var payload = new LicensePayload(
			FormatVersion: LicensePayload.LegacyFormatVersion,
			LicenseId: "lic-1",
			InstallationId: "inst-1",
			ProductCode: "faranou",
			LicenseKind: LicensePayload.KindTerm,
			IssuedAtUtc: DateTimeOffset.UtcNow,
			NotBeforeUtc: null,
			ExpiresAtUtc: null,
			GraceDays: 0,
			Sequence: 1,
			Limits: new LicenseLimits(2, new[] { "finance" }));
		var d = EntitlementPolicyEngine.EvaluateCompany(payload, "co-1");
		Assert.False(d.Allowed);
	}

	[Fact]
	public void Module_TRADE_IMPORT_allowed_only_when_listed()
	{
		var payload = V2Payload(new[]
		{
			Company("co-1", modules: new[] { LicenseSubsystemCodes.TradeImport })
		});
		var ok = EntitlementPolicyEngine.EvaluateModule(payload, "co-1", LicenseSubsystemCodes.TradeImport);
		Assert.True(ok.Allowed);

		var denied = EntitlementPolicyEngine.EvaluateModule(payload, "co-1", LicenseSubsystemCodes.Finance);
		Assert.False(denied.Allowed);
		Assert.Equal("module_not_entitled", denied.DenyCode);
	}

	[Fact]
	public void Module_unknown_code_denied()
	{
		var payload = V2Payload(new[] { Company("co-1", modules: new[] { "finance" }) });
		var d = EntitlementPolicyEngine.EvaluateModule(payload, "co-1", "unknown_mod");
		Assert.False(d.Allowed);
		Assert.Equal("module_code_unknown", d.DenyCode);
	}

	[Fact]
	public void Feature_enabled_true_allowed()
	{
		var payload = V2Payload(new[]
		{
			Company("co-1",
				modules: new[] { "finance" },
				features: new LicenseFeatureEntitlement?[]
				{
					new("finance.budgeting", LicenseFeatureValueTypes.Enabled, Enabled: true)
				})
		});
		var d = EntitlementPolicyEngine.EvaluateFeature(payload, "co-1", "finance.budgeting");
		Assert.True(d.Allowed);
	}

	[Fact]
	public void Feature_enabled_false_denied()
	{
		var payload = V2Payload(new[]
		{
			Company("co-1",
				modules: new[] { "finance" },
				features: new LicenseFeatureEntitlement?[]
				{
					new("finance.budgeting", LicenseFeatureValueTypes.Enabled, Enabled: false)
				})
		});
		var d = EntitlementPolicyEngine.EvaluateFeature(payload, "co-1", "finance.budgeting");
		Assert.False(d.Allowed);
		Assert.Equal("feature_disabled", d.DenyCode);
	}

	[Fact]
	public void Feature_max_count_exceeded_denied()
	{
		var payload = V2Payload(new[]
		{
			Company("co-1",
				modules: new[] { "finance" },
				features: new LicenseFeatureEntitlement?[]
				{
					new("finance.analytic_account_levels", LicenseFeatureValueTypes.MaxCount, MaxCount: 2)
				})
		});
		var d = EntitlementPolicyEngine.EvaluateFeature(payload, "co-1", "finance.analytic_account_levels", requestedCount: 3);
		Assert.False(d.Allowed);
		Assert.Equal("feature_limit_exceeded", d.DenyCode);
	}

	[Fact]
	public void Feature_requires_owning_module()
	{
		var payload = V2Payload(new[]
		{
			Company("co-1",
				modules: new[] { "sales" }, // finance missing
				features: new LicenseFeatureEntitlement?[]
				{
					new("finance.budgeting", LicenseFeatureValueTypes.Enabled, Enabled: true)
				})
		});
		var d = EntitlementPolicyEngine.EvaluateFeature(payload, "co-1", "finance.budgeting");
		Assert.False(d.Allowed);
	}

	[Fact]
	public void Seat_under_limit_allowed()
	{
		var payload = V2Payload(new[] { Company("co-1", maxUsers: 5) });
		var d = EntitlementPolicyEngine.EvaluateSeatCapacity(payload, "co-1", activeHumanMembershipCount: 2);
		Assert.True(d.Allowed);
		Assert.Equal(2, d.ActiveCount);
		Assert.Equal(5, d.MaxUsers);
	}

	[Fact]
	public void Seat_at_limit_denied_block_new()
	{
		var payload = V2Payload(new[] { Company("co-1", maxUsers: 3) });
		var d = EntitlementPolicyEngine.EvaluateSeatCapacity(payload, "co-1", activeHumanMembershipCount: 3);
		Assert.False(d.Allowed);
		Assert.Equal("seat_limit_reached", d.DenyCode);
	}

	[Fact]
	public void Seat_zero_max_denied()
	{
		var payload = V2Payload(new[] { Company("co-1", maxUsers: 0) });
		var d = EntitlementPolicyEngine.EvaluateSeatCapacity(payload, "co-1", activeHumanMembershipCount: 0);
		Assert.False(d.Allowed);
		Assert.Equal("seat_limit_zero", d.DenyCode);
	}

	[Fact]
	public void Legacy_subsystem_allowed_on_v1()
	{
		var payload = new LicensePayload(
			FormatVersion: LicensePayload.LegacyFormatVersion,
			LicenseId: "lic-1",
			InstallationId: "inst-1",
			ProductCode: "faranou",
			LicenseKind: LicensePayload.KindTerm,
			IssuedAtUtc: DateTimeOffset.UtcNow,
			NotBeforeUtc: null,
			ExpiresAtUtc: null,
			GraceDays: 0,
			Sequence: 1,
			Limits: new LicenseLimits(2, new[] { "finance", "sales" }));
		var d = EntitlementPolicyEngine.EvaluateLegacySubsystem(payload, "finance");
		Assert.True(d.Allowed);
	}

	[Fact]
	public void Legacy_does_not_grant_TRADE_IMPORT()
	{
		var payload = new LicensePayload(
			FormatVersion: LicensePayload.LegacyFormatVersion,
			LicenseId: "lic-1",
			InstallationId: "inst-1",
			ProductCode: "faranou",
			LicenseKind: LicensePayload.KindTerm,
			IssuedAtUtc: DateTimeOffset.UtcNow,
			NotBeforeUtc: null,
			ExpiresAtUtc: null,
			GraceDays: 0,
			Sequence: 1,
			Limits: new LicenseLimits(2, new[] { "finance", LicenseSubsystemCodes.TradeImport }));
		// IsAllowed rejects TradeImport for legacy list; even if present in payload it is not granted via legacy path.
		var d = EntitlementPolicyEngine.EvaluateLegacySubsystem(payload, LicenseSubsystemCodes.TradeImport);
		Assert.False(d.Allowed);
	}
}
