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
