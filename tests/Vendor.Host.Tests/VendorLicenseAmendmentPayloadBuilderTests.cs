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
        Assert.Same(fixture.Payload.CompanyEntitlements, amendment.CompanyEntitlements);
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
