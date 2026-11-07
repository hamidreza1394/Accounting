using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Signing;
using Faranou.Vendor.Persistence.Domain;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorLicenseAmendmentPayloadBuilderTests
{
    private static readonly DateTimeOffset IssuedAt =
        new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<string> LegacySubsystems =
        Array.AsReadOnly(new[] { "finance", "sales" });

    private static readonly IReadOnlyList<string> CompanyModules =
        Array.AsReadOnly(new[] { "finance", "inventory" });

    private static readonly IReadOnlyList<LicenseFeatureEntitlement?> CompanyFeatures =
        Array.AsReadOnly<LicenseFeatureEntitlement?>(new[]
        {
            new LicenseFeatureEntitlement(
                "finance.cost_centers", LicenseFeatureValueTypes.MaxCount, MaxCount: 12)
        });

    private static readonly IReadOnlyList<CompanyLicenseEntitlement?> CompanyEntitlements =
        Array.AsReadOnly<CompanyLicenseEntitlement?>(new[]
        {
            new CompanyLicenseEntitlement("company-0001", CompanyModules, 25, CompanyFeatures)
        });

    [Fact]
    public void BuildsV1CompanyCapacityIncreaseWithoutChangingExistingRights()
    {
        var fixture = CreateV1Fixture();
        var issuedAt = IssuedAt.AddHours(2);

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            newMaxCompanies: 12, issuedAt, out var amendment, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(amendment);
        Assert.Equal(1, amendment!.FormatVersion);
        Assert.Equal(fixture.Payload.LicenseId, amendment.LicenseId);
        Assert.Equal(fixture.Payload.InstallationId, amendment.InstallationId);
        Assert.Equal(fixture.Payload.ProductCode, amendment.ProductCode);
        Assert.Equal(fixture.Payload.LicenseKind, amendment.LicenseKind);
        Assert.Equal(fixture.Payload.NotBeforeUtc, amendment.NotBeforeUtc);
        Assert.Equal(fixture.Payload.ExpiresAtUtc, amendment.ExpiresAtUtc);
        Assert.Equal(fixture.Payload.GraceDays, amendment.GraceDays);
        Assert.Equal(fixture.Payload.Sequence + 1, amendment.Sequence);
        Assert.Equal(12, amendment.Limits.MaxCompanies);
        Assert.Equal(fixture.Payload.Limits.Subsystems, amendment.Limits.Subsystems);
        Assert.Equal(issuedAt, amendment.IssuedAtUtc);
        Assert.True(LicenseRevisionRules.TryValidateSuccessor(fixture.Payload, amendment, out var successorError), successorError);
    }

    [Fact]
    public void BuildsV2CompanyCapacityIncreaseAndPreservesAllCompanyEntitlements()
    {
        var fixture = CreateV2Fixture();
        var issuedAt = IssuedAt.AddDays(1);

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            newMaxCompanies: 20, issuedAt, out var amendment, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(amendment);
        Assert.Equal(2, amendment!.FormatVersion);
        Assert.Equal(20, amendment.Limits.MaxCompanies);
        Assert.Empty(amendment.Limits.Subsystems);
        var originalCompany = fixture.Payload.CompanyEntitlements![0]!;
        var amendedCompany = amendment.CompanyEntitlements![0]!;
        Assert.NotSame(fixture.Payload.CompanyEntitlements, amendment.CompanyEntitlements);
        Assert.Equal(originalCompany.CompanyReference, amendedCompany.CompanyReference);
        Assert.Equal(originalCompany.MaxUsers, amendedCompany.MaxUsers);
        Assert.Equal(originalCompany.Modules, amendedCompany.Modules);
        Assert.Equal(originalCompany.Features!.Count, amendedCompany.Features!.Count);
        Assert.Equal(originalCompany.Features[0]!.Code, amendedCompany.Features[0]!.Code);
        Assert.Equal(originalCompany.Features[0]!.Type, amendedCompany.Features[0]!.Type);
        Assert.Equal(originalCompany.Features[0]!.MaxCount, amendedCompany.Features[0]!.MaxCount);
        Assert.NotSame(originalCompany.Modules, amendedCompany.Modules);
        Assert.NotSame(originalCompany.Features, amendedCompany.Features);
        Assert.NotSame(originalCompany.Features[0], amendedCompany.Features[0]);
        Assert.Equal(fixture.Payload.SupportUntilUtc, amendment.SupportUntilUtc);
        Assert.Equal(fixture.Payload.UpdatesUntilUtc, amendment.UpdatesUntilUtc);
        Assert.Equal(CanonicalJson.SerializePayload(amendment), CanonicalJson.SerializePayload(
            fixture.Payload with
            {
                IssuedAtUtc = issuedAt,
                Sequence = fixture.Payload.Sequence + 1,
                Limits = fixture.Payload.Limits with { MaxCompanies = 20 }
            }));
    }

    [Fact]
    public void BuiltV1AmendmentIsDetachedFromMutableSubsystemSource()
    {
        var fixture = CreateV1Fixture();
        var mutableSubsystems = new List<string> { "finance", "sales" };
        fixture.Payload = fixture.Payload with
        {
            Limits = new LicenseLimits(5, mutableSubsystems)
        };

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            newMaxCompanies: 8, IssuedAt.AddHours(1), out var amendment, out var error);

        Assert.True(success, error);
        Assert.NotNull(amendment);
        mutableSubsystems.Add("treasury");

        Assert.Equal(new[] { "finance", "sales" }, amendment!.Limits.Subsystems);
        Assert.NotSame(mutableSubsystems, amendment.Limits.Subsystems);
    }

    [Fact]
    public void BuiltV2AmendmentIsDetachedFromMutableCompanyEntitlementSources()
    {
        var fixture = CreateV2Fixture();
        var modules = new List<string> { "finance", "inventory" };
        var features = new List<LicenseFeatureEntitlement?>
        {
            new("finance.cost_centers", LicenseFeatureValueTypes.MaxCount, MaxCount: 12)
        };
        var companies = new List<CompanyLicenseEntitlement?>
        {
            new("company-0001", modules, 25, features)
        };
        fixture.Payload = fixture.Payload with { CompanyEntitlements = companies };

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            newMaxCompanies: 20, IssuedAt.AddDays(1), out var amendment, out var error);

        Assert.True(success, error);
        Assert.NotNull(amendment);
        modules.Add("TRADE_IMPORT");
        features.Add(new LicenseFeatureEntitlement("sales.pos", LicenseFeatureValueTypes.Enabled, Enabled: true));
        companies.Add(new CompanyLicenseEntitlement("company-0002", new[] { "finance" }, 5, Array.Empty<LicenseFeatureEntitlement?>()));

        Assert.Single(amendment!.CompanyEntitlements!);
        Assert.Equal(new[] { "finance", "inventory" }, amendment.CompanyEntitlements![0]!.Modules);
        Assert.Single(amendment.CompanyEntitlements[0]!.Features!);
        Assert.NotSame(companies, amendment.CompanyEntitlements);
        Assert.NotSame(modules, amendment.CompanyEntitlements[0]!.Modules);
        Assert.NotSame(features, amendment.CompanyEntitlements[0]!.Features);
    }

    [Theory]
    [InlineData(5, "company_limit_not_increased")]
    [InlineData(4, "company_limit_not_increased")]
    [InlineData(0, "company_limit_not_increased")]
    public void RejectsSameOrLowerCompanyCapacity(int requestedMaxCompanies, string expectedError)
    {
        var fixture = CreateV1Fixture();

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            requestedMaxCompanies, IssuedAt.AddHours(1), out var amendment, out var error);

        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public void RejectsMissingCurrentCompanyLimit()
    {
        var fixture = CreateV1Fixture();
        var payload = fixture.Payload with { Limits = new LicenseLimits(null, fixture.Payload.Limits.Subsystems) };
        fixture.License.MaxCompanies = null;

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            payload, fixture.License, fixture.Installation, fixture.Customer,
            10, IssuedAt.AddHours(1), out var amendment, out var error);

        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("current_company_limit_missing", error);
    }

    [Fact]
    public void RejectsStaleSequenceOrMetadataInsteadOfBuildingFromAnOldEnvelope()
    {
        var fixture = CreateV1Fixture();
        fixture.License.LastIssuedSequence++;

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            10, IssuedAt.AddHours(1), out var amendment, out var error);

        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("license_sequence_stale", error);

        fixture.License.LastIssuedSequence = fixture.Payload.Sequence;
        fixture.License.MaxCompanies = 99;
        success = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            100, IssuedAt.AddHours(1), out amendment, out error);

        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("license_metadata_stale", error);
    }

    [Fact]
    public void RejectsCrossCustomerInactiveOrNonIssuedResources()
    {
        var fixture = CreateV1Fixture();
        fixture.Installation.CustomerId = Guid.NewGuid();

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            10, IssuedAt.AddHours(1), out var amendment, out var error);

        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("license_installation_mismatch", error);

        fixture = CreateV1Fixture();
        fixture.Customer.Status = "Suspended";
        success = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            10, IssuedAt.AddHours(1), out amendment, out error);

        Assert.False(success);
        Assert.Equal("license_not_amendable", error);

        fixture = CreateV1Fixture();
        fixture.License.Status = "Draft";
        success = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            10, IssuedAt.AddHours(1), out amendment, out error);

        Assert.False(success);
        Assert.Equal("license_not_amendable", error);
    }

    [Fact]
    public void RejectsTermLicenseBeforeStartOrAfterExpiryWithoutUsingGrace()
    {
        var fixture = CreateV1Fixture();
        var startsAt = IssuedAt.AddHours(2);
        fixture.License.ValidFromUtc = startsAt;
        fixture.Payload = fixture.Payload with { NotBeforeUtc = startsAt };

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            10, IssuedAt.AddHours(1), out var amendment, out var error);

        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("license_not_yet_valid", error);

        fixture = CreateV1Fixture();
        var expiresAt = IssuedAt.AddMinutes(30);
        fixture.License.ValidUntilUtc = expiresAt;
        fixture.Payload = fixture.Payload with { ExpiresAtUtc = expiresAt };
        success = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            10, IssuedAt.AddHours(1), out amendment, out error);

        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("license_outside_validity", error);
    }

    [Fact]
    public void ExpiredSupportAndUpdateDatesDoNotBlockPerpetualLicenseAmendment()
    {
        var fixture = CreateV2Fixture();
        fixture.License.LicenseKind = LicensePayload.KindPerpetual;
        fixture.License.ValidUntilUtc = null;
        fixture.Payload = fixture.Payload with
        {
            LicenseKind = LicensePayload.KindPerpetual,
            ExpiresAtUtc = null,
            SupportUntilUtc = IssuedAt.AddDays(-30),
            UpdatesUntilUtc = IssuedAt.AddDays(-10)
        };

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            10, IssuedAt.AddDays(1), out var amendment, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(amendment);
        Assert.Equal(fixture.Payload.SupportUntilUtc, amendment!.SupportUntilUtc);
        Assert.Equal(fixture.Payload.UpdatesUntilUtc, amendment.UpdatesUntilUtc);
    }

    [Fact]
    public void RejectsInvalidPayloadSequenceExhaustionAndClockRollback()
    {
        var fixture = CreateV1Fixture();
        var invalid = fixture.Payload with { Sequence = 0 };

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            invalid, fixture.License, fixture.Installation, fixture.Customer,
            10, IssuedAt.AddHours(1), out var amendment, out var error);

        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("current_license_payload_invalid", error);

        fixture = CreateV1Fixture();
        fixture.Payload = fixture.Payload with { Sequence = int.MaxValue };
        fixture.License.LastIssuedSequence = int.MaxValue;
        success = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            10, IssuedAt.AddHours(1), out amendment, out error);

        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("license_sequence_exhausted", error);

        fixture = CreateV1Fixture();
        success = VendorLicenseAmendmentPayloadBuilder.TryBuildMaxCompaniesIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            10, IssuedAt.AddMinutes(-1), out amendment, out error);

        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("amendment_issued_at_invalid", error);
    }


    [Fact]
    public void BuildsV2CompanyEntitlementIncreaseForAnExistingCompanyAndSnapshotsInputs()
    {
        var fixture = CreateV2Fixture();
        var secondCompany = new CompanyLicenseEntitlement(
            "company-0002",
            Array.AsReadOnly(new[] { "sales" }),
            10,
            Array.Empty<LicenseFeatureEntitlement?>());
        fixture.Payload = fixture.Payload with
        {
            CompanyEntitlements = new[]
            {
                fixture.Payload.CompanyEntitlements![0],
                secondCompany
            }
        };

        var mutableModules = new List<string> { "sales", LicenseSubsystemCodes.TradeImport };
        var mutableFeatures = new List<LicenseFeatureEntitlement?>
        {
            new("finance.cost_centers", LicenseFeatureValueTypes.MaxCount, MaxCount: 20),
            new("finance.budgeting", LicenseFeatureValueTypes.Enabled, Enabled: true),
            new("sales.pos", LicenseFeatureValueTypes.Enabled, Enabled: true)
        };

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildCompanyEntitlementIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            "company-0001", mutableModules, newMaxUsers: 40, featureIncreases: mutableFeatures,
            issuedAtUtc: IssuedAt.AddDays(1), out var amendment, out var error);

        Assert.True(success, error);
        Assert.NotNull(amendment);
        Assert.Equal(2, amendment!.FormatVersion);
        Assert.Equal(fixture.Payload.LicenseId, amendment.LicenseId);
        Assert.Equal(fixture.Payload.InstallationId, amendment.InstallationId);
        Assert.Equal(fixture.Payload.Sequence + 1, amendment.Sequence);
        Assert.Equal(fixture.Payload.Limits.MaxCompanies, amendment.Limits.MaxCompanies);
        Assert.Empty(amendment.Limits.Subsystems);
        Assert.Equal(fixture.Payload.SupportUntilUtc, amendment.SupportUntilUtc);
        Assert.Equal(fixture.Payload.UpdatesUntilUtc, amendment.UpdatesUntilUtc);

        var originalTarget = fixture.Payload.CompanyEntitlements![0]!;
        var amendedTarget = amendment.CompanyEntitlements![0]!;
        Assert.Equal("company-0001", amendedTarget.CompanyReference);
        Assert.Equal(40, amendedTarget.MaxUsers);
        Assert.Equal(
            new[] { LicenseSubsystemCodes.TradeImport, "finance", "inventory", "sales" },
            amendedTarget.Modules);
        Assert.Equal(3, amendedTarget.Features!.Count);
        Assert.Equal(20, amendedTarget.Features.Single(feature => feature!.Code == "finance.cost_centers")!.MaxCount);
        Assert.True(amendedTarget.Features.Single(feature => feature!.Code == "finance.budgeting")!.Enabled);
        Assert.True(amendedTarget.Features.Single(feature => feature!.Code == "sales.pos")!.Enabled);
        Assert.NotSame(originalTarget.Modules, amendedTarget.Modules);
        Assert.NotSame(originalTarget.Features, amendedTarget.Features);
        Assert.NotSame(originalTarget.Features![0], amendedTarget.Features[0]);

        var amendedOther = amendment.CompanyEntitlements[1]!;
        Assert.Equal(secondCompany.CompanyReference, amendedOther.CompanyReference);
        Assert.Equal(secondCompany.MaxUsers, amendedOther.MaxUsers);
        Assert.Equal(new[] { "sales" }, amendedOther.Modules);
        Assert.NotSame(secondCompany.Modules, amendedOther.Modules);

        mutableModules.Add("treasury");
        mutableFeatures.Add(new LicenseFeatureEntitlement(
            "finance.budgeting", LicenseFeatureValueTypes.Enabled, Enabled: false));

        Assert.DoesNotContain("treasury", amendedTarget.Modules!);
        Assert.Equal(3, amendedTarget.Features.Count);
        Assert.Empty(StructuralValidation.ValidatePayload(amendment));
        Assert.True(
            CanonicalJson.TryParsePayload(CanonicalJson.SerializePayload(amendment), out var parsed, out var parseError),
            parseError);
        Assert.Equal(40, parsed!.CompanyEntitlements![0]!.MaxUsers);
    }

    [Fact]
    public void CompanyEntitlementIncreaseRejectsLegacyV1WithoutImplicitUpgrade()
    {
        var fixture = CreateV1Fixture();

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildCompanyEntitlementIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            "company-0001", Array.Empty<string>(), newMaxUsers: 40,
            Array.Empty<LicenseFeatureEntitlement?>(), IssuedAt.AddHours(1),
            out var amendment, out var error);

        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("company_entitlements_require_payload_v2", error);
    }

    [Fact]
    public void CompanyEntitlementIncreaseRejectsUnknownCompanyReference()
    {
        var fixture = CreateV2Fixture();

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildCompanyEntitlementIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            "company-9999", new[] { "sales" }, null,
            Array.Empty<LicenseFeatureEntitlement?>(), IssuedAt.AddHours(1),
            out var amendment, out var error);

        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("company_not_entitled", error);
    }

    [Fact]
    public void CompanyEntitlementIncreaseRejectsUnknownDuplicateAndAlreadyEntitledModules()
    {
        var fixture = CreateV2Fixture();

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildCompanyEntitlementIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            "company-0001", new[] { "unknown-module" }, null,
            Array.Empty<LicenseFeatureEntitlement?>(), IssuedAt.AddHours(1),
            out var amendment, out var error);
        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("company_module_invalid", error);

        success = VendorLicenseAmendmentPayloadBuilder.TryBuildCompanyEntitlementIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            "company-0001", new[] { "sales", "sales" }, null,
            Array.Empty<LicenseFeatureEntitlement?>(), IssuedAt.AddHours(1),
            out amendment, out error);
        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("company_module_duplicate", error);

        success = VendorLicenseAmendmentPayloadBuilder.TryBuildCompanyEntitlementIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            "company-0001", new[] { "finance" }, null,
            Array.Empty<LicenseFeatureEntitlement?>(), IssuedAt.AddHours(1),
            out amendment, out error);
        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("company_module_already_entitled", error);
    }

    [Fact]
    public void CompanyEntitlementIncreaseRequiresFeatureCatalogTypeAndEntitledOwningModule()
    {
        var fixture = CreateV2Fixture();

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildCompanyEntitlementIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            "company-0001", Array.Empty<string>(), null,
            new[] { new LicenseFeatureEntitlement("finance.unknown", LicenseFeatureValueTypes.Enabled, Enabled: true) },
            IssuedAt.AddHours(1), out var amendment, out var error);
        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("company_feature_unknown", error);

        success = VendorLicenseAmendmentPayloadBuilder.TryBuildCompanyEntitlementIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            "company-0001", Array.Empty<string>(), null,
            new[] { new LicenseFeatureEntitlement("finance.budgeting", LicenseFeatureValueTypes.MaxCount, MaxCount: 5) },
            IssuedAt.AddHours(1), out amendment, out error);
        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("company_feature_type_mismatch", error);

        success = VendorLicenseAmendmentPayloadBuilder.TryBuildCompanyEntitlementIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            "company-0001", Array.Empty<string>(), null,
            new[] { new LicenseFeatureEntitlement("sales.pos", LicenseFeatureValueTypes.Enabled, Enabled: true) },
            IssuedAt.AddHours(1), out amendment, out error);
        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("company_feature_module_not_entitled", error);
    }

    [Fact]
    public void CompanyEntitlementIncreaseRejectsSeatAndFeatureDowngradesOrNoOpRequests()
    {
        var fixture = CreateV2Fixture();

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildCompanyEntitlementIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            "company-0001", Array.Empty<string>(), newMaxUsers: 25,
            Array.Empty<LicenseFeatureEntitlement?>(), IssuedAt.AddHours(1),
            out var amendment, out var error);
        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("company_user_limit_not_increased", error);

        success = VendorLicenseAmendmentPayloadBuilder.TryBuildCompanyEntitlementIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            "company-0001", Array.Empty<string>(), null,
            new[] { new LicenseFeatureEntitlement("finance.cost_centers", LicenseFeatureValueTypes.MaxCount, MaxCount: 10) },
            IssuedAt.AddHours(1), out amendment, out error);
        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("company_feature_not_increased", error);

        success = VendorLicenseAmendmentPayloadBuilder.TryBuildCompanyEntitlementIncrease(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            "company-0001", Array.Empty<string>(), null,
            Array.Empty<LicenseFeatureEntitlement?>(), IssuedAt.AddHours(1),
            out amendment, out error);
        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("amendment_has_no_entitlement_increase", error);
    }

    [Fact]
    public void BuildsV2SupportPeriodExtensionWithoutChangingUpdatesOrLicenseExpiry()
    {
        var fixture = CreateV2Fixture();
        var issuedAt = IssuedAt.AddDays(1);
        var requestedSupportUntil = new DateTimeOffset(2027, 2, 1, 0, 0, 0, TimeSpan.FromHours(3));

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildSupportAndUpdatePeriodExtension(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            requestedSupportUntil, newUpdatesUntilUtc: null, issuedAt,
            out var amendment, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(amendment);
        Assert.Equal(2, amendment!.FormatVersion);
        Assert.Equal(fixture.Payload.LicenseId, amendment.LicenseId);
        Assert.Equal(fixture.Payload.InstallationId, amendment.InstallationId);
        Assert.Equal(fixture.Payload.Sequence + 1, amendment.Sequence);
        Assert.Equal(requestedSupportUntil.ToUniversalTime(), amendment.SupportUntilUtc);
        Assert.Equal(fixture.Payload.UpdatesUntilUtc, amendment.UpdatesUntilUtc);
        Assert.Equal(fixture.Payload.ExpiresAtUtc, amendment.ExpiresAtUtc);
        Assert.NotSame(fixture.Payload.CompanyEntitlements, amendment.CompanyEntitlements);
        Assert.True(
            LicenseRevisionRules.TryValidateSuccessor(fixture.Payload, amendment, out var successorError),
            successorError);
    }

    [Fact]
    public void BuildsV2UpdatePeriodExtensionWithoutChangingSupportOrLicenseExpiry()
    {
        var fixture = CreateV2Fixture();
        var issuedAt = IssuedAt.AddDays(1);
        var requestedUpdatesUntil = IssuedAt.AddDays(180).ToOffset(TimeSpan.FromHours(-4));

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildSupportAndUpdatePeriodExtension(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            newSupportUntilUtc: null, requestedUpdatesUntil, issuedAt,
            out var amendment, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(amendment);
        Assert.Equal(fixture.Payload.SupportUntilUtc, amendment!.SupportUntilUtc);
        Assert.Equal(requestedUpdatesUntil.ToUniversalTime(), amendment.UpdatesUntilUtc);
        Assert.Equal(fixture.Payload.ExpiresAtUtc, amendment.ExpiresAtUtc);
        Assert.Equal(fixture.Payload.Sequence + 1, amendment.Sequence);
    }

    [Fact]
    public void SupportUpdatePeriodExtensionRejectsLegacyPayloadAndDoesNotGrantMissingPeriods()
    {
        var legacy = CreateV1Fixture();
        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildSupportAndUpdatePeriodExtension(
            legacy.Payload, legacy.License, legacy.Installation, legacy.Customer,
            IssuedAt.AddDays(60), newUpdatesUntilUtc: null, IssuedAt.AddDays(1),
            out var amendment, out var error);
        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("support_update_periods_require_payload_v2", error);

        var v2 = CreateV2Fixture();
        v2.Payload = v2.Payload with { SupportUntilUtc = null };
        success = VendorLicenseAmendmentPayloadBuilder.TryBuildSupportAndUpdatePeriodExtension(
            v2.Payload, v2.License, v2.Installation, v2.Customer,
            IssuedAt.AddDays(60), newUpdatesUntilUtc: null, IssuedAt.AddDays(1),
            out amendment, out error);
        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("support_period_not_previously_granted", error);
    }

    [Fact]
    public void SupportUpdatePeriodExtensionRejectsNoOpNonIncreasingNonFutureAndExpiredTermRequests()
    {
        var fixture = CreateV2Fixture();
        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildSupportAndUpdatePeriodExtension(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            newSupportUntilUtc: null, newUpdatesUntilUtc: null, IssuedAt.AddDays(1),
            out var amendment, out var error);
        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("amendment_has_no_period_increase", error);

        success = VendorLicenseAmendmentPayloadBuilder.TryBuildSupportAndUpdatePeriodExtension(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            fixture.Payload.SupportUntilUtc, newUpdatesUntilUtc: null, IssuedAt.AddDays(1),
            out amendment, out error);
        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("support_period_not_increased", error);

        fixture.Payload = fixture.Payload with { SupportUntilUtc = IssuedAt.AddDays(-2) };
        success = VendorLicenseAmendmentPayloadBuilder.TryBuildSupportAndUpdatePeriodExtension(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            IssuedAt.AddDays(-1), newUpdatesUntilUtc: null, IssuedAt,
            out amendment, out error);
        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("support_period_not_future", error);

        fixture = CreateV2Fixture();
        fixture.License.ValidUntilUtc = IssuedAt.AddHours(1);
        fixture.Payload = fixture.Payload with { ExpiresAtUtc = fixture.License.ValidUntilUtc };
        success = VendorLicenseAmendmentPayloadBuilder.TryBuildSupportAndUpdatePeriodExtension(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            IssuedAt.AddDays(60), newUpdatesUntilUtc: null, IssuedAt.AddDays(1),
            out amendment, out error);
        Assert.False(success);
        Assert.Null(amendment);
        Assert.Equal("license_outside_validity", error);
    }

    [Fact]
    public void PerpetualLicenseCanRenewAnExpiredPreviouslyGrantedSupportPeriod()
    {
        var fixture = CreateV2Fixture();
        fixture.License.LicenseKind = LicensePayload.KindPerpetual;
        fixture.License.ValidUntilUtc = null;
        fixture.Payload = fixture.Payload with
        {
            LicenseKind = LicensePayload.KindPerpetual,
            ExpiresAtUtc = null,
            SupportUntilUtc = IssuedAt.AddDays(-10)
        };

        var success = VendorLicenseAmendmentPayloadBuilder.TryBuildSupportAndUpdatePeriodExtension(
            fixture.Payload, fixture.License, fixture.Installation, fixture.Customer,
            IssuedAt.AddDays(60), newUpdatesUntilUtc: null, IssuedAt.AddDays(1),
            out var amendment, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(amendment);
        Assert.Null(amendment!.ExpiresAtUtc);
        Assert.Equal(IssuedAt.AddDays(60), amendment.SupportUntilUtc);
        Assert.Equal(fixture.Payload.UpdatesUntilUtc, amendment.UpdatesUntilUtc);
    }

    private static Fixture CreateV1Fixture()
    {
        var customer = new VendorCustomer { Id = Guid.NewGuid(), Status = "Active" };
        var installation = new VendorInstallation
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, Status = "Active"
        };
        var license = new VendorLicense
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, InstallationId = installation.Id,
            Status = "Issued", ProductCode = "FARANOU", LicenseKind = LicensePayload.KindTerm,
            MaxCompanies = 5, GraceDays = 10, LastIssuedSequence = 7,
            ValidFromUtc = IssuedAt.AddDays(-2), ValidUntilUtc = IssuedAt.AddDays(30)
        };
        var payload = new LicensePayload(
            FormatVersion: 1,
            LicenseId: license.Id.ToString("D"),
            InstallationId: installation.Id.ToString("D"),
            ProductCode: license.ProductCode,
            LicenseKind: license.LicenseKind,
            IssuedAtUtc: IssuedAt,
            NotBeforeUtc: license.ValidFromUtc,
            ExpiresAtUtc: license.ValidUntilUtc,
            GraceDays: license.GraceDays,
            Sequence: license.LastIssuedSequence,
            Limits: new LicenseLimits(license.MaxCompanies, LegacySubsystems));
        return new Fixture(customer, installation, license, payload);
    }

    private static Fixture CreateV2Fixture()
    {
        var fixture = CreateV1Fixture();
        fixture.Payload = new LicensePayload(
            FormatVersion: 2,
            LicenseId: fixture.Payload.LicenseId,
            InstallationId: fixture.Payload.InstallationId,
            ProductCode: fixture.Payload.ProductCode,
            LicenseKind: fixture.Payload.LicenseKind,
            IssuedAtUtc: fixture.Payload.IssuedAtUtc,
            NotBeforeUtc: fixture.Payload.NotBeforeUtc,
            ExpiresAtUtc: fixture.Payload.ExpiresAtUtc,
            GraceDays: fixture.Payload.GraceDays,
            Sequence: fixture.Payload.Sequence,
            Limits: new LicenseLimits(5, Array.Empty<string>()))
        {
            CompanyEntitlements = CompanyEntitlements,
            SupportUntilUtc = IssuedAt.AddDays(45),
            UpdatesUntilUtc = IssuedAt.AddDays(90)
        };
        return fixture;
    }

    private sealed class Fixture(
        VendorCustomer customer,
        VendorInstallation installation,
        VendorLicense license,
        LicensePayload payload)
    {
        public VendorCustomer Customer { get; } = customer;
        public VendorInstallation Installation { get; } = installation;
        public VendorLicense License { get; } = license;
        public LicensePayload Payload { get; set; } = payload;
    }
}
