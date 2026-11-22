using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Signing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// Policy-gate contract tests for license amendments: the candidate payload must keep every
/// entitlement the current signature-verified payload grants and may only carry entitlements the
/// pure <see cref="EntitlementPolicyEngine"/> accepts. Feature grant values and seat usage are
/// deliberately not re-decided here (D-054; disabled features are legitimate pre-grant states).
/// </summary>
public sealed class VendorAmendmentEntitlementPolicyTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private const string CompanyOne = "company-0001";
    private const string CompanyTwo = "company-0002";

    private static IReadOnlyList<LicenseFeatureEntitlement?> CostCenterFeature(int maxCount) =>
        Array.AsReadOnly<LicenseFeatureEntitlement?>([
            new LicenseFeatureEntitlement("finance.cost_centers", LicenseFeatureValueTypes.MaxCount, MaxCount: maxCount)
        ]);

    private static LicensePayload V1Payload(string[] subsystems, int sequence = 7) => new(
        FormatVersion: 1,
        LicenseId: "01234567-89ab-cdef-0123-456789abcdef",
        InstallationId: "fedcba98-7654-3210-fedc-ba9876543210",
        ProductCode: "FARANOU",
        LicenseKind: LicensePayload.KindPerpetual,
        IssuedAtUtc: IssuedAt,
        NotBeforeUtc: IssuedAt.AddDays(-2),
        ExpiresAtUtc: null,
        GraceDays: 30,
        Sequence: sequence,
        Limits: new LicenseLimits(5, Array.AsReadOnly(subsystems)));

    private static LicensePayload V2Payload(
        CompanyLicenseEntitlement?[] companies,
        int sequence = 7) => new(
        FormatVersion: 2,
        LicenseId: "01234567-89ab-cdef-0123-456789abcdef",
        InstallationId: "fedcba98-7654-3210-fedc-ba9876543210",
        ProductCode: "FARANOU",
        LicenseKind: LicensePayload.KindPerpetual,
        IssuedAtUtc: IssuedAt,
        NotBeforeUtc: IssuedAt.AddDays(-2),
        ExpiresAtUtc: null,
        GraceDays: 30,
        Sequence: sequence,
        Limits: new LicenseLimits(5, Array.Empty<string>()))
    {
        CompanyEntitlements = Array.AsReadOnly(companies)
    };

    private static CompanyLicenseEntitlement Company(
        string reference, string[] modules, int maxUsers = 25) => new(
        reference,
        Array.AsReadOnly(modules),
        maxUsers,
        CostCenterFeature(12));

    [Fact]
    public void V1SuccessorKeepingEveryGrantedSubsystemPasses()
    {
        var current = V1Payload(["finance", "sales"]);
        var candidate = V1Payload(["finance", "sales"], sequence: 8);

        Assert.Null(VendorAmendmentEntitlementPolicy.ValidateSuccessorKeepsEntitlements(current, candidate));
    }

    [Fact]
    public void V1CandidateRevokingASubsystemIsDenied()
    {
        var current = V1Payload(["finance", "sales"]);
        var candidate = V1Payload(["finance"], sequence: 8);

        Assert.Equal(VendorAmendmentEntitlementPolicy.PolicyViolationCode,
            VendorAmendmentEntitlementPolicy.ValidateSuccessorKeepsEntitlements(current, candidate));
    }

    [Fact]
    public void V1CandidateCarryingACompanyScopedOnlyCodeIsDenied()
    {
        var current = V1Payload(["finance"]);
        var candidate = V1Payload(["finance", LicenseSubsystemCodes.TradeImport], sequence: 8);

        Assert.Equal(VendorAmendmentEntitlementPolicy.PolicyViolationCode,
            VendorAmendmentEntitlementPolicy.ValidateSuccessorKeepsEntitlements(current, candidate));
    }

    [Fact]
    public void V2SuccessorKeepingCompaniesAndModulesPasses()
    {
        var current = V2Payload([Company(CompanyOne, ["finance", "inventory"])]);
        var candidate = V2Payload([Company(CompanyOne, ["finance", "inventory"])], sequence: 8);

        Assert.Null(VendorAmendmentEntitlementPolicy.ValidateSuccessorKeepsEntitlements(current, candidate));
    }

    [Fact]
    public void V2CandidateRevokingACompanyModuleIsDenied()
    {
        var current = V2Payload([Company(CompanyOne, ["finance", "inventory"])]);
        var candidate = V2Payload([Company(CompanyOne, ["finance"])], sequence: 8);

        Assert.Equal(VendorAmendmentEntitlementPolicy.PolicyViolationCode,
            VendorAmendmentEntitlementPolicy.ValidateSuccessorKeepsEntitlements(current, candidate));
    }

    [Fact]
    public void V2CandidateLosingAGrantedCompanyIsDenied()
    {
        var current = V2Payload([
            Company(CompanyOne, ["finance"]),
            Company(CompanyTwo, ["sales"])
        ]);
        var candidate = V2Payload([Company(CompanyOne, ["finance"])], sequence: 8);

        Assert.Equal(VendorAmendmentEntitlementPolicy.PolicyViolationCode,
            VendorAmendmentEntitlementPolicy.ValidateSuccessorKeepsEntitlements(current, candidate));
    }

    [Fact]
    public void V2CandidateWithAMalformedCompanyEntryIsDenied()
    {
        var current = V2Payload([Company(CompanyOne, ["finance"])]);
        var candidate = V2Payload([null, Company(CompanyOne, ["finance"])], sequence: 8);

        Assert.Equal(VendorAmendmentEntitlementPolicy.PolicyViolationCode,
            VendorAmendmentEntitlementPolicy.ValidateSuccessorKeepsEntitlements(current, candidate));
    }

    [Fact]
    public void V2CandidateWithAnUnknownModuleCodeIsDenied()
    {
        var current = V2Payload([Company(CompanyOne, ["finance"])]);
        var candidate = V2Payload([Company(CompanyOne, ["finance", "warehouse"])], sequence: 8);

        Assert.Equal(VendorAmendmentEntitlementPolicy.PolicyViolationCode,
            VendorAmendmentEntitlementPolicy.ValidateSuccessorKeepsEntitlements(current, candidate));
    }

    [Fact]
    public void V2CompanyScopedTradeImportRemainsAllowable()
    {
        var current = V2Payload([Company(CompanyOne, ["finance"])]);
        var candidate = V2Payload([Company(CompanyOne, ["finance", LicenseSubsystemCodes.TradeImport])], sequence: 8);

        Assert.Null(VendorAmendmentEntitlementPolicy.ValidateSuccessorKeepsEntitlements(current, candidate));
    }

    [Fact]
    public void DowngradeFromV2ToV1IsDenied()
    {
        var current = V2Payload([Company(CompanyOne, ["finance"])]);
        var candidate = V1Payload(["finance"], sequence: 8);

        Assert.Equal(VendorAmendmentEntitlementPolicy.PolicyViolationCode,
            VendorAmendmentEntitlementPolicy.ValidateSuccessorKeepsEntitlements(current, candidate));
    }

    [Fact]
    public void UpgradeFromV1ToV2IsCheckedOnTheCandidateSideOnly()
    {
        var current = V1Payload(["finance"]);
        var candidate = V2Payload([Company(CompanyOne, ["finance"])], sequence: 8);

        Assert.Null(VendorAmendmentEntitlementPolicy.ValidateSuccessorKeepsEntitlements(current, candidate));
    }
}
