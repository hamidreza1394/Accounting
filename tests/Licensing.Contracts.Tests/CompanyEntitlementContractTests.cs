using System.Security.Cryptography;
using System.Text;
using Faranou.Licensing.Contracts;
using Xunit;

namespace Licensing.Contracts.Tests;

public sealed class CompanyEntitlementContractTests
{
	[Fact]
	public void PayloadV2_RoundTripsCompanyScopedTradeAndFeatureEntitlements()
	{
		var payload = SampleV2();
		var json = CanonicalJson.SerializePayload(payload);

		Assert.Contains("\"formatVersion\":2", json, StringComparison.Ordinal);
		Assert.Contains("\"modules\":[\"TRADE_IMPORT\",\"finance\",\"inventory\"]", json, StringComparison.Ordinal);
		Assert.True(CanonicalJson.TryParsePayload(json, out var parsed, out var error), error);
		Assert.NotNull(parsed);
		Assert.Equal(2, parsed!.FormatVersion);
		Assert.Equal("company-0001", parsed.CompanyEntitlements![0]!.CompanyReference);
		Assert.Contains(LicenseSubsystemCodes.TradeImport, parsed.CompanyEntitlements[0]!.Modules!);
		Assert.Equal(25, parsed.CompanyEntitlements[0]!.MaxUsers);
		Assert.Equal(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), parsed.SupportUntilUtc);
		Assert.Equal(new DateTimeOffset(2027, 6, 1, 0, 0, 0, TimeSpan.Zero), parsed.UpdatesUntilUtc);
	}

	[Fact]
	public void PayloadV2_ValidEnvelopeAcceptsCompanyScopedTradeEntitlement()
	{
		using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var payload = SampleV2();
		var envelope = Sign(CanonicalJson.SerializePayload(payload), signingKey);
		var validator = CreateValidator(signingKey);

		var result = validator.Validate(envelope, payload.InstallationId);

		Assert.True(result.Success, result.ErrorCode);
		Assert.Contains(
			LicenseSubsystemCodes.TradeImport,
			result.Payload!.CompanyEntitlements![0]!.Modules!);
	}

	[Fact]
	public void PayloadV2_TamperedExactPayloadBytesFailSignatureVerification()
	{
		using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var json = CanonicalJson.SerializePayload(SampleV2());
		var envelope = Sign(json, signingKey);
		var validator = CreateValidator(signingKey);

		var result = validator.Validate(
			envelope with { PayloadJson = json.Replace("TRADE_IMPORT", "treasury", StringComparison.Ordinal) },
			"installation-0001");

		Assert.False(result.Success);
		Assert.Equal("signature_invalid", result.ErrorCode);
	}

	[Fact]
	public void PayloadV2_NonCanonicalJsonIsRejectedEvenWhenItsExactBytesAreSigned()
	{
		using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var canonical = CanonicalJson.SerializePayload(SampleV2());
		var nonCanonical = canonical.Replace("{", "{ ", StringComparison.Ordinal);
		var envelope = Sign(nonCanonical, signingKey);
		var validator = CreateValidator(signingKey);

		var result = validator.Validate(envelope, "installation-0001");

		Assert.False(result.Success);
		Assert.Equal("payload_not_canonical", result.ErrorCode);
	}

	[Fact]
	public void PayloadV2_UnknownJsonPropertiesAreRejected()
	{
		using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var canonical = CanonicalJson.SerializePayload(SampleV2());
		var withUnknownProperty = canonical.Replace(
			"{\"formatVersion\":2,",
			"{\"formatVersion\":2,\"unexpected\":true,",
			StringComparison.Ordinal);
		var envelope = Sign(withUnknownProperty, signingKey);
		var validator = CreateValidator(signingKey);

		var result = validator.Validate(envelope, "installation-0001");

		Assert.False(result.Success);
		Assert.Equal("payload_json_invalid", result.ErrorCode);
	}

	[Fact]
	public void PayloadV1_CanonicalGoldenVectorRemainsUnchanged()
	{
		var json = CanonicalJson.SerializePayload(SampleV1());

		Assert.Equal(
			"{\"formatVersion\":1,\"licenseId\":\"lic-00000001\",\"installationId\":\"installation-0001\",\"productCode\":\"faranou-accounting\",\"licenseKind\":\"perpetual\",\"issuedAtUtc\":\"2026-01-01T00:00:00.0000000+00:00\",\"notBeforeUtc\":\"2026-01-01T00:00:00.0000000+00:00\",\"graceDays\":30,\"sequence\":1,\"limits\":{\"maxCompanies\":5,\"subsystems\":[\"finance\"]}}",
			json);
	}

	[Fact]
	public void PayloadV1_IgnoresFieldsIntroducedByV2ToPreserveLegacyParsing()
	{
		var legacy = CanonicalJson.SerializePayload(SampleV1());
		var withV2OnlyFields = legacy.Replace(
			"\"graceDays\":30",
			"\"supportUntilUtc\":\"not-a-date\",\"updatesUntilUtc\":\"also-invalid\",\"companyEntitlements\":[],\"graceDays\":30",
			StringComparison.Ordinal);

		Assert.True(CanonicalJson.TryParsePayload(withV2OnlyFields, out var parsed, out var error), error);
		Assert.NotNull(parsed);
		Assert.Null(parsed!.SupportUntilUtc);
		Assert.Null(parsed.UpdatesUntilUtc);
		Assert.Null(parsed.CompanyEntitlements);
		Assert.Empty(StructuralValidation.ValidatePayload(parsed));
	}

	[Fact]
	public void PayloadV1_CannotGrantTradeImportThroughLegacyGlobalSubsystems()
	{
		var payload = SampleV1() with
		{
			Limits = new LicenseLimits(5, new[] { LicenseSubsystemCodes.TradeImport })
		};

		Assert.Contains(
			StructuralValidation.ValidatePayload(payload),
			error => error.Contains("unsupported subsystem code", StringComparison.Ordinal));
	}

	[Fact]
	public void PayloadV2_RejectsUnknownCompanyModule()
	{
		var payload = SampleV2() with
		{
			CompanyEntitlements = new[]
			{
				SampleCompany() with { Modules = new[] { "finance", "future-module" } }
			}
		};

		Assert.Contains(
			StructuralValidation.ValidatePayload(payload),
			error => error.Contains("unsupported company module code", StringComparison.Ordinal));
	}

	[Fact]
	public void PayloadV2_RejectsUnknownFeatureCode()
	{
		var payload = SampleV2() with
		{
			CompanyEntitlements = new[]
			{
				SampleCompany() with
				{
					Features = new[]
					{
						new LicenseFeatureEntitlement("finance.unknown_feature", LicenseFeatureValueTypes.Enabled, Enabled: true)
					}
				}
			}
		};

		Assert.Contains(
			StructuralValidation.ValidatePayload(payload),
			error => error.Contains("unsupported feature code", StringComparison.Ordinal));
	}

	[Fact]
	public void PayloadV2_RejectsFeatureTypeThatDoesNotMatchCatalog()
	{
		var payload = SampleV2() with
		{
			CompanyEntitlements = new[]
			{
				SampleCompany() with
				{
					Features = new[]
					{
						new LicenseFeatureEntitlement(
							"finance.budgeting",
							LicenseFeatureValueTypes.MaxCount,
							MaxCount: 3)
					}
				}
			}
		};

		Assert.Contains(
			StructuralValidation.ValidatePayload(payload),
			error => error.Contains("feature value type does not match catalog", StringComparison.Ordinal));
	}

	[Fact]
	public void PayloadV2_RejectsMalformedTypedFeatureUnion()
	{
		var payload = SampleV2() with
		{
			CompanyEntitlements = new[]
			{
				SampleCompany() with
				{
					Features = new[]
					{
						new LicenseFeatureEntitlement(
							"finance.budgeting",
							LicenseFeatureValueTypes.Enabled,
							Enabled: true,
							MaxCount: 2)
					}
				}
			}
		};

		Assert.Contains(
			StructuralValidation.ValidatePayload(payload),
			error => error.Contains("enabled feature value is invalid", StringComparison.Ordinal));
	}

	[Fact]
	public void PayloadV2_RejectsDuplicateCompanyModuleAndFeatureEntries()
	{
		var duplicateCompany = SampleV2() with
		{
			CompanyEntitlements = new[] { SampleCompany(), SampleCompany() }
		};
		var duplicateModule = SampleV2() with
		{
			CompanyEntitlements = new[]
			{
				SampleCompany() with { Modules = new[] { "finance", "finance" } }
			}
		};
		var duplicateFeature = SampleV2() with
		{
			CompanyEntitlements = new[]
			{
				SampleCompany() with
				{
					Features = new[]
					{
						new LicenseFeatureEntitlement("finance.budgeting", LicenseFeatureValueTypes.Enabled, Enabled: true),
						new LicenseFeatureEntitlement("finance.budgeting", LicenseFeatureValueTypes.Enabled, Enabled: false)
					}
				}
			}
		};

		Assert.Contains(StructuralValidation.ValidatePayload(duplicateCompany), e => e.Contains("duplicate company reference", StringComparison.Ordinal));
		Assert.Contains(StructuralValidation.ValidatePayload(duplicateModule), e => e.Contains("duplicate company module code", StringComparison.Ordinal));
		Assert.Contains(StructuralValidation.ValidatePayload(duplicateFeature), e => e.Contains("duplicate feature code", StringComparison.Ordinal));
	}

	[Fact]
	public void PayloadV2_RejectsCompanyEntitlementsAboveInstallationLimit()
	{
		var payload = SampleV2() with
		{
			Limits = new LicenseLimits(1, Array.Empty<string>()),
			CompanyEntitlements = new[]
			{
				SampleCompany(),
				SampleCompany() with { CompanyReference = "company-0002" }
			}
		};

		Assert.Contains(
			StructuralValidation.ValidatePayload(payload),
			error => error.Contains("company entitlements exceed maxCompanies", StringComparison.Ordinal));
	}

	[Fact]
	public void PayloadV2_RequiresCompanyScopedEntitlementsAndNoGlobalSubsystems()
	{
		var missingCompanies = SampleV2() with { CompanyEntitlements = null };
		var globalSubsystems = SampleV2() with
		{
			Limits = new LicenseLimits(2, new[] { "finance" })
		};

		Assert.Contains(
			StructuralValidation.ValidatePayload(missingCompanies),
			error => error.Contains("companyEntitlements is required", StringComparison.Ordinal));
		Assert.Contains(
			StructuralValidation.ValidatePayload(globalSubsystems),
			error => error.Contains("installation-wide subsystems", StringComparison.Ordinal));
	}

	[Fact]
	public void PayloadV2_RequiresPositiveCompanySeatLimitAndEntitledModuleForFeature()
	{
		var noSeats = SampleV2() with
		{
			CompanyEntitlements = new[] { SampleCompany() with { MaxUsers = 0 } }
		};
		var missingFeatureModule = SampleV2() with
		{
			CompanyEntitlements = new[]
			{
				SampleCompany() with
				{
					Modules = new[] { "sales" },
					Features = new[]
					{
						new LicenseFeatureEntitlement(
							"finance.budgeting",
							LicenseFeatureValueTypes.Enabled,
							Enabled: true)
					}
				}
			}
		};

		Assert.Contains(
			StructuralValidation.ValidatePayload(noSeats),
			error => error.Contains("company maxUsers is out of range", StringComparison.Ordinal));
		Assert.Contains(
			StructuralValidation.ValidatePayload(missingFeatureModule),
			error => error.Contains("feature module is not entitled", StringComparison.Ordinal));
	}

	[Fact]
	public void RevisionRules_AcceptHigherSequenceForSameLicenseAndInstallation()
	{
		var current = SampleV1();
		var candidate = SampleV2() with
		{
			Sequence = 2,
			LicenseId = current.LicenseId,
			InstallationId = current.InstallationId,
			ProductCode = current.ProductCode
		};

		Assert.True(LicenseRevisionRules.TryValidateSuccessor(current, candidate, out var error));
		Assert.Null(error);
	}

	[Theory]
	[InlineData(1, 1)]
	[InlineData(2, 1)]
	public void RevisionRules_RejectReplayAndLowerSequence(int currentSequence, int candidateSequence)
	{
		var current = SampleV1() with { Sequence = currentSequence };
		var candidate = SampleV1() with { Sequence = candidateSequence };

		Assert.False(LicenseRevisionRules.TryValidateSuccessor(current, candidate, out var error));
		Assert.Equal("sequence_not_increasing", error);
	}

	[Fact]
	public void RevisionRules_RejectLicenseOrInstallationReplacement()
	{
		var current = SampleV1();
		var changedLicense = current with { LicenseId = "lic-00000002", Sequence = 2 };
		var changedInstallation = current with { InstallationId = "installation-0002", Sequence = 2 };

		Assert.False(LicenseRevisionRules.TryValidateSuccessor(current, changedLicense, out var licenseError));
		Assert.Equal("license_id_changed", licenseError);
		Assert.False(LicenseRevisionRules.TryValidateSuccessor(current, changedInstallation, out var installationError));
		Assert.Equal("installation_id_changed", installationError);
	}

	[Fact]
	public void RevisionRules_RejectPayloadFormatDowngrade()
	{
		var current = SampleV2();
		var candidate = SampleV1() with
		{
			LicenseId = current.LicenseId,
			InstallationId = current.InstallationId,
			ProductCode = current.ProductCode,
			Sequence = current.Sequence + 1
		};

		Assert.False(LicenseRevisionRules.TryValidateSuccessor(current, candidate, out var error));
		Assert.Equal("payload_format_downgrade", error);
	}

	private static LicenseEnvelopeValidator CreateValidator(ECDsa signingKey)
	{
		var resolver = new StaticTrustedKeyResolver(new[]
		{
			new TrustedPublicKey(
				"company-entitlement-key",
				LicenseAlgorithms.EcdsaP256Sha256,
				signingKey.ExportSubjectPublicKeyInfo())
		});
		return new LicenseEnvelopeValidator(
			resolver,
			new EcdsaP256Sha256Verifier(),
			new LicenseEvaluator(TimeProvider.System));
	}

	private static LicenseEnvelope Sign(string payloadJson, ECDsa signingKey)
	{
		var signature = signingKey.SignData(
			Encoding.UTF8.GetBytes(payloadJson),
			HashAlgorithmName.SHA256,
			DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
		return new LicenseEnvelope(
			LicenseAlgorithms.CurrentFormatVersion,
			"company-entitlement-key",
			LicenseAlgorithms.EcdsaP256Sha256,
			payloadJson,
			Convert.ToBase64String(signature));
	}

	private static LicensePayload SampleV1() => new(
		LicensePayload.LegacyFormatVersion,
		"lic-00000001",
		"installation-0001",
		"faranou-accounting",
		LicensePayload.KindPerpetual,
		new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
		new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
		null,
		30,
		1,
		new LicenseLimits(5, new[] { "finance" }));

	private static LicensePayload SampleV2() => SampleV1() with
	{
		FormatVersion = LicensePayload.CurrentFormatVersion,
		Limits = new LicenseLimits(2, Array.Empty<string>()),
		CompanyEntitlements = new[] { SampleCompany() },
		SupportUntilUtc = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
		UpdatesUntilUtc = new DateTimeOffset(2027, 6, 1, 0, 0, 0, TimeSpan.Zero)
	};

	private static CompanyLicenseEntitlement SampleCompany() => new(
		"company-0001",
		new[] { LicenseSubsystemCodes.TradeImport, LicenseSubsystemCodes.Finance, LicenseSubsystemCodes.Inventory },
		25,
		new LicenseFeatureEntitlement?[]
		{
			new("finance.analytic_account_levels", LicenseFeatureValueTypes.MaxCount, MaxCount: 4),
			new("finance.budgeting", LicenseFeatureValueTypes.Enabled, Enabled: true),
			new("inventory.warehouses", LicenseFeatureValueTypes.MaxCount, MaxCount: 8)
		});
}
